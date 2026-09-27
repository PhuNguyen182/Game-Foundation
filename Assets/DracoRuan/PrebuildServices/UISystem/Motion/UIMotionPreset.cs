using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// A reusable Show/Hide timeline, shared across many UIMotion instances instead of
    /// authored inline on each one (REWRITE_PLAN.md 2.7, "Preset"). An asset cannot hold a
    /// scene reference, so every track's `target` here is expected to be null with
    /// `targetPath` set instead ("" for Self, or a child path) - UIMotion resolves those
    /// against its own Transform once, in PrepareTimelines, before anything plays.
    /// Referencing a preset (UIMotion.preset) replaces the component's own inline
    /// showTracks/hideTracks/mirrorHide entirely - the two are not combined. "Apply
    /// preset" (copying this into a component's own inline tracks, for one-off tweaking
    /// after) is an Editor-tooling operation that doesn't exist yet.
    /// </summary>
    [CreateAssetMenu(menuName = "DracoRuan/UISystem/UIMotion Preset", fileName = "NewUIMotionPreset")]
    public sealed class UIMotionPreset : ScriptableObject
    {
        [SerializeField] private List<UIMotionTrack> showTracks = new List<UIMotionTrack>();
        [SerializeField] private List<UIMotionTrack> hideTracks = new List<UIMotionTrack>();
        [SerializeField] private bool mirrorHide;

        public List<UIMotionTrack> ShowTracks => this.showTracks;
        public List<UIMotionTrack> HideTracks => this.hideTracks;
        public bool MirrorHide => this.mirrorHide;

        /// <summary>Test-only setup hook (see Motion/AssemblyInfo.cs InternalsVisibleTo),
        /// mirroring UIMotion.ConfigureForTest: assigns the timelines directly instead of
        /// through the Inspector.</summary>
        internal void ConfigureForTest(IEnumerable<UIMotionTrack> show, IEnumerable<UIMotionTrack> hide, bool mirrorHide = false)
        {
            this.showTracks = new List<UIMotionTrack>(show);
            this.hideTracks = new List<UIMotionTrack>(hide);
            this.mirrorHide = mirrorHide;
        }
    }
}
