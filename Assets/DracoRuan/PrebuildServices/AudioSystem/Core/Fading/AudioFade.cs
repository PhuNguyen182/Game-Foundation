using DracoRuan.PrebuildServices.AudioSystem.Logic;

namespace DracoRuan.PrebuildServices.AudioSystem.Core.Fading
{
    /// <summary>What happens to a voice once its fade reaches the end.</summary>
    public enum AudioFadeCompletion
    {
        /// <summary>Nothing. The voice keeps playing at its new volume.</summary>
        None = 0,

        /// <summary>Release the voice.</summary>
        Stop = 1,

        /// <summary>Pause the voice, leaving it resumable.</summary>
        Pause = 2,
    }

    /// <summary>One fade in flight.</summary>
    public struct AudioFade
    {
        public bool IsActive;
        public float From;
        public float To;
        public float Duration;
        public float Elapsed;
        public AudioFadeCurveType Curve;
        public AudioFadeCompletion Completion;

        /// <summary>
        /// Set while the voice is still loading. The fade holds at <see cref="From"/> and only
        /// starts counting when the first sample plays.
        /// </summary>
        /// <remarks>
        /// Without this, a two second cross-fade over a one and a half second bundle load is three
        /// quarters finished before there is any sound to fade.
        /// </remarks>
        public bool PendingStart;
    }
}
