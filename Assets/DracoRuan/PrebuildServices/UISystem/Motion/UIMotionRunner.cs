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
            UIMotionRunner.TickTracks(this.Playback);
            if (this.Playback.completionSource.TrySetResult(UIMotionPlaybackResult.Completed))
                this.Playback.removed = true;
        }
    }

    /// <summary>
    /// Single PlayerLoop-driven tick for every in-flight UIMotion timeline. Registers
    /// itself once (static ctor) and always reads Time.unscaledDeltaTime directly -
    /// UpdateServiceManager.Tick(deltaTime) passes scaled Time.deltaTime, and motion
    /// must keep running under timeScale = 0 (e.g. a pause menu popup).
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
            public IReadOnlyList<UIMotionTrack> tracks;
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

        private readonly List<Playback> _active = new List<Playback>(capacity: 16);

        static UIMotionRunner()
        {
            UpdateServiceManager.RegisterUpdateHandler(Instance);
        }

        /// <summary>Touching this triggers the static ctor, ensuring registration has happened.</summary>
        public static void EnsureRegistered()
        {
        }

        public static UIMotionPlaybackHandle Play(
            IReadOnlyList<UIMotionTrack> tracks, Vector4[] restPoses, float speed)
        {
            var scheduleInputs = new UIMotionScheduleInput[tracks.Count];
            for (int i = 0; i < tracks.Count; i++)
                scheduleInputs[i] = new UIMotionScheduleInput(tracks[i].startMode, tracks[i].offset, tracks[i].duration);

            float[] startTimes = UIMotionScheduler.ComputeStartTimes(scheduleInputs);
            float totalDuration = UIMotionScheduler.ComputeTotalDuration(scheduleInputs, startTimes);

            var trackStates = new TrackState[tracks.Count];
            for (int i = 0; i < trackStates.Length; i++)
                trackStates[i] = new TrackState();

            var playback = new Playback
            {
                tracks = tracks,
                startTimes = startTimes,
                trackStates = trackStates,
                restPoses = restPoses,
                totalDuration = totalDuration,
                speed = speed <= 0f ? 1f : speed,
                completionSource = new UniTaskCompletionSource<UIMotionPlaybackResult>(),
            };

            var handle = new UIMotionPlaybackHandle { Playback = playback };

            if (tracks.Count == 0 || totalDuration <= 0f)
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
                TickTracks(playback);

                if (playback.elapsed >= playback.totalDuration)
                {
                    playback.removed = true;
                    this._active.RemoveAt(i);
                    playback.completionSource.TrySetResult(UIMotionPlaybackResult.Completed);
                }
            }
        }

        internal static void TickTracks(Playback playback)
        {
            for (int i = 0; i < playback.tracks.Count; i++)
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
                    Float4 currentOrFrom = track.useStartValue
                        ? UIMotionValueResolver.Resolve(track.fromValueMode, track.from.ToFloat4(), rest, rest, parentSize)
                        : UIMotionTrackEvaluator.Capture(track).ToFloat4();
                    state.resolvedFrom = currentOrFrom.ToVector4();
                    state.resolvedTo = UIMotionValueResolver
                        .Resolve(track.toValueMode, track.to.ToFloat4(), rest, currentOrFrom, parentSize)
                        .ToVector4();
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
