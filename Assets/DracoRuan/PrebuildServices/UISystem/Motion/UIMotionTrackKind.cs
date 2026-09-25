namespace DracoRuan.PrebuildServices.UISystem.Motion
{
    // Numbered explicitly and append-only: values are serialized on UIMotionTrack.
    public enum UIMotionTrackKind
    {
        Fade = 0,
        Move = 1,
        Rect = 2,
        Scale = 3,
        Rotate = 4,
        Color = 5,
        Fill = 6,
        Punch = 7,
        Shake = 8,
        SetActive = 9,
        AnimatorState = 10,
        Custom = 11,
    }
}
