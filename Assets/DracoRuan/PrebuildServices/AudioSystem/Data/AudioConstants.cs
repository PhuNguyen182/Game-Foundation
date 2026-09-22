namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>Tuning values shared by the audio service.</summary>
    public static class AudioConstants
    {
        /// <summary>
        /// Default ceiling on simultaneous voices.
        /// </summary>
        /// <remarks>
        /// Mobile hardware realistically manages 24 to 32 real voices before Unity starts
        /// virtualising by <c>AudioSource.priority</c> behind our back. Sitting at the bottom of
        /// that range keeps our own accounting and what you actually hear in agreement.
        /// </remarks>
        public const int DefaultMaxTotalVoices = 24;

        /// <summary>Default name of the channel that stands for the mixer's Master group.</summary>
        public const string DefaultMasterChannelId = "Master";

        /// <summary>
        /// How long an Addressables clip stays loaded after its last voice releases it.
        /// </summary>
        /// <remarks>
        /// Releasing immediately makes a footstep four hundred milliseconds apart thrash the
        /// loader. The grace period costs a little memory and removes the thrash entirely.
        /// </remarks>
        public const float DefaultClipUnloadGraceSeconds = 10f;

        /// <summary>Per-entry budget for a preload, so a failure names the clip that stalled.</summary>
        public const float DefaultPreloadTimeoutSeconds = 15f;

        /// <summary>
        /// Largest delta the tick will act on.
        /// </summary>
        /// <remarks>
        /// On the first frame after an app returns from the background, unscaled delta time can be
        /// tens of seconds. Unclamped, that single frame retires every live voice at once.
        /// </remarks>
        public const float DefaultMaxTickDeltaSeconds = 0.25f;

        /// <summary>Name of the GameObject that parents every pooled voice.</summary>
        public const string VoiceRootName = "[AudioSystem] Voices";

        /// <summary>Log prefix, matching the house convention.</summary>
        public const string LogTag = "AudioService";
    }
}
