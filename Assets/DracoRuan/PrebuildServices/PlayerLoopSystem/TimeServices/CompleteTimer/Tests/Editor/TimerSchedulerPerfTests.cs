using System;
using System.Diagnostics;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// Confirms the min-heap design meets its O(1)-idle-frame, zero-allocation goal at scale (see
    /// REWRITE_PLAN.md 1.3/4, 6.5 TimerSchedulerPerfTests).
    /// </summary>
    [TestFixture]
    public sealed class TimerSchedulerPerfTests
    {
        [Test]
        public void TenThousandTimers_ThousandIdleTicks_ZeroAllocation()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock, initialCapacity: 10_000);

            for (int i = 0; i < 10_000; i++)
            {
                // Far-future deadlines so none of them are ever due during the idle ticks below.
                scheduler.TryStart(new TimerSpec { Key = "t" + i, DurationMs = 10_000_000 + i }, out _);
            }

            // Warm up JIT and any one-time allocations (dictionary resizing, etc.) before measuring.
            for (int i = 0; i < 10; i++)
                scheduler.Tick(0);

            long before = GC.GetAllocatedBytesForCurrentThread();

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
                scheduler.Tick(0);
            stopwatch.Stop();

            long after = GC.GetAllocatedBytesForCurrentThread();
            long allocatedBytes = after - before;

            TestContext.WriteLine($"10,000 timers x 1,000 idle ticks: {stopwatch.Elapsed.TotalMilliseconds:F3} ms total, allocated {allocatedBytes} bytes.");

            Assert.That(allocatedBytes, Is.EqualTo(0), "An idle Tick over 10,000 timers must not allocate.");
        }
    }
}
