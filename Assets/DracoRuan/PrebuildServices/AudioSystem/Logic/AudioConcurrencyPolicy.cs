namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// What an entry does when it is asked to play while it is already at its concurrency limit.
    /// </summary>
    /// <remarks>
    /// Authored per entry, because the right answer is a property of the sound: a footstep should
    /// be dropped, a looping ambience should hand its voice over, and an alarm should start again
    /// rather than stack into mush. Without <see cref="Restart"/> in particular, the same guard
    /// gets hand-written at every call site as
    /// <c>if (audio.IsPlaying(handle)) audio.Stop(handle);</c>.
    /// </remarks>
    public enum AudioConcurrencyPolicy
    {
        /// <summary>At the limit, the new request is refused. The right default for one-shots.</summary>
        DropNewest = 0,

        /// <summary>At the limit, the instance that started first gives up its voice.</summary>
        StealOldest = 1,

        /// <summary>Every live instance of this entry stops, so only the new one plays.</summary>
        Restart = 2,
    }
}
