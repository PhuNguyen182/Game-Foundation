#if UNITY_EDITOR
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// Edit-mode preview support for the Inspector (Play / Pause / Stop / scrub). The runner
    /// itself only ticks in Play Mode, so the Editor owns the clock and calls PreviewSeek
    /// with an absolute time instead - which is possible because UIMotionRunner.TickTracks is
    /// a pure function of Playback.elapsed. Every seek first puts the targets back at their
    /// rest pose and resets per-track state, so scrubbing backward is as deterministic as
    /// scrubbing forward. Compiled out of player builds entirely.
    /// </summary>
    public partial class UIMotion
    {
        private UIMotionRunner.Playback _previewPlayback;
        private Vector4[] _previewRestPoses;
        private bool _previewFailed;

        public bool IsPreviewing => this._previewPlayback != null;

        /// <summary>Total length of the timeline being previewed, in seconds of timeline time.</summary>
        public float PreviewDuration => this._previewPlayback?.totalDuration ?? 0f;

        /// <summary>Timeline seconds per real second (the component's speedOverride).</summary>
        public float PreviewSpeed => this.speedOverride <= 0f ? 1f : this.speedOverride;

        /// <summary>AnimatorState tracks are not previewed (they need AnimationMode's clip
        /// sampling); how many the current preview skipped.</summary>
        public int PreviewSkippedTrackCount { get; private set; }

        /// <summary>True once a track threw while previewing; ticking stops until the
        /// preview is rebuilt.</summary>
        public bool PreviewFailed => this._previewFailed;

        /// <summary>Starts (or restarts) a preview of the Show or Hide timeline, leaving every
        /// target at its rest pose (time 0 still needs a PreviewSeek). Rest poses are re-captured
        /// from the scene here, so this must only run while no preview is active - it ends any
        /// previous one first.</summary>
        public void PreviewBegin(bool show)
        {
            this.PreviewEnd();
            this.PrepareTimelines();

            UIMotionTrack[] tracks;
            Vector4[] restPoses;
            bool[] mirrorEase = null;

            // Snapshot copies (Clone), not the authored instances: the Inspector edits those
            // in place while a preview is running, and restoring with a track whose kind or
            // target just changed would write a rest pose into the wrong property.
            if (show)
            {
                tracks = SnapshotForPreview(this._showExpanded);
                restPoses = this._showRestPoses;
            }
            else if (this.mirrorHide)
            {
                (tracks, mirrorEase) = this.BuildMirrorHideTimeline();
                restPoses = this._showRestPoses;
            }
            else
            {
                tracks = SnapshotForPreview(this._hideExpanded);
                restPoses = this._hideRestPoses;
            }

            this._previewRestPoses = restPoses;
            this._previewPlayback = UIMotionRunner.CreatePlayback(tracks, restPoses, this.PreviewSpeed, mirrorEase);
            this._previewFailed = false;
            this.PreviewSkippedTrackCount = 0;
            this.ResetPreviewTrackStates();

            if (show)
                this._lastShowPlayback = this._previewPlayback;
        }

        /// <summary>Jumps the preview to `time` (clamped to [0, PreviewDuration]).</summary>
        public void PreviewSeek(float time)
        {
            UIMotionRunner.Playback playback = this._previewPlayback;
            if (playback == null)
                return;

            this.RestorePreviewPoses();
            if (this._previewFailed)
                return;

            this.ResetPreviewTrackStates();
            UIMotionRunner.PreApplyPendingStartValues(playback);

            playback.elapsed = Mathf.Clamp(time, 0f, playback.totalDuration);
            UIMotionRunner.TickTracksSafe(playback);

            // TickTracksSafe swallows an exception by ending the playback; that is the only
            // way `removed` can become true here, since the runner never ticks this one.
            if (playback.removed)
                this._previewFailed = true;
        }

        /// <summary>Ends the preview and puts every target back at its rest pose.</summary>
        public void PreviewEnd()
        {
            if (this._previewPlayback == null)
                return;

            this.RestorePreviewPoses();
            this._previewPlayback = null;
            this._previewRestPoses = null;
            this._previewFailed = false;
        }

        /// <summary>Writes rest poses back, and un-does Custom tracks that actually started.
        /// AnimatorState tracks are never touched by the preview, so they are never restored.</summary>
        private void RestorePreviewPoses()
        {
            UIMotionRunner.Playback playback = this._previewPlayback;
            for (int i = 0; i < playback.tracks.Length; i++)
            {
                UIMotionTrack track = playback.tracks[i];
                switch (track.kind)
                {
                    case UIMotionTrackKind.AnimatorState:
                        continue;

                    case UIMotionTrackKind.Custom:
                        if (playback.trackStates[i].started
                            && UIMotionTrackEvaluator.TryGetCustomTrack(track, out IUIMotionCustomTrack custom))
                        {
                            try
                            {
                                custom.Snap(toEnd: false);
                            }
                            catch (System.Exception ex)
                            {
                                this._previewFailed = true;
                                Debug.LogException(ex, this);
                            }
                        }

                        continue;

                    default:
                        UIMotionTrackEvaluator.Write(track, this._previewRestPoses[i]);
                        break;
                }
            }
        }

        private static UIMotionTrack[] SnapshotForPreview(UIMotionTrack[] source)
        {
            var copy = new UIMotionTrack[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                copy[i] = source[i].Clone();
                UIMotionTrackEvaluator.ResolveTargetCache(copy[i]);
            }

            return copy;
        }

        private void ResetPreviewTrackStates()
        {
            UIMotionRunner.Playback playback = this._previewPlayback;
            this.PreviewSkippedTrackCount = 0;

            for (int i = 0; i < playback.trackStates.Length; i++)
            {
                playback.trackStates[i] = new UIMotionRunner.TrackState();
                if (playback.tracks[i].kind != UIMotionTrackKind.AnimatorState)
                    continue;

                playback.trackStates[i].finished = true;
                this.PreviewSkippedTrackCount++;
            }
        }
    }
}
#endif