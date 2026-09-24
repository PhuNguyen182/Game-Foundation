using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// Confirms remaining time always matches real elapsed wall-clock time, independent of when or
    /// how often the app was running in between (see REWRITE_PLAN.md Q1/5.1, 6.5 RealTimeElapsedTests).
    /// </summary>
    [TestFixture]
    public sealed class RealTimeElapsedTests
    {
        [Test]
        public void OneHourTimer_Offline25Minutes_ThirtyFiveMinutesRemaining_ExactlyZeroDrift()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 60 * 60_000 }, out TimerHandle h);

            clock.Advance(25 * 60_000);
            scheduler.Tick(0);

            Assert.That(scheduler.GetRemainingMs(h), Is.EqualTo(35 * 60_000));
        }

        [Test]
        public void SavingAtMinute1OrMinute20_ProducesIdenticalRestoredRemaining()
        {
            // Two independent sessions of the same 1-hour timer, captured at different in-session
            // moments, must agree on remaining time once both are restored to the same "now".
            FakeClock clockA = new(0);
            TimerScheduler schedulerA = new(clockA);
            schedulerA.TryStart(new TimerSpec { Key = "t", DurationMs = 60 * 60_000 }, out TimerHandle ha);
            clockA.Advance(1 * 60_000);
            schedulerA.Tick(0);
            System.Collections.Generic.List<TimerEntrySnapshot> snapA = new();
            schedulerA.CaptureSnapshot(snapA);

            FakeClock clockB = new(0);
            TimerScheduler schedulerB = new(clockB);
            schedulerB.TryStart(new TimerSpec { Key = "t", DurationMs = 60 * 60_000 }, out TimerHandle hb);
            clockB.Advance(20 * 60_000);
            schedulerB.Tick(0);
            System.Collections.Generic.List<TimerEntrySnapshot> snapB = new();
            schedulerB.CaptureSnapshot(snapB);

            // Restore both into fresh schedulers set to the same absolute "now".
            const long restoredNow = 60 * 60_000; // 1 hour after StartMs = 0.
            FakeClock restoreClockA = new(restoredNow);
            TimerScheduler restoredA = new(restoreClockA);
            restoredA.Restore(snapA);

            FakeClock restoreClockB = new(restoredNow);
            TimerScheduler restoredB = new(restoreClockB);
            restoredB.Restore(snapB);

            restoredA.TryGetHandle("t", out TimerHandle rha);
            restoredB.TryGetHandle("t", out TimerHandle rhb);

            Assert.That(restoredA.GetRemainingMs(rha), Is.EqualTo(restoredB.GetRemainingMs(rhb)));
        }

        [Test]
        public void Offline2Hours_OneHourTimer_CompletedAtExactStartPlusDuration_IsLate()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 60 * 60_000 }, out TimerHandle h);

            TestListener listener = new();
            scheduler.SetListener(h, listener);

            clock.Advance(2 * 60 * 60_000);
            scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(1));
            Assert.That(listener.Events[0].Type, Is.EqualTo(TimerEventType.Completed));
            Assert.That(listener.Events[0].AtMs, Is.EqualTo(60 * 60_000));
            Assert.That(listener.Events[0].IsLate, Is.True);
        }

        [Test]
        public void PauseAtMinute10_Offline1Hour_FiftyMinutesRemaining()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 60 * 60_000 }, out TimerHandle h);

            clock.Advance(10 * 60_000);
            scheduler.Tick(0);
            scheduler.Pause(h);

            clock.Advance(60 * 60_000);

            Assert.That(scheduler.GetRemainingMs(h), Is.EqualTo(50 * 60_000));
        }

        [Test]
        public void ThreeStages_OfflinePastTwoBoundaries_TwoStageChangedAtCorrectMoments()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(
                new TimerSpec { Key = "t", StageDurationsMs = new long[] { 1000, 1000, 1000 } },
                out TimerHandle h);

            TestListener listener = new();
            scheduler.SetListener(h, listener);

            clock.Advance(2500);
            scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(2));
            Assert.That(listener.Events[0].AtMs, Is.EqualTo(1000));
            Assert.That(listener.Events[1].AtMs, Is.EqualTo(2000));
        }

        [Test]
        public void DeltaTimeArgument_NeverAffectsComputedRemaining()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 10_000 }, out TimerHandle h);

            clock.Advance(3000);
            scheduler.Tick(999f); // Absurd deltaTime; must be ignored entirely.

            Assert.That(scheduler.GetRemainingMs(h), Is.EqualTo(7000));
        }
    }
}
