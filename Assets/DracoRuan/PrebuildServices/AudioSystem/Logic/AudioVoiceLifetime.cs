namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// Works out when a voice will have finished playing, so the pool can reclaim it on a clock
    /// rather than by asking the engine.
    /// </summary>
    /// <remarks>
    /// <para><b>Why not <c>!AudioSource.isPlaying</c>.</b> It reads false for a frame after
    /// <c>PlayDelayed</c>, false while <c>AudioListener.pause</c> is set, and false when Unity
    /// virtualises the source under its own voice cap. Each of those would reclaim a voice that is
    /// still meant to be audible, handing the slot, and every outstanding handle to it, to the next
    /// caller.</para>
    ///
    /// <para>The returned value is a point on the service's own clock, not on <c>Time.time</c>.
    /// A voice that gets paused therefore has to have this shifted by the time it spent paused,
    /// otherwise it expires while nobody is listening to it.</para>
    /// </remarks>
    public static class AudioVoiceLifetime
    {
        /// <summary>
        /// The moment a voice started at <paramref name="startTime"/> will have finished, or
        /// <see cref="float.PositiveInfinity"/> when it never will on its own.
        /// </summary>
        /// <param name="pitch">
        /// Playback rate. Zero would divide by zero, and Unity's reverse playback at a negative
        /// rate does not run a clip to a predictable end, so neither is given a finite budget:
        /// such a voice lives until something stops it.
        /// </param>
        public static float CalculateEndTime(
            float startTime,
            float delaySeconds,
            float clipLengthSeconds,
            float startOffsetSeconds,
            float pitch,
            bool isLoop)
        {
            if (isLoop || pitch <= 0f)
                return float.PositiveInfinity;

            float remaining = clipLengthSeconds - startOffsetSeconds;
            if (remaining <= 0f)
                return startTime + delaySeconds;

            return startTime + delaySeconds + (remaining / pitch);
        }
    }
}
