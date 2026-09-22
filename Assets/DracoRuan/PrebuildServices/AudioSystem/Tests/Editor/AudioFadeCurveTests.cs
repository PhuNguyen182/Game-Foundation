using System;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects the fade shaping used by every fade and cross-fade.
    /// </summary>
    /// <remarks>
    /// Every curve here is a <b>progress</b> function: it runs 0 to 1, and the fade engine turns
    /// that into a volume with <c>Lerp(from, to, progress)</c>. Keeping all curves rising means a
    /// fade-out is expressed by its endpoints, not by an inverted curve, so interrupting one fade
    /// with another never has to reason about which direction the curve was written for.
    ///
    /// The constant-power test is the one with teeth: two correlated tracks cross-faded on linear
    /// curves lose about 3 dB at the midpoint, which is audible as a hole in the music on every
    /// area transition.
    /// </remarks>
    [TestFixture]
    public sealed class AudioFadeCurveTests
    {
        private static readonly AudioFadeCurveType[] AllCurves =
            (AudioFadeCurveType[])Enum.GetValues(typeof(AudioFadeCurveType));

        // ---- Shared properties every curve must have ----

        [Test]
        public void Evaluate_AtStart_IsZeroForEveryCurve()
        {
            foreach (AudioFadeCurveType curve in AllCurves)
                Assert.That(AudioFadeCurve.Evaluate(curve, 0f), Is.EqualTo(0f).Within(1e-5f), curve.ToString());
        }

        [Test]
        public void Evaluate_AtEnd_IsOneForEveryCurve()
        {
            foreach (AudioFadeCurveType curve in AllCurves)
                Assert.That(AudioFadeCurve.Evaluate(curve, 1f), Is.EqualTo(1f).Within(1e-5f), curve.ToString());
        }

        [Test]
        public void Evaluate_AcrossTheWholeFade_NeverGoesBackwards()
        {
            foreach (AudioFadeCurveType curve in AllCurves)
            {
                float previous = -1f;
                for (int i = 0; i <= 100; i++)
                {
                    float value = AudioFadeCurve.Evaluate(curve, i / 100f);
                    Assert.That(value, Is.GreaterThanOrEqualTo(previous - 1e-5f),
                        $"{curve} dipped at t={i / 100f}");
                    previous = value;
                }
            }
        }

        [Test]
        public void Evaluate_StaysWithinZeroAndOneForEveryCurve()
        {
            foreach (AudioFadeCurveType curve in AllCurves)
            {
                for (int i = 0; i <= 100; i++)
                {
                    float value = AudioFadeCurve.Evaluate(curve, i / 100f);
                    Assert.That(value, Is.InRange(-1e-5f, 1f + 1e-5f), $"{curve} at t={i / 100f}");
                }
            }
        }

        [Test]
        public void Evaluate_OutsideTheFade_IsClamped()
        {
            Assert.That(AudioFadeCurve.Evaluate(AudioFadeCurveType.Linear, -0.5f), Is.EqualTo(0f));
            Assert.That(AudioFadeCurve.Evaluate(AudioFadeCurveType.Linear, 1.5f), Is.EqualTo(1f));
        }

        // ---- Individual shapes ----

        [Test]
        public void Evaluate_Linear_IsTheIdentity()
        {
            Assert.That(AudioFadeCurve.Evaluate(AudioFadeCurveType.Linear, 0.25f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(AudioFadeCurve.Evaluate(AudioFadeCurveType.Linear, 0.75f), Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void Evaluate_SmoothStep_EasesInAndOutAroundTheMidpoint()
        {
            Assert.That(AudioFadeCurve.Evaluate(AudioFadeCurveType.SmoothStep, 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(AudioFadeCurve.Evaluate(AudioFadeCurveType.SmoothStep, 0.25f), Is.LessThan(0.25f));
            Assert.That(AudioFadeCurve.Evaluate(AudioFadeCurveType.SmoothStep, 0.75f), Is.GreaterThan(0.75f));
        }

        [Test]
        public void Evaluate_Default_IsSmoothStep()
        {
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                Assert.That(AudioFadeCurve.Evaluate(AudioFadeCurveType.Default, t),
                    Is.EqualTo(AudioFadeCurve.Evaluate(AudioFadeCurveType.SmoothStep, t)).Within(1e-6f));
            }
        }

        // ---- The property that makes a cross-fade sound right ----

        [Test]
        public void EqualPowerPair_SumsToConstantPower_SoACrossFadeHasNoHoleInTheMiddle()
        {
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;

                // Incoming voice fades 0 -> 1, so its volume is the progress itself.
                float incoming = AudioFadeCurve.Evaluate(AudioFadeCurveType.EqualPowerIn, t);

                // Outgoing voice fades 1 -> 0, so its volume is Lerp(1, 0, progress) = 1 - progress.
                float outgoing = 1f - AudioFadeCurve.Evaluate(AudioFadeCurveType.EqualPowerOut, t);

                float power = (incoming * incoming) + (outgoing * outgoing);
                Assert.That(power, Is.EqualTo(1f).Within(1e-4f), $"power dipped at t={t}");
            }
        }

        [Test]
        public void EqualPowerPair_AtTheMidpoint_IsLouderThanLinearWouldBe()
        {
            float incoming = AudioFadeCurve.Evaluate(AudioFadeCurveType.EqualPowerIn, 0.5f);

            Assert.That(incoming, Is.GreaterThan(0.5f),
                "A linear cross-fade would put both voices at 0.5 here, which is the ~3 dB dip.");
        }

        // ---- Direction resolution ----

        [Test]
        public void ResolveDirectional_RisingFade_BecomesTheEqualPowerFadeIn()
        {
            Assert.That(AudioFadeCurve.ResolveDirectional(AudioFadeCurveType.EqualPower, from: 0f, to: 1f),
                Is.EqualTo(AudioFadeCurveType.EqualPowerIn));
        }

        [Test]
        public void ResolveDirectional_FallingFade_BecomesTheEqualPowerFadeOut()
        {
            Assert.That(AudioFadeCurve.ResolveDirectional(AudioFadeCurveType.EqualPower, from: 1f, to: 0f),
                Is.EqualTo(AudioFadeCurveType.EqualPowerOut));
        }

        [Test]
        public void ResolveDirectional_AnyOtherCurve_IsLeftAlone()
        {
            Assert.That(AudioFadeCurve.ResolveDirectional(AudioFadeCurveType.SmoothStep, from: 1f, to: 0f),
                Is.EqualTo(AudioFadeCurveType.SmoothStep));
        }
    }
}