namespace DracoRuan.PrebuildServices.UISystem.Motion.Logic
{
    /// <summary>
    /// Resolves a track's raw serialized value (Absolute/RelativeToRest/RelativeToStart/
    /// FractionOfParent) against the pose captured at rest/track-start into the effective
    /// value the track should animate to.
    /// </summary>
    public static class UIMotionValueResolver
    {
        /// <param name="multiplicativeRelative">
        /// For kinds where the raw value is a literal multiplier of the reference pose
        /// (Scale: "0.8" means "80% of rest/start", not "rest/start + 0.8") rather than
        /// an offset to add to it. Only affects RelativeToRest/RelativeToStart.
        /// </param>
        public static Float4 Resolve(
            UIMotionValueMode mode, Float4 rawValue, Float4 rest, Float4 start, Float4 parentSize,
            bool multiplicativeRelative = false)
        {
            switch (mode)
            {
                case UIMotionValueMode.RelativeToRest:
                    return multiplicativeRelative ? Float4.Scale(rest, rawValue) : rest + rawValue;
                case UIMotionValueMode.RelativeToStart:
                    return multiplicativeRelative ? Float4.Scale(start, rawValue) : start + rawValue;
                case UIMotionValueMode.FractionOfParent:
                    // An OFFSET from rest, not an absolute position: "0.5" means "half the
                    // parent's width away from where this element sits at rest".
                    return rest + Float4.Scale(rawValue, parentSize);
                case UIMotionValueMode.Absolute:
                default:
                    return rawValue;
            }
        }
    }
}
