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

        private Vector4[] _showRestPoses;
        private Vector4[] _hideRestPoses;
        private UIMotionPlaybackHandle _currentHandle;
        private IUIMotionTriggerSource _triggerSource;

        private void Awake()
        {
            UIMotionRunner.EnsureRegistered();
            this._showRestPoses = CaptureRestPoses(this.showTracks);
            this._hideRestPoses = CaptureRestPoses(this.hideTracks);
        }

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

            this._currentHandle?.Stop();
        }

        private void OnParentShown() => this.Restart();

        /// <summary>Snaps every track (both timelines) back to its rest pose, then plays Show.</summary>
        public void Restart()
        {
            this._currentHandle?.Stop();
            SnapToPoses(this.showTracks, this._showRestPoses);
            SnapToPoses(this.hideTracks, this._hideRestPoses);
            this.PlayShowAsync().Forget();
        }

        public UniTask<UIMotionPlaybackResult> PlayShowAsync() => this.Play(this.showTracks, this._showRestPoses);

        public UniTask<UIMotionPlaybackResult> PlayHideAsync() => this.Play(this.hideTracks, this._hideRestPoses);

        public void Stop() => this._currentHandle?.Stop();

        public void SnapShow()
        {
            this._currentHandle?.Stop();
            SnapToEndValues(this.showTracks, this._showRestPoses);
        }

        public void SnapHide()
        {
            this._currentHandle?.Stop();
            SnapToEndValues(this.hideTracks, this._hideRestPoses);
        }

        private UniTask<UIMotionPlaybackResult> Play(List<UIMotionTrack> tracks, Vector4[] restPoses)
        {
            this._currentHandle?.Stop();
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
                Float4 start = track.useStartValue
                    ? Logic.UIMotionValueResolver.Resolve(track.fromValueMode, track.from.ToFloat4(), rest, rest, parentSize)
                    : rest;
                Float4 end = Logic.UIMotionValueResolver.Resolve(track.toValueMode, track.to.ToFloat4(), rest, start, parentSize);
                UIMotionTrackEvaluator.Write(track, end.ToVector4());
            }
        }
    }
}
