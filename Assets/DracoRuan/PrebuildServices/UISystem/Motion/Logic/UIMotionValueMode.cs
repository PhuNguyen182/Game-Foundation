namespace DracoRuan.PrebuildServices.UISystem.Motion.Logic
{
    // Numbered explicitly and append-only: values are serialized on UIMotionTrack.
    public enum UIMotionValueMode
    {
        Absolute = 0,
        RelativeToRest = 1,
        RelativeToStart = 2,
        FractionOfParent = 3,
    }
}
