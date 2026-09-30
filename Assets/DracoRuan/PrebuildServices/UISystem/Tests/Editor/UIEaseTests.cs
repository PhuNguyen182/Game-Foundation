using System;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic.DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public class UIEaseTests
    {
        private static readonly UIEaseType[] AllTypes = (UIEaseType[])Enum.GetValues(typeof(UIEaseType));

        [Test]
        public void Evaluate_AtZero_ReturnsZero_ForEveryEaseType()
        {
            foreach (UIEaseType type in AllTypes)
            {
                float value = UIEase.Evaluate(type, 0f);
                Assert.AreEqual(0f, value, 1e-5f, $"{type} at t=0");
            }
        }

        [Test]
        public void Evaluate_AtOne_ReturnsOne_ForEveryEaseType()
        {
            foreach (UIEaseType type in AllTypes)
            {
                float value = UIEase.Evaluate(type, 1f);
                Assert.AreEqual(1f, value, 1e-5f, $"{type} at t=1");
            }
        }

        [Test]
        public void Evaluate_Linear_IsIdentity()
        {
            Assert.AreEqual(0.25f, UIEase.Evaluate(UIEaseType.Linear, 0.25f), 1e-6f);
            Assert.AreEqual(0.5f, UIEase.Evaluate(UIEaseType.Linear, 0.5f), 1e-6f);
            Assert.AreEqual(0.75f, UIEase.Evaluate(UIEaseType.Linear, 0.75f), 1e-6f);
        }

        [Test]
        public void Evaluate_InQuad_AtHalf_IsQuarter()
        {
            Assert.AreEqual(0.25f, UIEase.Evaluate(UIEaseType.InQuad, 0.5f), 1e-6f);
        }

        [Test]
        public void Evaluate_OutQuad_AtHalf_IsThreeQuarters()
        {
            Assert.AreEqual(0.75f, UIEase.Evaluate(UIEaseType.OutQuad, 0.5f), 1e-6f);
        }

        [Test]
        public void Evaluate_InOutQuad_AtHalf_IsHalf()
        {
            Assert.AreEqual(0.5f, UIEase.Evaluate(UIEaseType.InOutQuad, 0.5f), 1e-6f);
        }

        [Test]
        public void Evaluate_InCubic_AtHalf_IsOneEighth()
        {
            Assert.AreEqual(0.125f, UIEase.Evaluate(UIEaseType.InCubic, 0.5f), 1e-6f);
        }

        [Test]
        public void Evaluate_OutCubic_AtHalf_IsSevenEighths()
        {
            Assert.AreEqual(0.875f, UIEase.Evaluate(UIEaseType.OutCubic, 0.5f), 1e-6f);
        }

        [Test]
        public void Evaluate_InOutSine_AtHalf_IsHalf()
        {
            Assert.AreEqual(0.5f, UIEase.Evaluate(UIEaseType.InOutSine, 0.5f), 1e-5f);
        }

        [Test]
        public void Evaluate_InSine_AtHalf_MatchesReferenceValue()
        {
            // 1 - cos(pi/4)
            Assert.AreEqual(0.29289322f, UIEase.Evaluate(UIEaseType.InSine, 0.5f), 1e-5f);
        }

        [Test]
        public void Evaluate_OutSine_AtHalf_MatchesReferenceValue()
        {
            // sin(pi/4)
            Assert.AreEqual(0.70710678f, UIEase.Evaluate(UIEaseType.OutSine, 0.5f), 1e-5f);
        }

        [Test]
        public void Evaluate_OutBounce_AtOneThird_MatchesReferenceValue()
        {
            // First bounce segment: n1*t*t with n1=7.5625, t=1/3 -> 7.5625/9
            Assert.AreEqual(7.5625f / 9f, UIEase.Evaluate(UIEaseType.OutBounce, 1f / 3f), 1e-4f);
        }

        [Test]
        public void Evaluate_InBack_Overshoots_BelowZero_NearStart()
        {
            // Back eases characteristically overshoot past the [0,1] range.
            float value = UIEase.Evaluate(UIEaseType.InBack, 0.2f);
            Assert.Less(value, 0f);
        }

        [Test]
        public void Evaluate_OutBack_Overshoots_AboveOne_NearEnd()
        {
            float value = UIEase.Evaluate(UIEaseType.OutBack, 0.8f);
            Assert.Greater(value, 1f);
        }

        [Test]
        public void Evaluate_OutElastic_Overshoots_AboveOne_Somewhere()
        {
            bool anyAboveOne = false;
            for (int i = 1; i < 20; i++)
            {
                float t = i / 20f;
                if (UIEase.Evaluate(UIEaseType.OutElastic, t) > 1f)
                {
                    anyAboveOne = true;
                    break;
                }
            }

            Assert.IsTrue(anyAboveOne);
        }

        [Test]
        public void Evaluate_IsMonotonic_ForPureInOutEases()
        {
            UIEaseType[] monotonicTypes =
            {
                UIEaseType.Linear,
                UIEaseType.InSine, UIEaseType.OutSine, UIEaseType.InOutSine,
                UIEaseType.InQuad, UIEaseType.OutQuad, UIEaseType.InOutQuad,
                UIEaseType.InCubic, UIEaseType.OutCubic, UIEaseType.InOutCubic,
                UIEaseType.InQuart, UIEaseType.OutQuart, UIEaseType.InOutQuart,
                UIEaseType.InQuint, UIEaseType.OutQuint, UIEaseType.InOutQuint,
                UIEaseType.InExpo, UIEaseType.OutExpo, UIEaseType.InOutExpo,
                UIEaseType.InCirc, UIEaseType.OutCirc, UIEaseType.InOutCirc,
            };

            foreach (UIEaseType type in monotonicTypes)
            {
                float previous = UIEase.Evaluate(type, 0f);
                for (int i = 1; i <= 20; i++)
                {
                    float t = i / 20f;
                    float value = UIEase.Evaluate(type, t);
                    Assert.GreaterOrEqual(value, previous - 1e-5f, $"{type} not monotonic at t={t}");
                    previous = value;
                }
            }
        }
    }
}
