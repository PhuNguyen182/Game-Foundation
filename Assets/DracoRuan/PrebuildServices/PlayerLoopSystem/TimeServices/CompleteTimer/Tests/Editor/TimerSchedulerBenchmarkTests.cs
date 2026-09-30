using System;
using System.Collections.Generic;
using System.Diagnostics;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// 100,000-timer benchmark of the scheduler's hot paths.
    /// <para>
    /// Allocations are counted exactly with a <see cref="ProfilerRecorder"/> on "GC Allocated In Frame" (every GC.Alloc
    /// in the window, regardless of whether a collection runs meanwhile). <c>GC.GetAllocatedBytesForCurrentThread</c>
    /// is not used: it reports 0 under Unity's Mono. The Mono heap delta and the number of collections during each
    /// window are printed as a cross-check, and a control allocation of known size proves the recorder works. A
    /// separate phase reports the retained (post-GC) footprint of 100,000 live timers. Timings vary by machine and
    /// are only reported; allocation goals are asserted.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class TimerSchedulerBenchmarkTests
    {
        private const int TimerCount = 100_000;
        private const int IdleTicks = 1000;
        private const long FarFutureMs = 10_000_000;

        private static string _tag = "";

        /// <summary>
        /// "Zero allocation" means below this many bytes per operation once the recorder's own overhead is subtracted.
        /// The Editor adds a little background noise (about 0.02 B/op over 100,000 ops), while any real per-call
        /// allocation costs at least one 24-byte object, so 1 B/op separates the two cleanly.
        /// </summary>
        private const double NoiseBytesPerOp = 1.0;

        /// <summary>Bytes the profiler reports for an empty window (recorder/Editor bookkeeping); subtracted from every result.</summary>
        private static long? _windowOverheadBytes;

        private sealed class CountingListener : ITimerListener
        {
            public int Completed;

            public void OnTimerEvent(in TimerEvent e)
            {
                if (e.Type == TimerEventType.Completed)
                    this.Completed++;
            }
        }

        private readonly struct Result
        {
            public readonly double Ms;
            public readonly long AllocatedBytes;
            public readonly long HeapDeltaBytes;
            public readonly int Collections;
            public readonly int Operations;

            public double BytesPerOp => this.AllocatedBytes / (double)Math.Max(1, this.Operations);

            public Result(double ms, long allocatedBytes, long heapDeltaBytes, int collections, int operations)
            {
                this.Operations = operations;
                this.Ms = ms;
                this.AllocatedBytes = allocatedBytes;
                this.HeapDeltaBytes = heapDeltaBytes;
                this.Collections = collections;
            }
        }

        [Test]
        public void MemoryMeasurer_DetectsKnownAllocation()
        {
            _tag = "control";
            const int arrays = 1000;
            const int arraySize = 1000;
            object[] sink = new object[arrays];

            Result r = Measure("Control: 1,000 x new byte[1000]", arrays, () =>
            {
                for (int i = 0; i < arrays; i++)
                    sink[i] = new byte[arraySize];
            });

            GC.KeepAlive(sink);
            Assert.That(r.AllocatedBytes, Is.GreaterThanOrEqualTo(arrays * arraySize),
                "Measurer missed a known 1,000,000-byte allocation - memory numbers can't be trusted.");
        }

        [TestCase(100_000)]
        [TestCase(2_000)]
        public void Benchmark(int count)
        {
            _tag = $"n={count:N0}";

            string[] keys = new string[count];
            for (int i = 0; i < count; i++)
                keys[i] = "t" + i;

            TimerHandle[] handles = new TimerHandle[count];
            List<TimerEntrySnapshot> snapshot = new(count);
            FakeClock clock = new(0);
            CountingListener listener = new();

            // Retained footprint: live heap after a full GC, before vs after the scheduler holds 100k timers.
            long retainedBefore = RetainedBytes();

            TimerScheduler scheduler = null;
            Measure("Construct scheduler (capacity N)", 1, () =>
            {
                scheduler = new TimerScheduler(clock, initialCapacity: count, maxEventsPerTick: int.MaxValue)
                {
                    CompletedWarningThreshold = int.MaxValue
                };
                scheduler.AddChannelListener(0, listener);
            });

            // Start: durations are spread out so the heap does real sift work.
            Measure("TryStart xN (first use: creates the records)", count, () =>
            {
                for (int i = 0; i < count; i++)
                {
                    bool ok = scheduler.TryStart(new TimerSpec { Key = keys[i], DurationMs = FarFutureMs - i },
                        out handles[i]);
                    if (!ok)
                        Assert.Fail("TryStart failed at index " + i);
                }
            });

            long retainedAfter = RetainedBytes();
            long retained = retainedAfter - retainedBefore;
            Report($"Retained by N live timers (post-GC): {retained:N0} bytes = {retained / (double)count:F1} B/timer");

            Result lookups = Measure("TryGetHandle xN", count, () =>
            {
                for (int i = 0; i < count; i++)
                    scheduler.TryGetHandle(keys[i], out _);
            });

            // Idle ticks: nothing due, must be O(1) and allocation-free. Warm up, then keep the best of 3.
            for (int i = 0; i < 10; i++)
                scheduler.Tick(0);

            Result idleBest = default;
            for (int run = 0; run < 3; run++)
            {
                Result idle = Measure($"Idle Tick x{IdleTicks:N0} over N timers (run {run + 1})", IdleTicks, () =>
                {
                    for (int i = 0; i < IdleTicks; i++)
                        scheduler.Tick(0);
                });
                if (run == 0 || idle.AllocatedBytes < idleBest.AllocatedBytes)
                    idleBest = idle;
            }

            Result queries = Measure("GetRemainingMs xN", count, () =>
            {
                for (int i = 0; i < count; i++)
                    scheduler.GetRemainingMs(handles[i]);
            });

            // Snapshot into a pre-sized list: entries are structs sharing the immutable StageEnds arrays.
            Result capture = Measure("CaptureSnapshot (N entries)", count,
                () => scheduler.CaptureSnapshot(snapshot));
            Assert.That(snapshot.Count, Is.EqualTo(count));

            // Complete everything in one Tick.
            clock.Advance(FarFutureMs + 1);
            Result complete = Measure("Single Tick completing N timers", count, () => scheduler.Tick(0));
            Assert.That(scheduler.CompletedCount, Is.EqualTo(count));
            Assert.That(listener.Completed, Is.EqualTo(count));

            Result release = Measure("Release xN", count, () =>
            {
                for (int i = 0; i < count; i++)
                    scheduler.Release(handles[i]);
            });
            Assert.That(scheduler.CompletedCount, Is.EqualTo(0));

            GC.KeepAlive(scheduler);

            AssertNoPerCallAllocation(idleBest, "An idle Tick over N timers");
            AssertNoPerCallAllocation(queries, "GetRemainingMs");
            AssertNoPerCallAllocation(lookups, "TryGetHandle");
            AssertNoPerCallAllocation(capture, "CaptureSnapshot into a pre-sized list");
            AssertNoPerCallAllocation(complete, "Completing timers through a listener");
            AssertNoPerCallAllocation(release, "Release");
        }

        private static void AssertNoPerCallAllocation(Result result, string what)
        {
            Assert.That(result.BytesPerOp, Is.LessThan(NoiseBytesPerOp),
                $"{what} must not allocate ({result.AllocatedBytes:N0} B over {result.Operations:N0} ops).");
        }

        [Test]
        public void HundredThousandTimers_SharedDurations_RestartDoesNotAllocate()
        {
            const int distinctDurations = 100;
            _tag = "n=100,000 shared";

            string[] keys = new string[TimerCount];
            for (int i = 0; i < TimerCount; i++)
                keys[i] = "s" + i;

            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock, initialCapacity: TimerCount);
            TimerHandle[] handles = new TimerHandle[TimerCount];

            // First round creates the records and the shared duration arrays; cancelling frees every slot.
            for (int i = 0; i < TimerCount; i++)
                scheduler.TryStart(new TimerSpec { Key = keys[i], DurationMs = FarFutureMs + i % distinctDurations }, out handles[i]);

            Measure("Cancel x100,000", TimerCount, () =>
            {
                for (int i = 0; i < TimerCount; i++)
                    scheduler.Cancel(handles[i]);
            });

            // Second round reuses both the records and the shared arrays.
            Result restart = Measure($"TryStart x100,000 (reused slots, {distinctDurations} distinct durations)", TimerCount, () =>
            {
                for (int i = 0; i < TimerCount; i++)
                {
                    bool ok = scheduler.TryStart(
                        new TimerSpec { Key = keys[i], DurationMs = FarFutureMs + i % distinctDurations }, out handles[i]);
                    if (!ok)
                        Assert.Fail("TryStart failed at index " + i);
                }
            });

            GC.KeepAlive(scheduler);
            AssertNoPerCallAllocation(restart, "TryStart into reused slots with repeated single-stage durations");
        }

        /// <summary>Live bytes on the Mono heap after a full, finalizer-complete collection.</summary>
        private static long RetainedBytes()
        {
            CollectFully();
            return Profiler.GetMonoUsedSizeLong();
        }

        private static void CollectFully()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static Result Measure(string label, int operations, Action action)
        {
            _windowOverheadBytes ??= CalibrateWindowOverhead();

            Result raw = MeasureWindow(action, operations);
            Result result = new(raw.Ms, Math.Max(0, raw.AllocatedBytes - _windowOverheadBytes.Value), raw.HeapDeltaBytes,
                raw.Collections, operations);

            Report(
                $"{label}: {result.Ms:F3} ms, allocated {result.AllocatedBytes:N0} B " +
                $"({result.BytesPerOp:F2} B/op), " +
                $"mono heap {result.HeapDeltaBytes:+#,0;-#,0;0} B, gen0 GCs {result.Collections}");
            return result;
        }

        /// <summary>Smallest allocation reported over several empty windows: the measurement floor.</summary>
        private static long CalibrateWindowOverhead()
        {
            long floor = long.MaxValue;
            for (int i = 0; i < 10; i++)
                floor = Math.Min(floor, MeasureWindow(static () => { }, 1).AllocatedBytes);

            Report($"Measurement floor (empty window): {floor:N0} B");
            return floor;
        }

        private static Result MeasureWindow(Action action, int operations)
        {
            CollectFully();

            using ProfilerRecorder allocRecorder =
                ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            int collectionsBefore = GC.CollectionCount(0);
            long heapBefore = Profiler.GetMonoUsedSizeLong();
            long allocBefore = allocRecorder.CurrentValue;
            var stopwatch = Stopwatch.StartNew();

            action();

            stopwatch.Stop();
            long allocAfter = allocRecorder.CurrentValue;
            long heapAfter = Profiler.GetMonoUsedSizeLong();
            int collections = GC.CollectionCount(0) - collectionsBefore;

            return new Result(stopwatch.Elapsed.TotalMilliseconds, allocAfter - allocBefore, heapAfter - heapBefore,
                collections, operations);
        }

        private static void Report(string message)
        {
            string line = $"[Bench][{_tag}] {message}";
            TestContext.WriteLine(line);
            TestContext.Progress.WriteLine(line);
        }
    }
}
