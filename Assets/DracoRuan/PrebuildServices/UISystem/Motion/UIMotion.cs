using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// Standalone animation component: two timelines (Show/Hide), each a flat list of
    /// UIMotionTrack. Does not reference UISystem runtime - works on any GameObject.
    /// See REWRITE_PLAN.md section 2.7 for the full spec. This first pass covers Show/
    /// Hide playback, rest-pose capture/restart, "new Play stops the old one in place",
    /// OnEnable/OnParentShow/Manual triggers. NOT yet implemented: Mirror Hide, stagger,
    /// cancellation-token snap-to-end wiring, HideAndDeactivateAsync/SetActiveAnimated,
    /// reduce-motion, and the Rect/Punch/Shake/AnimatorState/Custom track kinds.
    /// </summary>
    public class UIMotion : MonoBehaviour
    {
        [SerializeField] private List<UIMotionTrack> showTracks = new List<UIMotionTrack>();
        [SerializeField] private List<UIMotionTrack> hideTracks = new List<UIMotionTrack>();
        [SerializeField] private UIMotionTrigger trigger = UIMotionTrigger.OnEnable;
        [SerializeField] private float speedOverride = 1f;

        private bool _prepared;
        private Vector4[] _showRestPoses;
        private Vector4[] _hideRestPoses;
        private UIMotionPlaybackHandle _currentHandle;
        private IUIMotionTriggerSource _triggerSource;

        private void Awake() => this.EnsurePrepared();

        private void OnEnable()
        {
            switch (this.trigger)
            {
                case UIMotionTrigger.OnEnable:
                    this.Restart();
                    break;
                case UIMotionTrigger.OnParentShow:
                    this._triggerSource = this.GetComponentInParent<IUIMotionTriggerSource>();
                    if (this._triggerSource != null)
                        this._triggerSource.ParentShown += this.OnParentShown;
                    break;
            }
        }

        private void OnDisable()
        {
            if (this._triggerSource != null)
            {
                this._triggerSource.ParentShown -= this.OnParentShown;
                this._triggerSource = null;
            }

            this.StopCurrent();
        }

        private void OnParentShown() => this.Restart();

        /// <summary>
        /// Captures rest poses and registers with the runner if that hasn't happened yet.
        /// Awake does this for the common case, but a UIMotion whose GameObject was never
        /// activated (Awake never ran) can still be driven via script - every public entry
        /// point below calls this first so _showRestPoses/_hideRestPoses is never null.
        /// </summary>
        private void EnsurePrepared()
        {
            if (this._prepared)
                return;

            this._prepared = true;
            UIMotionRunner.EnsureRegistered();
            this._showRestPoses = CaptureRestPoses(this.showTracks);
            this._hideRestPoses = CaptureRestPoses(this.hideTracks);
        }

        /// <summary>Snaps every track (both timelines) back to its rest pose, then plays Show.</summary>
        public void Restart()
        {
            this.EnsurePrepared();
            this.StopCurrent();
            SnapToPoses(this.showTracks, this._showRestPoses);
            SnapToPoses(this.hideTracks, this._hideRestPoses);
            this.PlayShowAsync().Forget();
        }

        public UniTask<UIMotionPlaybackResult> PlayShowAsync()
        {
            this.EnsurePrepared();
            return this.Play(this.showTracks, this._showRestPoses);
        }

        public UniTask<UIMotionPlaybackResult> PlayHideAsync()
        {
            this.EnsurePrepared();
            return this.Play(this.hideTracks, this._hideRestPoses);
        }

        public void Stop() => this.StopCurrent();

        public void SnapShow()
        {
            this.EnsurePrepared();
            this.StopCurrent();
            SnapToEndValues(this.showTracks, this._showRestPoses);
        }

        public void SnapHide()
        {
            this.EnsurePrepared();
            this.StopCurrent();
            SnapToEndValues(this.hideTracks, this._hideRestPoses);
        }

        private UniTask<UIMotionPlaybackResult> Play(List<UIMotionTrack> tracks, Vector4[] restPoses)
        {
            this.StopCurrent();

            UIMotionPlaybackHandle handle = UIMotionRunner.Play(tracks, restPoses, this.speedOverride);
            this._currentHandle = handle;
            return AwaitAndClear(this, handle);

            static async UniTask<UIMotionPlaybackResult> AwaitAndClear(UIMotion self, UIMotionPlaybackHandle handle)
            {
                UIMotionPlaybackResult result = await handle.Task;
                if (self._currentHandle == handle)
                    self._currentHandle = null;
                return result;
            }
        }

        /// <summary>
        /// Stops the current playback, tolerant of a Stop() continuation re-entrantly
        /// starting a new one (its completion source resumes awaiters inline). Detaches
        /// _currentHandle before calling Stop() so that reentrant continuation sees no
        /// "current" handle to clobber; if it starts a new playback anyway, that one is
        /// stopped too, since whoever called this method is about to become current.
        /// </summary>
        private void StopCurrent()
        {
            UIMotionPlaybackHandle handle = this._currentHandle;
            this._currentHandle = null;
            handle?.Stop();

            if (this._currentHandle != null)
            {
                UIMotionPlaybackHandle reentrant = this._currentHandle;
                this._currentHandle = null;
                reentrant.Stop();
            }
        }

        private static Vector4[] CaptureRestPoses(List<UIMotionTrack> tracks)
        {
            var result = new Vector4[tracks.Count];
            for (int i = 0; i < tracks.Count; i++)
                result[i] = UIMotionTrackEvaluator.Capture(tracks[i]);
            return result;
        }

        private static void SnapToPoses(List<UIMotionTrack> tracks, Vector4[] poses)
        {
            for (int i = 0; i < tracks.Count; i++)
                UIMotionTrackEvaluator.Write(tracks[i], poses[i]);
        }

        private static void SnapToEndValues(List<UIMotionTrack> tracks, Vector4[] restPoses)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                UIMotionTrack track = tracks[i];
                Float4 rest = restPoses[i].ToFloat4();
                Float4 parentSize = UIMotionTrackEvaluator.GetParentSize(track).ToFloat4();
                bool multiplicative = track.kind == UIMotionTrackKind.Scale;

                Float4 start = track.useStartValue
                    ? UIMotionValueResolver.Resolve(track.fromValueMode, track.from.ToFloat4(), rest, rest, parentSize, multiplicative)
                    : rest;
                Float4 end = UIMotionValueResolver.Resolve(track.toValueMode, track.to.ToFloat4(), rest, start, parentSize, multiplicative);

                Vector4 endVector = end.ToVector4();
                if (track.kind == UIMotionTrackKind.Rotate)
                    endVector = UIMotionTrackEvaluator.UnwrapRotation(start.ToVector4(), endVector);

                UIMotionTrackEvaluator.Write(track, endVector);
            }
        }
    }
}
