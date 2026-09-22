namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// Combines an authored value with an optional random range.
    /// </summary>
    /// <remarks>
    /// An untouched range must leave the authored value completely alone. If an empty range quietly
    /// meant "between 0 and 0", every entry a designer had not filled in would go silent, and that
    /// is the kind of default that ships.
    /// </remarks>
    public static class AudioValueRange
    {
        /// <summary>
        /// <paramref name="authored"/> when the range has no width, otherwise a point inside it.
        /// </summary>
        /// <remarks>
        /// A range entered backwards is read as the interval it describes rather than rejected: a
        /// designer dragging two handles past each other should not silence the sound.
        /// </remarks>
        public static float Resolve(float authored, float min, float max, float roll01)
        {
            float low = min < max ? min : max;
            float high = min < max ? max : min;

            if (high - low <= 0f)
                return authored;

            float roll = roll01 <= 0f ? 0f
                : roll01 >= 1f ? 1f
                : roll01;

            return low + ((high - low) * roll);
        }
    }
}