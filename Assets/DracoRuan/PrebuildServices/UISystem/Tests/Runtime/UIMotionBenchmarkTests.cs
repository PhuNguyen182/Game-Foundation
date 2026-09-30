using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using UIMotion = DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion.UIMotion;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>
    /// Not a correctness test - a throwaway PlayMode benchmark for "how much does
    /// UIMotionRunner.Play cost with 10,000 concurrent UIMotion timelines", run on demand
    /// rather than as part of the regular suite. Must be PlayMode: UIMotionRunner only
    /// actually ticks through Unity's own PlayerLoop (see UIMotionRunnerBenchmarkTests for
    /// the same reasoning applied to the pooled value-tween API).
    /// Each UIMotion gets one real Fade track (CanvasGroup.alpha, useStartValue 0 -> 1,
    /// 1s duration) - the same track shape UIMotionTests.cs already exercises for
    /// correctness - so this measures the full Play() pipeline (schedule/rest-pose/track
    /// evaluator), not just the simpler value-tween path.
    /// </summary>
    [TestFixture]
    public sealed class UIMotionBenchmarkTests
    {
        private const int MotionCount = 10_000;
        private const int WarmupFrames = 5;
        private const int MeasuredFrames = 120;

        [TearDown]
        public void TearDown() => UIMotionRunner.ReduceMotion = false;

        [UnityTest]
        public IEnumerator Benchmark_10000_UIMotions_CreateAndPlayShowAndPerFrameTickCost()
        {
            var roots = new List<GameObject>(MotionCount);
            var motions = new UIMotion[MotionCount];
            var canvasGroups = new CanvasGroup[MotionCount];

            var createStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < MotionCount; i++)
            {
                var go = new GameObject("BenchMotion");
                roots.Add(go);

                CanvasGroup cg = go.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                canvasGroups[i] = cg;

                UIMotion motion = go.AddComponent<UIMotion>();
                var show = new UIMotionTrack
                {
                    kind = UIMotionTrackKind.Fade,
                    target = cg,
                    useStartValue = true,
                    from = new Vector4(0f, 0f, 0f, 0f),
                    to = new Vector4(1f, 0f, 0f, 0f),
                    duration = 1f,
                };
                motion.ConfigureForTest(new[] { show }, new UIMotionTrack[0], motionTrigger: UIMotionTrigger.Manual);
                motions[i] = motion;
            }

            createStopwatch.Stop();

            var playStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < MotionCount; i++)
                motions[i].PlayShowAsync().Forget();
            playStopwatch.Stop();

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

            int completedAtHalfSecond = 0;
            for (int i = 0; i < MotionCount; i++)
            {
                if (canvasGroups[i].alpha >= 0.999f)
                    completedAtHalfSecond++;
            }

            for (int i = 0; i < MotionCount; i++)
                UnityEngine.Object.DestroyImmediate(roots[i]);

            double totalMs = tickStopwatch.Elapsed.TotalMilliseconds;
            double avgMsPerFrame = totalMs / MeasuredFrames;
            double avgUsPerMotionPerFrame = avgMsPerFrame * 1000.0 / MotionCount;

            var report = new StringBuilder();
            report.AppendLine("=== UIMotion benchmark: 10,000 concurrent Fade timelines ===");
            report.AppendLine($"Create (GameObject+CanvasGroup+UIMotion+ConfigureForTest) x{MotionCount}: " +
                              $"{createStopwatch.Elapsed.TotalMilliseconds:0.000} ms " +
                              $"({createStopwatch.Elapsed.TotalMilliseconds * 1000.0 / MotionCount:0.000} us/motion)");
            report.AppendLine($"PlayShowAsync x{MotionCount} (incl. frame-0 sync sample): " +
                              $"{playStopwatch.Elapsed.TotalMilliseconds:0.000} ms " +
                              $"({playStopwatch.Elapsed.TotalMilliseconds * 1000.0 / MotionCount:0.000} us/motion)");
            report.AppendLine($"Steady-state tick: {MeasuredFrames} frames, {totalMs:0.000} ms total, " +
                              $"{avgMsPerFrame:0.0000} ms/frame avg, {avgUsPerMotionPerFrame:0.000} us/motion/frame");
            report.AppendLine($"Gen0 GC collections during steady-state window: {gcCountAfter - gcCountBefore}");
            report.AppendLine($"Fully faded-in (alpha>=0.999) after warmup+{MeasuredFrames} frames: " +
                              $"{completedAtHalfSecond}/{MotionCount}");

            Debug.Log(report.ToString());
            Assert.Pass(report.ToString());
        }
    }
}