namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// Picks which clip variant an entry plays, avoiding an immediate repeat.
    /// </summary>
    /// <remarks>
    /// <para>Variants exist so a footstep does not sound identical forty times a minute. Without
    /// them a designer authors <c>footstep_01</c> through <c>footstep_05</c> as five separate ids
    /// and call sites start building id strings by concatenation, which defeats the point of a
    /// generated constant entirely.</para>
    ///
    /// <para>The roll is mapped onto the other variants rather than re-rolled until it differs.
    /// Re-rolling has no bound on how long it runs and still repeats sometimes; this is exact and
    /// constant time.</para>
    /// </remarks>
    public static class AudioClipVariantSelector
    {
        /// <summary>
        /// The variant to play, given how many there are and which one played last.
        /// </summary>
        /// <param name="lastIndex">The previous pick, or any out-of-range value for "none yet".</param>
        /// <param name="roll01">A random value in 0..1.</param>
        public static int Next(int variantCount, int lastIndex, float roll01)
        {
            if (variantCount <= 1)
                return 0;

            float roll = roll01 <= 0f ? 0f
                : roll01 >= 1f ? 0.9999999f
                : roll01;

            bool hasUsableLast = lastIndex >= 0 && lastIndex < variantCount;
            if (!hasUsableLast)
                return (int)(roll * variantCount);

            // Choose among the other variants, then step over the one just played. This is why the
            // result can never equal lastIndex and never leaves the range.
            int picked = (int)(roll * (variantCount - 1));
            return picked >= lastIndex ? picked + 1 : picked;
        }
    }
}