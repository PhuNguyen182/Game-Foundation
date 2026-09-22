using System;

namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// Converts between the linear volume a designer thinks in (0..1) and the decibels an
    /// <c>AudioMixer</c> exposed parameter expects.
    /// </summary>
    /// <remarks>
    /// <para><b>Silence is a floor, never negative infinity.</b> <c>Log10(0)</c> is
    /// <c>-Infinity</c>, and an <c>AudioMixer.SetFloat</c> given a non-finite value poisons the
    /// mixer with NaN — every group downstream goes silent and stays that way until the Editor is
    /// restarted. Clamping at <see cref="MinDecibels"/> is the entire reason a caller should route
    /// through this class instead of writing the one-line formula inline.</para>
    ///
    /// <para>Uses <see cref="MathF"/> rather than <c>Mathf</c> so this assembly needs no engine
    /// reference and the conversion stays unit-testable without Unity.</para>
    /// </remarks>
    public static class AudioDecibels
    {
        /// <summary>The decibel value that means silence. Matches the bottom of a mixer fader.</summary>
        public const float MinDecibels = -80f;

        /// <summary>
        /// The quietest linear volume that still maps to a finite decibel value.
        /// Chosen so that it converts to exactly <see cref="MinDecibels"/>.
        /// </summary>
        public const float MinLinear = 0.0001f;

        /// <summary>
        /// Converts a linear volume to decibels, clamping anything at or below
        /// <see cref="MinLinear"/> (including zero and negatives) to <see cref="MinDecibels"/>.
        /// </summary>
        public static float LinearToDecibels(float linear)
        {
            if (linear <= MinLinear)
                return MinDecibels;

            return MathF.Log10(linear) * 20f;
        }

        /// <summary>
        /// Converts decibels back to a linear volume. At or below <see cref="MinDecibels"/> the
        /// result is exactly zero, so a fader dragged to the bottom reads back as silent rather
        /// than as a very small non-zero number.
        /// </summary>
        public static float DecibelsToLinear(float decibels)
        {
            if (decibels <= MinDecibels)
                return 0f;

            return MathF.Pow(10f, decibels / 20f);
        }
    }
}