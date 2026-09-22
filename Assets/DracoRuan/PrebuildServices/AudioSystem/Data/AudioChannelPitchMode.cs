namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>How a channel applies its pitch.</summary>
    /// <remarks>
    /// <see cref="PerVoice"/> is the default deliberately. Driving pitch through the mixer needs the
    /// group's Pitch parameter to have been exposed by hand, which is routinely forgotten and fails
    /// silently, and it resamples the entire bus including anything else routed through it.
    /// Multiplying into each live <c>AudioSource</c> is exact, free at this voice count, and cannot
    /// be mis-authored.
    /// </remarks>
    public enum AudioChannelPitchMode
    {
        /// <summary>Multiply the channel pitch into every voice on the channel.</summary>
        PerVoice = 0,

        /// <summary>Write the channel pitch to an exposed mixer parameter.</summary>
        MixerParameter = 1,
    }
}
