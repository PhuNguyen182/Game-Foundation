using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>
    /// EditMode tests for UIBinderTweenExtensions.TweenValue's ReactiveProperty&lt;T&gt;
    /// overload (see UIMotionRunnerTweenTests.cs for why duration &lt;= 0 is used
    /// throughout - the whole tween then completes synchronously, with no dependency on
    /// UIMotionRunner's PlayerLoop tick, which only actually runs in Play Mode).
    /// </summary>
    [TestFixture]
    public sealed class UIBinderTweenExtensionsTests
    {
        [TearDown]
        public void TearDown() => UIMotionRunner.ReduceMotion = false;

        [Test]
        public void TweenValue_ReactiveProperty_Bind_ReplaysExistingValueOnceAsInstantSync()
        {
            var property = new ReactiveProperty<float>(5f);
            var applied = new List<float>();

            UIBinder b = default;
            // ReactiveProperty.Subscribe itself replays the current value synchronously -
            // this first tween's own from/to both come from that replay (5 -> 5), so it
            // applies exactly once even before Build() returns, not zero times.
            b.TweenValue(property, 0f, UIEaseType.Linear, v => applied.Add(v));
            IDisposable binding = b.Build();

            Assert.That(applied, Is.EqualTo(new[] { 5f }));

            binding.Dispose();
            property.Dispose();
        }

        [Test]
        public void TweenValue_ReactiveProperty_ValueChangesAfterBind_TweensFromSeededValue()
        {
            var property = new ReactiveProperty<float>(5f);
            var applied = new List<float>();

            UIBinder b = default;
            b.TweenValue(property, 0f, UIEaseType.Linear, v => applied.Add(v));
            IDisposable binding = b.Build();

            property.Value = 20f;

            // [0] is Subscribe's own initial-value replay (5 -> 5, see the sibling test);
            // [1] is the real change, tweening from the seeded 5 to 20 - not from == to.
            Assert.That(applied, Is.EqualTo(new[] { 5f, 20f }));

            binding.Dispose();
            property.Dispose();
        }

        [Test]
        public void TweenValue_ReactiveProperty_SecondChange_TweensFromPreviousTargetNotFromSnappedValue()
        {
            var property = new ReactiveProperty<float>(0f);
            var applied = new List<float>();

            UIBinder b = default;
            // Non-zero duration: the first tween must still be in-flight (not yet
            // completed) when the second Value change interrupts it, so this exercises
            // "cancel the old tween in place and start the new one from wherever the
            // running `current` tracker last landed" rather than the duration<=0 path.
            b.TweenValue(property, 1f, UIEaseType.Linear, v => applied.Add(v));
            IDisposable binding = b.Build();

            property.Value = 10f;
            int countAfterFirstChange = applied.Count;
            Assert.That(applied[applied.Count - 1], Is.EqualTo(0f)); // frame-0 sample: still at the old current

            property.Value = 100f;

            // The second change must cut the first tween off in place and start from the
            // last value that tween actually applied (0, its own frame-0 sample) - not
            // snap to 10 (the value the interrupted tween was still travelling toward).
            Assert.That(applied.Count, Is.GreaterThan(countAfterFirstChange));
            Assert.That(applied[applied.Count - 1], Is.EqualTo(0f));

            binding.Dispose();
            property.Dispose();
        }

        [Test]
        public void TweenValue_ReactiveProperty_Unbind_DisposesInFlightTween()
        {
            var property = new ReactiveProperty<float>(0f);
            var applied = new List<float>();

            UIBinder b = default;
            b.TweenValue(property, 1f, UIEaseType.Linear, v => applied.Add(v));
            IDisposable binding = b.Build();

            property.Value = 10f; // starts an in-flight (non-zero duration) tween
            int countBeforeUnbind = applied.Count;

            binding.Dispose();

            // No PlayerLoop tick can run in EditMode (see class remarks), so the only way
            // this assertion could fail is if Unbind left the tween's own Dispose call
            // unregistered - i.e. leaked. Changing the property after Unbind must not
            // resurrect it either: the subscription itself must be gone.
            property.Value = 999f;
            Assert.That(applied.Count, Is.EqualTo(countBeforeUnbind));

            property.Dispose();
        }

        [Test]
        public void TweenValue_ReactiveProperty_CurveOverload_UsesCurve()
        {
            var property = new ReactiveProperty<float>(0f);
            var applied = new List<float>();
            // Evaluate(anything in [0,1]) == 42 for a constant curve, used here as the
            // eased-t fed into Lerp(from, to, t) - not the applied value directly. The
            // initial replay tweens 0 -> 0 (Lerp(0, 0, 42) == 0 regardless of t), so only
            // the real change (0 -> 1, Lerp(0, 1, 42) == 42) proves the curve is actually
            // being read instead of the UIEaseType overload's ease table.
            var curve = UnityEngine.AnimationCurve.Constant(0f, 1f, 42f);

            UIBinder b = default;
            b.TweenValue(property, 0f, curve, v => applied.Add(v));
            IDisposable binding = b.Build();

            property.Value = 1f;

            Assert.That(applied, Is.EqualTo(new[] { 0f, 42f }));

            binding.Dispose();
            property.Dispose();
        }

        [Test]
        public void TweenLocalRotation_ValueChanges_TweensFromTargetsCurrentRotation()
        {
            var go = new GameObject("RotationTarget");
            try
            {
                go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var source = new Subject<Quaternion>();

                UIBinder b = default;
                b.TweenLocalRotation(go.transform, source, 0f);
                IDisposable binding = b.Build();

                // duration <= 0 applies the target value directly - proves the helper
                // wires through to Transform.localRotation (and, via LerpQuaternion, a
                // real Quaternion.SlerpUnclamped) rather than testing the interpolation
                // curve itself, which needs a non-zero duration and a real PlayerLoop
                // tick that only runs in Play Mode (see class remarks).
                source.OnNext(Quaternion.Euler(0f, 180f, 0f));

                Assert.That(go.transform.localRotation,
                    Is.EqualTo(Quaternion.Euler(0f, 180f, 0f)).Using(QuaternionComparer));

                binding.Dispose();
                source.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TweenValue_Quaternion_DefaultLerp_UsesSlerp()
        {
            var source = new Subject<Quaternion>();
            Quaternion applied = default;

            UIBinder b = default;
            b.TweenValue(source, 0f, UIEaseType.Linear, v => applied = v);
            IDisposable binding = b.Build();

            var from = Quaternion.identity;
            var to = Quaternion.Euler(0f, 90f, 0f);
            source.OnNext(from);
            source.OnNext(to);

            // duration <= 0 applies `to` directly - this only proves DefaultLerp<Quaternion>
            // resolved to a real Quaternion lerp (SlerpUnclamped) instead of throwing
            // ArgumentException("no default lerp for this type"), which TweenValue's
            // escape hatch would do for any type it doesn't recognize.
            Assert.That(applied, Is.EqualTo(to).Using(QuaternionComparer));

            binding.Dispose();
            source.Dispose();
        }

        [Test]
        public void TweenFill_ReactiveProperty_Overload_SeedsFromValueNotFromTargetsCurrentFillAmount()
        {
            var go = new GameObject("FillTarget", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                Image image = go.AddComponent<Image>();
                image.fillAmount = 0.9f; // deliberately different from the property below

                // ReactiveProperty<float> is also an Observable<float>, so this call could
                // in principle bind through either TweenFill overload - overload
                // resolution must pick the ReactiveProperty<float> one (more specific
                // parameter type) and seed from its .Value (0.1), not from
                // image.fillAmount (0.9) the way the plain-Observable<float> overload's
                // readCurrent would.
                var property = new ReactiveProperty<float>(0.1f);

                UIBinder b = default;
                b.TweenFill(image, property, 0f, UIEaseType.Linear);
                IDisposable binding = b.Build();

                property.Value = 0.5f;

                // duration <= 0 applies `to` directly regardless of `from`, so this alone
                // doesn't distinguish the two overloads - the real proof is the seed
                // value itself, captured via a non-zero-duration tween below.
                Assert.That(image.fillAmount, Is.EqualTo(0.5f).Within(0.001f));

                binding.Dispose();
                property.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TweenFill_ReactiveProperty_Overload_FirstRetargetStartsFromPropertysSeededValue()
        {
            var go = new GameObject("FillTarget", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                Image image = go.AddComponent<Image>();
                image.fillAmount = 0.9f; // must NOT be the tween's `from` value

                var property = new ReactiveProperty<float>(0.1f); // must BE the tween's `from` value

                UIBinder b = default;
                // Non-zero duration: the frame-0 sample below reveals `from` (Lerp(from, to, t=0) == from),
                // which distinguishes "seeded from property.Value" (0.1) from "read from
                // image.fillAmount" (0.9) - the duration<=0 test above can't tell them apart.
                b.TweenFill(image, property, 1f, UIEaseType.Linear);
                IDisposable binding = b.Build();

                property.Value = 0.5f;

                Assert.That(image.fillAmount, Is.EqualTo(0.1f).Within(0.001f));

                binding.Dispose();
                property.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static readonly IEqualityComparer<Quaternion> QuaternionComparer =
            new QuaternionApproximatelyComparer();

        private sealed class QuaternionApproximatelyComparer : IEqualityComparer<Quaternion>
        {
            public bool Equals(Quaternion a, Quaternion b) => Quaternion.Angle(a, b) < 0.01f;
            public int GetHashCode(Quaternion q) => 0;
        }
    }
}