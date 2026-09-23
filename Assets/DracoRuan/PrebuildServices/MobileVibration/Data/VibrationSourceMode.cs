namespace DracoRuan.PrebuildServices.MobileVibration.Data
{
    /// <summary>How a <see cref="VibrationEntry"/> produces its haptic.</summary>
    public enum VibrationSourceMode
    {
        /// <summary>One of <c>MOST_HapticFeedback.HapticTypes</c>. Fires and returns immediately.</summary>
        Preset = 0,

        /// <summary>An authored per-platform pulse sequence.</summary>
        CustomPattern = 1,

        /// <summary>An authored intensity envelope, sampled into pulses at play time.</summary>
        Curve = 2,
    }
}
