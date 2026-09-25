namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// Implemented by a component referenced from a Custom-kind UIMotionTrack. A normal
    /// Unity object reference, so unlike SerializeReference-based tracks there's no
    /// class-rename/IL2CPP-strip data-loss risk.
    /// </summary>
    public interface IUIMotionCustomTrack
    {
        /// <summary>Called once, at the moment the track starts, before the first Sample.</summary>
        void CaptureStart();

        /// <summary>Called every tick while the track is active. t is unclamped eased progress.</summary>
        void Sample(float t);

        /// <summary>Called to jump straight to a pose without ticking through it.</summary>
        void Snap(bool toEnd);
    }
}
