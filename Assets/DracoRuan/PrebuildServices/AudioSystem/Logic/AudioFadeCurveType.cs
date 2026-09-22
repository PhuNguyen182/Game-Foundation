namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// The shape a fade follows from its start value to its target.
    /// </summary>
    /// <remarks>
    /// Every member is a rising progress curve; the fade engine turns progress into a volume with
    /// <c>Lerp(from, to, progress)</c>. <see cref="EqualPower"/> is the exception in that it is not
    /// a shape but a request to pick <see cref="EqualPowerIn"/> or <see cref="EqualPowerOut"/> from
    /// the fade's own direction, which is what a caller writing
    /// <c>CrossFadeChannel(..., AudioFadeCurveType.EqualPower)</c> means.
    /// </remarks>
    public enum AudioFadeCurveType
    {
        /// <summary>Whatever the system considers a sensible plain fade. Today: <see cref="SmoothStep"/>.</summary>
        Default = 0,

        /// <summary>Constant rate of change. Correct for a value, wrong for a cross-fade.</summary>
        Linear = 1,

        /// <summary>Eases in and out. The default for a plain fade in or out.</summary>
        SmoothStep = 2,

        /// <summary>Resolves to <see cref="EqualPowerIn"/> or <see cref="EqualPowerOut"/> by direction.</summary>
        EqualPower = 3,

        /// <summary>The rising half of a constant-power cross-fade.</summary>
        EqualPowerIn = 4,

        /// <summary>The progress curve whose <c>1 - progress</c> is the falling half of a constant-power cross-fade.</summary>
        EqualPowerOut = 5,

        // A decibel-linear ("logarithmic") fade is deliberately absent. Unlike the curves above it
        // has no single rising shape: a perceptually even fade-in is concave and a perceptually
        // even fade-out is convex, so it would need its own In/Out pair and a second directional
        // rule. SmoothStep covers plain fades and EqualPower covers cross-fades, so nothing asks
        // for it yet. Add it as a pair, not as one member, if that changes.
    }
}