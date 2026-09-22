using System;

namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// The shaping functions behind every fade and cross-fade.
    /// </summary>
    /// <remarks>
    /// <para>Every curve is a <b>rising progress</b> function over 0..1, and the fade engine turns
    /// progress into a volume with <c>Lerp(from, to, progress)</c>. A fade-out is therefore
    /// expressed by its endpoints rather than by an inverted curve, which is what lets a fade be
    /// replaced mid-flight without any reasoning about which direction the curve was written for.</para>
    ///
    /// <para>Pure and engine-free on purpose: the constant-power property is the difference between
    /// a cross-fade that sounds seamless and one with an audible hole in the middle, and that
    /// deserves an assertion rather than a listening test.</para>
    /// </remarks>
    public static class AudioFadeCurve
    {
        private const float HalfPi = 1.5707963267948966f;

        /// <summary>
        /// Evaluates <paramref name="curve"/> at <paramref name="progress"/>, clamping progress to
        /// 0..1 so a caller that overshoots its duration cannot drive a volume out of range.
        /// </summary>
        public static float Evaluate(AudioFadeCurveType curve, float progress)
        {
            float t = progress <= 0f ? 0f
                : progress >= 1f ? 1f
                : progress;

            switch (curve)
            {
                case AudioFadeCurveType.Linear:
                    return t;

                case AudioFadeCurveType.EqualPowerIn:
                // A fade that still names EqualPower never went through ResolveDirectional. Rising
                // is the safe reading, and it keeps this function total rather than throwing deep
                // inside a tick.
                case AudioFadeCurveType.EqualPower:
                    return MathF.Sin(t * HalfPi);

                case AudioFadeCurveType.EqualPowerOut:
                    return 1f - MathF.Cos(t * HalfPi);

                case AudioFadeCurveType.Default:
                case AudioFadeCurveType.SmoothStep:
                default:
                    return t * t * (3f - (2f * t));
            }
        }

        /// <summary>
        /// Turns a direction-agnostic curve into the concrete one this fade needs, so
        /// <see cref="Evaluate"/> stays a plain lookup.
        /// </summary>
        /// <remarks>
        /// Only <see cref="AudioFadeCurveType.EqualPower"/> is directional; every other curve is
        /// returned unchanged. Call this once when the fade is created, not per tick.
        /// </remarks>
        public static AudioFadeCurveType ResolveDirectional(AudioFadeCurveType curve, float from, float to)
        {
            if (curve != AudioFadeCurveType.EqualPower)
                return curve;

            return to < from
                ? AudioFadeCurveType.EqualPowerOut
                : AudioFadeCurveType.EqualPowerIn;
        }
    }
}