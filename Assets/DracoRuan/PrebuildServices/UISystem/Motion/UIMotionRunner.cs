using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    public enum UIMotionPlaybackResult
    {
        Completed = 0,
        Replaced = 1,
    }

    /// <summary>Handle to one in-flight timeline playback. Owned by whoever called Play.</summary>
    public sealed class UIMotionPlaybackHandle
    {
        internal UIMotionRunner.Playback Playback;

        public UniTask<UIMotionPlaybackResult> Task => this.Playback.completionSource.Task;

        /// <summary>Stops ticking immediately, leaving every track at its current mid-value
        /// (no snap). Matches "play cutting play": the interrupted playback should not
        /// jump anywhere, just freeze where it was. An in-flight AnimatorState track is
        /// the one exception to "just stop ticking it is enough to freeze it": Unity keeps
        /// advancing an enabled Animator on its own every frame regardless of whether this
        /// runner still calls into it, so freezing its pose means disabling it here.</summary>
        public void Stop()
        {
            if (this.Playback.completionSource.TrySetResult(UIMotionPlaybackResult.Replaced))
            {
                this.Playback.removed = true;
                UIMotionRunner.FreezeInFlightAnimatorTracks(this.Playback);
            }
        }

        /// <summary>Forces every track straight to its resolved end value and completes.</summary>
        public void SnapToEnd()
        {
            if (this.Playback.removed)
                return;

            this.Playback.elapsed = this.Playback.totalDuration;
            UIMotionRunner.TickTracksSafe(this.Playback);
            if (this.Playback.completionSource.TrySetResult(UIMotionPlaybackResult.Completed))
                this.Playback.removed = true;
        }
    }

    /// <summary>
    /// Single PlayerLoop-driven tick for every in-flight UIMotion timeline. Registration
    /// is lazy (EnsureRegistered, called from UIMotion.Awake) and reset every time a new
    /// play session starts via RuntimeInitializeOnLoadMethod - NOT a static constructor.
    /// This project runs with domain reload disabled on entering play mode, so a static
    /// constructor only ever runs once for the whole editor process; a second Play press
    /// would leave the runner permanently unregistered and every PlayShowAsync/
    /// PlayHideAsync await hanging forever. RuntimeInitializeOnLoadMethod is guaranteed to
    /// fire on every play-mode entry regardless of the domain/scene reload setting, which
    /// is exactly why it exists. Also always reads Time.unscaledDeltaTime directly -
    /// UpdateServiceManager.Tick(deltaTime) passes scaled Time.deltaTime, and motion must
    /// keep running under timeScale = 0 (e.g. a pause menu popup).
    /// </summary>
    public sealed class UIMotionRunner : IUpdateHandler
    {
        internal sealed class TrackState
        {
            public bool started;
            public bool finished;
            public Vector4 resolvedFrom;
            public Vector4 resolvedTo;

            /// <summary>AnimatorState only: whether the once-only timeout warning has
            /// already been logged for this track's run.</summary>
            public bool animatorTimeoutWarned;
        }

        internal sealed class Playback
        {
            public UIMotionTrack[] tracks;
            public float[] startTimes;
            public TrackState[] trackStates;
            public Vector4[] restPoses;
            public float elapsed;
            public float totalDuration;
            public float speed = 1f;

            /// <summary>Per-track "play this track's ease backward" flag, used by a
            /// generated Mirror Hide timeline (see UIMotion.BuildMirrorHideTimeline).
            /// Null for an authored timeline - no per-track lookup or allocation on the
            /// common path.</summary>
            public bool[] mirrorEase;

            public UniTaskCompletionSource<UIMotionPlaybackResult> completionSource;

            /// <summary>Set once removed from the active list, either by the runner
            /// finishing it or by the handle's Stop()/SnapToEnd(). Guards against the
            /// runner's own Tick loop double-removing a playback the handle just ended.</summary>
            public bool removed;
        }

        private static readonly UIMotionRunner Instance = new UIMotionRunner();
        private static bool _registered;

        /// <summary>Accessibility switch: when true, every newly-started playback jumps
        /// straight to its resolved end values instead of interpolating, regardless of
        /// authored durations. Applies from the next Play() call onward; does not affect
        /// playbacks already in flight.</summary>
        public static bool ReduceMotion { get; set; }

        private readonly List<Playback> _active = new List<Playback>(capacity: 16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewPlaySession()
        {
            Instance._active.Clear();
            _registered = false;
        }

        /// <summary>Idempotent; call before relying on the runner (UIMotion.Awake does).</summary>
        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            _registered = true;
            UpdateServiceManager.RegisterUpdateHandler(Instance);
        }

        public static UIMotionPlaybackHandle Play(
            IReadOnlyList<UIMotionTrack> tracks, Vector4[] restPoses, float speed, bool[] mirrorEase = null)
        {
            EnsureRegistered();

            // Snapshot into an array: the caller's List<UIMotionTrack> is a live,
            // editor-editable field. Adding a track mid-playback must not let
            // trackStates/startTimes go out of sync with tracks.Count.
            var trackArray = new UIMotionTrack[tracks.Count];
            for (int i = 0; i < trackArray.Length; i++)
                trackArray[i] = tracks[i];

            var scheduleInputs = new UIMotionScheduleInput[trackArray.Length];
            for (int i = 0; i < trackArray.Length; i++)
                scheduleInputs[i] = new UIMotionScheduleInput(trackArray[i].startMode, trackArray[i].offset, trackArray[i].duration);

            float[] startTimes = UIMotionScheduler.ComputeStartTimes(scheduleInputs);
            float totalDuration = UIMotionScheduler.ComputeTotalDuration(scheduleInputs, startTimes);

            var trackStates = new TrackState[trackArray.Length];
            for (int i = 0; i < trackStates.Length; i++)
                trackStates[i] = new TrackState();

            var playback = new Playback
            {
                tracks = trackArray,
                startTimes = startTimes,
                trackStates = trackStates,
                restPoses = restPoses,
                totalDuration = totalDuration,
                speed = speed <= 0f ? 1f : speed,
                mirrorEase = mirrorEase,
                completionSource = new UniTaskCompletionSource<UIMotionPlaybackResult>(),
            };

            var handle = new UIMotionPlaybackHandle { Playback = playback };

            // Pre-apply every useStartValue-on track's resolved Start value immediately,
            // even for tracks that won't actually start until later (AfterPrevious
            // chains). Otherwise a not-yet-started track still shows its rest pose -
            // e.g. a Show recommended-pattern track (alpha 0 -> Rest) would render fully
            // visible until its own turn, then pop to invisible and fade in.
            PreApplyPendingStartValues(playback);

            // Reduce-motion: skip straight to the end instead of interpolating. Reuses
            // the same "already past totalDuration" completion path below rather than a
            // separate branch, so it is exercised by the exact same tested code as a
            // zero-duration timeline.
            if (ReduceMotion)
                playback.elapsed = playback.totalDuration;

            // Sample frame 0 synchronously instead of waiting for the next Tick: the
            // UpdateServices callback runs before Update()/Start(), so a track that opens
            // this same frame (e.g. OnEnable -> Restart -> PlayShowAsync from a button
            // click) would otherwise render one full frame at the rest pose first.
            TickTracksSafe(playback);

            if (playback.elapsed >= playback.totalDuration)
            {
                playback.removed = true;
                playback.completionSource.TrySetResult(UIMotionPlaybackResult.Completed);
                return handle;
            }

            Instance._active.Add(playback);
            return handle;
        }

        void IUpdateHandler.Tick(float _)
        {
            float deltaTime = Time.unscaledDeltaTime;

            for (int i = this._active.Count - 1; i >= 0; i--)
            {
                Playback playback = this._active[i];
                if (playback.removed)
                {
                    this._active.RemoveAt(i);
                    continue;
                }

                playback.elapsed += deltaTime * playback.speed;
                TickTracksSafe(playback);

                if (playback.elapsed >= playback.totalDuration && !playback.removed)
                {
                    playback.removed = true;
                    this._active.RemoveAt(i);
                    playback.completionSource.TrySetResult(UIMotionPlaybackResult.Completed);
                }
                else if (playback.removed)
                {
                    // TickTracksSafe's own catch block ended this playback (see below).
                    this._active.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Ticks one playback, isolating any exception (a destroyed target, a track
        /// referencing a component that was never Awoken, ...) to that single playback
        /// instead of letting it escape into UpdateServiceManager's shared loop, where it
        /// would skip every lower-index handler this frame and repeat forever since the
        /// failing playback would never be removed.
        /// </summary>
        internal static void TickTracksSafe(Playback playback)
        {
            try
            {
                TickTracks(playback);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (playback.completionSource.TrySetResult(UIMotionPlaybackResult.Completed))
                    playback.removed = true;
            }
        }

        private static void PreApplyPendingStartValues(Playback playback)
        {
            for (int i = 0; i < playback.tracks.Length; i++)
            {
                UIMotionTrack track = playback.tracks[i];
                if (!track.useStartValue || playback.trackStates[i].started)
                    continue;

                Float4 rest = playback.restPoses[i].ToFloat4();
                Float4 parentSize = UIMotionTrackEvaluator.GetParentSize(track).ToFloat4();
                bool multiplicative = track.kind == UIMotionTrackKind.Scale;
                Float4 from = UIMotionValueResolver.Resolve(track.fromValueMode, track.from.ToFloat4(), rest, rest, parentSize, multiplicative);
                UIMotionTrackEvaluator.Write(track, from.ToVector4());
            }
        }

        private static void TickTracks(Playback playback)
        {
            for (int i = 0; i < playback.tracks.Length; i++)
            {
                UIMotionTrack track = playback.tracks[i];
                TrackState state = playback.trackStates[i];
                if (state.finished || playback.elapsed < playback.startTimes[i])
                    continue;

                if (track.kind == UIMotionTrackKind.Custom)
                {
                    TickCustomTrack(track, state, playback, i);
                    continue;
                }

                if (track.kind == UIMotionTrackKind.AnimatorState)
                {
                    TickAnimatorTrack(track, state, playback, i);
                    continue;
                }

                bool oscillating = track.kind == UIMotionTrackKind.Punch || track.kind == UIMotionTrackKind.Shake;

                if (!state.started)
                {
                    state.started = true;

                    if (oscillating)
                    {
                        // Always oscillates around whatever the target's value is right
                        // now, regardless of useStartValue - the plan hides that toggle
                        // entirely for these two kinds, so nothing here should depend on it.
                        state.resolvedFrom = UIMotionTrackEvaluator.Capture(track);
                        state.resolvedTo = state.resolvedFrom;
                    }
                    else
                    {
                        Float4 rest = playback.restPoses[i].ToFloat4();
                        Float4 parentSize = UIMotionTrackEvaluator.GetParentSize(track).ToFloat4();
                        bool multiplicative = track.kind == UIMotionTrackKind.Scale;

                        Float4 currentOrFrom = track.useStartValue
                            ? UIMotionValueResolver.Resolve(track.fromValueMode, track.from.ToFloat4(), rest, rest, parentSize, multiplicative)
                            : UIMotionTrackEvaluator.Capture(track).ToFloat4();
                        Float4 resolvedTo = UIMotionValueResolver
                            .Resolve(track.toValueMode, track.to.ToFloat4(), rest, currentOrFrom, parentSize, multiplicative);

                        state.resolvedFrom = currentOrFrom.ToVector4();
                        state.resolvedTo = resolvedTo.ToVector4();

                        if (track.kind == UIMotionTrackKind.Rotate)
                            state.resolvedTo = UIMotionTrackEvaluator.UnwrapRotation(state.resolvedFrom, state.resolvedTo);

                        if (track.kind == UIMotionTrackKind.SetActive)
                        {
                            // Discrete action: fires once, the instant the track starts, no
                            // easing and no further writes for the rest of this track's window.
                            UIMotionTrackEvaluator.Write(track, state.resolvedTo);
                            state.finished = true;
                            continue;
                        }
                    }
                }

                float localT = track.duration <= 0f
                    ? 1f
                    : Mathf.Clamp01((playback.elapsed - playback.startTimes[i]) / track.duration);

                if (oscillating)
                {
                    // Amplitude rides on the otherwise-unused `to.x` field (the plan gives
                    // Punch/Shake no field of their own beyond "chỉ có biên độ"): not an
                    // eased lerp between two fixed endpoints like every other kind, so it
                    // bypasses Evaluate()/mirrorEase entirely.
                    Vector4 oscValue = UIMotionTrackEvaluator.EvaluateOscillation(track.kind, state.resolvedFrom, track.to.x, localT);
                    UIMotionTrackEvaluator.Write(track, oscValue);
                    if (localT >= 1f)
                        state.finished = true;
                    continue;
                }

                bool mirrorEase = playback.mirrorEase != null && playback.mirrorEase[i];
                float easedT = mirrorEase ? 1f - track.Evaluate(1f - localT) : track.Evaluate(localT);
                Vector4 value = Vector4.LerpUnclamped(state.resolvedFrom, state.resolvedTo, easedT);
                UIMotionTrackEvaluator.Write(track, value);

                if (localT >= 1f)
                    state.finished = true;
            }
        }

        /// <summary>
        /// Custom tracks skip the Vector4 capture/resolve/lerp pipeline entirely - the
        /// referenced IUIMotionCustomTrack owns its own values. Still goes through the
        /// normal schedule/duration/ease (mirrorEase included), per the interface's own
        /// "Sample(t): t is unclamped eased progress" contract - only the resolvedFrom/To
        /// Vector4 math is irrelevant here, not the timing.
        /// </summary>
        private static void TickCustomTrack(UIMotionTrack track, TrackState state, Playback playback, int i)
        {
            if (!UIMotionTrackEvaluator.TryGetCustomTrack(track, out IUIMotionCustomTrack custom))
            {
                state.finished = true;
                return;
            }

            if (!state.started)
            {
                state.started = true;
                custom.CaptureStart();
            }

            float localT = track.duration <= 0f
                ? 1f
                : Mathf.Clamp01((playback.elapsed - playback.startTimes[i]) / track.duration);
            bool mirrorEase = playback.mirrorEase != null && playback.mirrorEase[i];
            float easedT = mirrorEase ? 1f - track.Evaluate(1f - localT) : track.Evaluate(localT);

            custom.Sample(easedT);

            if (localT >= 1f)
                state.finished = true;
        }

        /// <summary>
        /// AnimatorState skips the Vector4 pipeline entirely, same as Custom - but unlike
        /// every other kind, it does not need this runner to write anything per tick at
        /// all: an enabled Animator advances itself every frame through Unity's own
        /// animation update. This method only (a) starts the state once and (b) polls for
        /// real completion (REWRITE_PLAN.md 2.7: never trust `duration`/`bakedDuration` for
        /// completion, only `GetCurrentAnimatorStateInfo`), snapping-and-disabling on
        /// either a genuine end or a timeout. `pastScheduledEnd` covers two different
        /// paths through the same branch: the normal "just reached my own duration" tick,
        /// and a forced jump (SnapToEnd putting `playback.elapsed` at `totalDuration`)
        /// landing on a track that had not even started yet - both must end at
        /// normalizedTime=1, not start fresh at 0.
        /// </summary>
        private static void TickAnimatorTrack(UIMotionTrack track, TrackState state, Playback playback, int i)
        {
            if (!UIMotionTrackEvaluator.TryGetAnimator(track, out Animator animator))
            {
                state.finished = true;
                return;
            }

            float elapsedInTrack = playback.elapsed - playback.startTimes[i];
            bool pastScheduledEnd = elapsedInTrack >= track.duration;

            if (!state.started)
            {
                state.started = true;
                animator.keepAnimatorStateOnDisable = true;
                animator.writeDefaultValuesOnDisable = false;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = track.animatorSpeed <= 0f ? 1f : track.animatorSpeed;
                animator.enabled = true;
                animator.Play(track.animatorStateHash, track.animatorLayer, pastScheduledEnd ? 1f : 0f);
                animator.Update(0f);

                if (pastScheduledEnd)
                {
                    animator.enabled = false;
                    state.finished = true;
                    return;
                }
            }
            else if (pastScheduledEnd)
            {
                animator.Play(track.animatorStateHash, track.animatorLayer, 1f);
                animator.Update(0f);
                animator.enabled = false;
                state.finished = true;
                return;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(track.animatorLayer);
            bool reachedEnd = info.shortNameHash == track.animatorStateHash
                && info.normalizedTime >= 1f
                && !animator.IsInTransition(track.animatorLayer);

            if (reachedEnd)
            {
                animator.enabled = false;
                state.finished = true;
                return;
            }

            float timeout = track.duration * 2f + 0.5f;
            if (elapsedInTrack < timeout)
                return;

            if (!state.animatorTimeoutWarned)
            {
                state.animatorTimeoutWarned = true;
                Debug.LogWarning(
                    $"UIMotion: AnimatorState track on '{animator.name}' (state hash {track.animatorStateHash}) " +
                    $"did not report completion within {timeout:0.00}s - snapping to its end pose. " +
                    "Check that animatorStateHash/duration are baked against the right state.");
            }

            animator.Play(track.animatorStateHash, track.animatorLayer, 1f);
            animator.Update(0f);
            animator.enabled = false;
            state.finished = true;
        }

        /// <summary>See UIMotionPlaybackHandle.Stop(): an interrupted playback must
        /// explicitly disable any AnimatorState track it left mid-flight, since Unity
        /// keeps ticking an enabled Animator regardless of whether this runner does.</summary>
        internal static void FreezeInFlightAnimatorTracks(Playback playback)
        {
            for (int i = 0; i < playback.tracks.Length; i++)
            {
                UIMotionTrack track = playback.tracks[i];
                TrackState state = playback.trackStates[i];
                if (track.kind != UIMotionTrackKind.AnimatorState || !state.started || state.finished)
                    continue;

                state.finished = true;
                if (UIMotionTrackEvaluator.TryGetAnimator(track, out Animator animator))
                    animator.enabled = false;
            }
        }
    }
}
