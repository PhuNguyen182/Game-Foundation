namespace DracoRuan.PrebuildServices.UISystem.Motion.Logic
{
    /// <summary>
    /// Resolves a track's raw serialized value (Absolute/RelativeToRest/RelativeToStart/
    /// FractionOfParent) against the pose captured at rest/track-start into the effective
    /// value the track should animate to.
    /// </summary>
    public static class UIMotionValueResolver
    {
        public static Float4 Resolve(UIMotionValueMode mode, Float4 rawValue, Float4 rest, Float4 start, Float4 parentSize)
        {
            switch (mode)
            {
                case UIMotionValueMode.RelativeToRest:
                    return rest + rawValue;
                case UIMotionValueMode.RelativeToStart:
                    return start + rawValue;
                case UIMotionValueMode.FractionOfParent:
                    return Float4.Scale(rawValue, parentSize);
                case UIMotionValueMode.Absolute:
                default:
                    return rawValue;
            }
        }

        public static Float4 ResolveRest(Float4 rest) => rest;
    }
}
