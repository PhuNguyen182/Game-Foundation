namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>What a voice is currently doing.</summary>
    public enum AudioPlaybackState
    {
        /// <summary>Not in use. Also what a stale handle reports.</summary>
        Free = 0,

        /// <summary>Acquired, but its clip is still being loaded. Nothing is audible yet.</summary>
        Loading = 1,

        /// <summary>Audible.</summary>
        Playing = 2,

        /// <summary>Held at its current playback position by an explicit Pause.</summary>
        Paused = 3,

        /// <summary>Fading towards silence, after which it releases itself.</summary>
        FadingOut = 4,
    }
}
