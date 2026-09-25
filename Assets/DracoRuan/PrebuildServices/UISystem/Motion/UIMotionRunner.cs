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
        /// jump anywhere, just freeze where it was.</summary>
        public void Stop()
        {
            if (this.Playback.completionSource.TrySetResult(UIMotionPlaybackResult.Replaced))
                this.Playback.removed = true;
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
            public UniTaskCompletionSource<UIMotionPlaybackResult> completionSource;

            /// <summary>Set once removed from the active list, either by the runner
            /// finishing it or by the handle's Stop()/SnapToEnd(). Guards against the
            /// runner's own Tick loop double-removing a playback the handle just ended.</summary>
            public bool removed;
        }

        private static readonly UIMotionRunner Instance = new UIMotionRunner();
        private static bool _registered;

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

        public static UIMotionPlaybackHandle Play(IReadOnlyList<UIMotionTrack> tracks, Vector4[] restPoses, float speed)
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
                completionSource = new UniTaskCompletionSource<UIMotionPlaybackResult>(),
            };

            var handle = new UIMotionPlaybackHandle { Playback = playback };

            // Pre-apply every useStartValue-on track's resolved Start value immediately,
            // even for tracks that won't actually start until later (AfterPrevious
            // chains). Otherwise a not-yet-started track still shows its rest pose -
            // e.g. a Show recommended-pattern track (alpha 0 -> Rest) would render fully
            // visible until its own turn, then pop to invisible and fade in.
            PreApplyPendingStartValues(playback);

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

                if (!state.started)
                {
                    state.started = true;
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

                float localT = track.duration <= 0f
                    ? 1f
                    : Mathf.Clamp01((playback.elapsed - playback.startTimes[i]) / track.duration);
                float easedT = track.Evaluate(localT);
                Vector4 value = Vector4.LerpUnclamped(state.resolvedFrom, state.resolvedTo, easedT);
                UIMotionTrackEvaluator.Write(track, value);

                if (localT >= 1f)
                    state.finished = true;
            }
        }
    }
}
