using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>Covers <see cref="TimerScheduler.CaptureSnapshot"/>/<see cref="TimerScheduler.Restore"/> round-tripping (see REWRITE_PLAN.md 6.5, TimerSnapshotTests).</summary>
    [TestFixture]
    public sealed class TimerSnapshotTests
    {
        [Test]
        public void CaptureThenRestoreOnNewScheduler_PreservesRemainingStageAndState()
        {
            FakeClock clock1 = new(1_000_000);
            TimerScheduler scheduler1 = new(clock1);
            scheduler1.TryStart(
                new TimerSpec { Key = "crop", StageDurationsMs = new long[] { 1000, 2000, 3000 } },
                out TimerHandle handle1);

            clock1.Advance(1500);
            scheduler1.Tick(0);

            List<TimerEntrySnapshot> snapshots = new();
            scheduler1.CaptureSnapshot(snapshots);

            FakeClock clock2 = new(clock1.UtcNowMs);
            TimerScheduler scheduler2 = new(clock2);
            scheduler2.Restore(snapshots);

            scheduler2.TryGetHandle("crop", out TimerHandle handle2);

            Assert.That(scheduler2.GetState(handle2), Is.EqualTo(scheduler1.GetState(handle1)));
            Assert.That(scheduler2.GetCurrentStage(handle2), Is.EqualTo(scheduler1.GetCurrentStage(handle1)));
            Assert.That(scheduler2.GetRemainingMs(handle2), Is.EqualTo(scheduler1.GetRemainingMs(handle1)));
        }

        [Test]
        public void Restore_SkipsCorruptEntries_AndWarns()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);

            List<string> warnings = new();
            scheduler.OnWarning += warnings.Add;

            List<TimerEntrySnapshot> corrupt = new()
            {
                new TimerEntrySnapshot { Key = null, StageEnds = new long[] { 1000 } }, // Missing key.
                new TimerEntrySnapshot { Key = "bad-stages", StageEnds = null }, // Missing stages.
                new TimerEntrySnapshot { Key = "bad-stages2", StageEnds = new long[0] }, // Empty stages.
                new TimerEntrySnapshot { Key = "good", StageEnds = new long[] { 5000 }, State = TimerState.Running }
            };

            scheduler.Restore(corrupt);

            Assert.That(warnings.Count, Is.EqualTo(3));
            Assert.That(scheduler.TryGetHandle("good", out _), Is.True);
        }
    }
}
