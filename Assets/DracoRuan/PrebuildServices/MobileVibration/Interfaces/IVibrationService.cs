using DracoRuan.PrebuildServices.MobileVibration.Data;

namespace DracoRuan.PrebuildServices.MobileVibration.Interfaces
{
    /// <summary>
    /// Plays and stops the game's mobile haptics, wrapping <c>MOST_HapticFeedback</c>.
    /// </summary>
    /// <remarks>
    /// <para>Only one haptic plays at a time — the underlying plugin has exactly one playback slot.
    /// Starting a new one implicitly replaces whatever was playing, the same way the plugin's own
    /// <c>Generate</c> methods behave.</para>
    ///
    /// <para>All native code the plugin calls is compiled out under <c>!UNITY_EDITOR</c>, so nothing
    /// here produces a physical vibration in the desktop Editor. Build to a device (or use Unity
    /// Remote) to feel it.</para>
    /// </remarks>
    public interface IVibrationService
    {
        /// <summary>Whether the database is indexed and ready to serve <see cref="Play"/>.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Passthrough to <c>MOST_HapticFeedback.HapticsEnabled</c>, itself persisted in PlayerPrefs.
        /// </summary>
        bool HapticsEnabled { get; set; }

        /// <summary>
        /// Whether a Custom Pattern or Curve haptic is still running. A Preset play returns
        /// immediately and never sets this true.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>The id currently tracked as playing, or null when idle.</summary>
        string CurrentlyPlayingId { get; }

        /// <summary>Looks up an entry by its generated id.</summary>
        bool TryGetEntry(string vibrationId, out VibrationEntry entry);

        /// <summary>
        /// Plays <paramref name="vibrationId"/>. Returns false when the id does not exist,
        /// <see cref="HapticsEnabled"/> is off, or the entry's cooldown has not elapsed.
        /// </summary>
        bool Play(string vibrationId);

        /// <summary>Stops whatever is playing, unconditionally. For emergency stops.</summary>
        void Stop();

        /// <summary>
        /// Stops only when <paramref name="vibrationId"/> is the entry <see cref="CurrentlyPlayingId"/>
        /// currently tracks. Returns false otherwise, leaving anything else's haptic untouched — the
        /// unconditional <see cref="Stop()"/> exists for the emergency case, this one is for a caller
        /// that only wants to cancel its own play.
        /// </summary>
        bool Stop(string vibrationId);

        /// <summary>Warms up the native side ahead of the first play. Passthrough to the plugin.</summary>
        void Prewarm();

        /// <summary>Whether this device supports haptics at all. Passthrough to the plugin.</summary>
        bool IsSupported();

        /// <summary>
        /// Whether this device supports Core Haptics curves (iOS only). Passthrough to the plugin.
        /// </summary>
        bool IsCoreHapticsSupported();
    }
}
