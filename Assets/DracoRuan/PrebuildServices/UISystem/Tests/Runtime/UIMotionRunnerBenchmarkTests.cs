using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>
    /// Not a correctness test - a throwaway PlayMode benchmark for
    /// "how much does UIMotionRunner cost at 10,000 concurrent tweens", run on demand rather
    /// than as part of the regular suite. Must be PlayMode: UIMotionRunner only actually
    /// ticks through Unity's own PlayerLoop, which does not run in EditMode (every other
    /// EditMode test in this assembly uses duration &lt;= 0 to sidestep that entirely - this
    /// one specifically wants the real per-frame Tick cost, so it can't).
    /// Results are printed via Debug.Log so they show up in the Editor.log / test runner
    /// console; there is no pass/fail threshold, only Assert.Pass with the numbers in the
    /// message so the run is visible as a single line in the test report too.
    /// </summary>
    [TestFixture]
    public sealed class UIMotionRunnerBenchmarkTests
    {
        private const int TweenCount = 10_000;
        private const int WarmupFrames = 5;
        private const int MeasuredFrames = 120;

        [TearDown]
        public void TearDown() => UIMotionRunner.ReduceMotion = false;

        [UnityTest]
        public IEnumerator Benchmark_10000_PooledTweens_CreateRetargetAndPerFrameTickCost()
        {
            var handles = new UIMotionRunner.UIValueTweenHandle<float>[TweenCount];
            var targets = new float[TweenCount];

            var createStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < TweenCount; i++)
            {
                int captured = i;
                handles[i] = UIMotionRunner.CreateTween<float>(
                    1f, t => t, (a, b, t) => Mathf.LerpUnclamped(a, b, t), v => targets[captured] = v);
            }
            createStopwatch.Stop();

            var retargetStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < TweenCount; i++)
                handles[i].Retarget(0f, 100f);
            retargetStopwatch.Stop();

            // Let GC settle from setup before measuring steady-state per-frame tick cost.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long gcCountBefore = GC.CollectionCount(0);

            for (int i = 0; i < WarmupFrames; i++)
                yield return null;

            var tickStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < MeasuredFrames; i++)
                yield return null;
            tickStopwatch.Stop();

            long gcCountAfter = GC.CollectionCount(0);

            for (int i = 0; i < TweenCount; i++)
                handles[i].Dispose();

            double totalMs = tickStopwatch.Elapsed.TotalMilliseconds;
            double avgMsPerFrame = totalMs / MeasuredFrames;
            double avgUsPerTweenPerFrame = avgMsPerFrame * 1000.0 / TweenCount;

            var report = new StringBuilder();
            report.AppendLine("=== UIMotionRunner benchmark: 10,000 pooled tweens ===");
            report.AppendLine($"CreateTween x{TweenCount}: {createStopwatch.Elapsed.TotalMilliseconds:0.000} ms " +
                               $"({createStopwatch.Elapsed.TotalMilliseconds * 1000.0 / TweenCount:0.000} us/tween)");
            report.AppendLine($"Retarget x{TweenCount} (incl. frame-0 sync sample): " +
                               $"{retargetStopwatch.Elapsed.TotalMilliseconds:0.000} ms " +
                               $"({retargetStopwatch.Elapsed.TotalMilliseconds * 1000.0 / TweenCount:0.000} us/tween)");
            report.AppendLine($"Steady-state tick: {MeasuredFrames} frames, {totalMs:0.000} ms total, " +
                               $"{avgMsPerFrame:0.0000} ms/frame avg, {avgUsPerTweenPerFrame:0.000} us/tween/frame");
            report.AppendLine($"Gen0 GC collections during steady-state window: {gcCountAfter - gcCountBefore}");

            Debug.Log(report.ToString());
            Assert.Pass(report.ToString());
        }
    }
}
