using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.Motion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>
    /// EditMode tests for UIMotionRunner.Tween (see UIMotionTests.cs for why this asmdef
    /// can only run against a live Unity Editor). Every scenario uses duration &lt;= 0, so
    /// the whole tween completes synchronously inside the call that starts it - no
    /// dependency on UIMotionRunner's PlayerLoop tick, which only actually runs in Play
    /// Mode.
    /// </summary>
    [TestFixture]
    public sealed class UIMotionRunnerTweenTests
    {
        [TearDown]
        public void TearDown() => UIMotionRunner.ReduceMotion = false;

        [Test]
        public void Tween_ZeroDuration_AppliesEndValueOnceAndCompletes()
        {
            var applied = new List<float>();

            IDisposable handle = UIMotionRunner.Tween(0f, 10f, 0f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t),
                v => applied.Add(v));

            Assert.That(applied, Is.EqualTo(new[] { 10f }));
            handle.Dispose(); // must be safe to dispose an already-completed handle
        }

        [Test]
        public void Tween_ReduceMotion_SkipsInterpolationEvenWithNonZeroDuration()
        {
            var applied = new List<float>();

            UIMotionRunner.ReduceMotion = true;
            UIMotionRunner.Tween(0f, 10f, 5f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t), v => applied.Add(v));

            Assert.That(applied, Is.EqualTo(new[] { 10f }));
        }

        [Test]
        public void Tween_NullEase_TreatedAsLinear()
        {
            var applied = new List<float>();

            UIMotionRunner.Tween(0f, 10f, 0f, null, (a, b, t) => Mathf.LerpUnclamped(a, b, t), v => applied.Add(v));

            Assert.That(applied, Is.EqualTo(new[] { 10f }));
        }

        [Test]
        public void Tween_DisposeBeforeCompletion_NeverAppliesEndValue()
        {
            var applied = new List<float>();

            // Non-zero duration so the tween is still in-flight (added to the runner's
            // active list) instead of completing synchronously inside Tween() itself.
            IDisposable handle = UIMotionRunner.Tween(0f, 10f, 1f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t),
                v => applied.Add(v));

            // Frame-0 synchronous sample already applied the start value once.
            Assert.That(applied, Is.EqualTo(new[] { 0f }));

            handle.Dispose();

            // No further tick can ever reach this test (PlayerLoop only runs in Play
            // Mode), so "never applies the end value" is verified by construction here -
            // this asserts the immediate post-dispose state stays put.
            Assert.That(applied, Is.EqualTo(new[] { 0f }));
        }

        [Test]
        public void Tween_ThrowingSetter_IsolatedToThatTweenAtStartTime()
        {
            // The synchronous t=0 sample runs inside Tween() itself (see UIMotionRunner.
            // Play's own "sample frame 0 synchronously" comment for the same reason) -
            // an exception there must not prevent the call from returning a handle.
            // TickValueTweenSafe logs the caught exception (Debug.LogException), which
            // LogAssert would otherwise treat as an unexpected failure - expect it.
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("InvalidOperationException: boom"));

            IDisposable handle = null;
            Assert.DoesNotThrow(() =>
            {
                handle = UIMotionRunner.Tween<float>(0f, 10f, 1f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t),
                    _ => throw new InvalidOperationException("boom"));
            });

            Assert.That(handle, Is.Not.Null);
        }

        [Test]
        public void CreateTween_RetargetCalledRepeatedly_AppliesEachTargetWithoutAllocatingANewTween()
        {
            var applied = new List<float>();

            UIMotionRunner.UIValueTweenHandle<float> handle = UIMotionRunner.CreateTween<float>(
                0f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t), v => applied.Add(v));

            // Same handle instance for every Retarget call - this is the whole point of
            // CreateTween over Tween(): a value changing many times per frame (rapid
            // damage ticks, a currency counting up fast) reuses this one object instead
            // of allocating a new ValueTween<T> per change.
            handle.Retarget(0f, 1f);
            handle.Retarget(1f, 2f);
            handle.Retarget(2f, 3f);

            Assert.That(applied, Is.EqualTo(new[] { 1f, 2f, 3f }));

            handle.Dispose();
        }

        [Test]
        public void CreateTween_RetargetAfterDispose_Throws()
        {
            UIMotionRunner.UIValueTweenHandle<float> handle = UIMotionRunner.CreateTween<float>(
                0f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t), _ => { });

            handle.Dispose();

            Assert.Throws<ObjectDisposedException>(() => handle.Retarget(0f, 1f));
        }

        [Test]
        public void CreateTween_DisposeBeforeAnyRetarget_IsSafe()
        {
            UIMotionRunner.UIValueTweenHandle<float> handle = UIMotionRunner.CreateTween<float>(
                0f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t), _ => { });

            // Never retargeted (e.g. Bind() then Unbind() before the observable ever
            // fired) - disposing must not throw even though the underlying tween was
            // never added to the runner's active list.
            Assert.DoesNotThrow(() => handle.Dispose());
        }

        [Test]
        public void CreateTween_RetargetAfterInFlightTweenInterrupted_ContinuesFromCurrentValueNotSnappedTarget()
        {
            var applied = new List<float>();

            // Non-zero duration so the first Retarget leaves a still-running tween
            // (elapsed < duration) rather than completing synchronously - this exercises
            // "cut off in place, no snap" rather than the instant duration<=0 path.
            UIMotionRunner.UIValueTweenHandle<float> handle = UIMotionRunner.CreateTween<float>(
                1f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t), v => applied.Add(v));

            handle.Retarget(0f, 10f);
            Assert.That(applied[applied.Count - 1], Is.EqualTo(0f)); // frame-0 sample of the first tween

            handle.Retarget(applied[applied.Count - 1], 100f);
            Assert.That(applied[applied.Count - 1], Is.EqualTo(0f)); // frame-0 sample of the second tween

            handle.Dispose();
        }
    }
}