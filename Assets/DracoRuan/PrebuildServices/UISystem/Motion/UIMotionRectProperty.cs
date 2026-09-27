namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    // Numbered explicitly and append-only: value is serialized on UIMotionTrack.
    // Only meaningful when UIMotionTrack.kind == UIMotionTrackKind.Rect.
    public enum UIMotionRectProperty
    {
        AnchoredPosition = 0,
        AnchorMin = 1,
        AnchorMax = 2,
        Anchors = 3,
        Pivot = 4,
        SizeDelta = 5,
        OffsetMin = 6,
        OffsetMax = 7,
    }
}
