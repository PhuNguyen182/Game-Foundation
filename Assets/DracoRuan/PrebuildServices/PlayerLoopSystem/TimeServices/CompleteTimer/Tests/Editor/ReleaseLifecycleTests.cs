using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Production;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Regeneration;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// Covers the Running -> Completed -> Release -> Free slot lifecycle (see REWRITE_PLAN.md Q4,
    /// 6.5 ReleaseLifecycleTests).
    /// </summary>
    [TestFixture]
    public sealed class ReleaseLifecycleTests
    {
        [Test]
        public void Release_RemovesFromSnapshot_KeyReusable_OldHandleInvalid_SlotReused()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 1000 }, out TimerHandle h1);

            clock.Advance(1000);
            scheduler.Tick(0);
            scheduler.Release(h1);

            List<TimerEntrySnapshot> snapshots = new();
            scheduler.CaptureSnapshot(snapshots);
            Assert.That(snapshots.Count, Is.EqualTo(0));

            bool started = scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 2000 }, out TimerHandle h2);
            Assert.That(started, Is.True);
            Assert.That(h2.Index, Is.EqualTo(h1.Index), "Freed slot should be reused.");
            Assert.That(h2.Version, Is.Not.EqualTo(h1.Version));

            Assert.That(scheduler.GetState(h1), Is.EqualTo(TimerState.Free), "Old handle must not resolve to the new timer.");
            Assert.That(scheduler.GetState(h2), Is.EqualTo(TimerState.Running));
        }

        [Test]
        public void AutoRelease_WithListener_ReleasesAfterEventDelivered()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 1000, AutoRelease = true }, out TimerHandle h);

            TestListener listener = new();
            scheduler.SetListener(h, listener);

            clock.Advance(1000);
            scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(1));
            Assert.That(scheduler.GetState(h), Is.EqualTo(TimerState.Free), "AutoRelease must free the slot once delivered.");
        }

        [Test]
        public void AutoRelease_NoListenerYet_StaysCompletedInSnapshot_ThenReleasesAfterReplayDelivered()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 1000, AutoRelease = true }, out TimerHandle h);

            clock.Advance(1000);
            scheduler.Tick(0); // Completed, but no listener - must NOT auto-release yet.

            List<TimerEntrySnapshot> snapshots = new();
            scheduler.CaptureSnapshot(snapshots);
            Assert.That(snapshots.Count, Is.EqualTo(1), "Undelivered AutoRelease timer must still be persisted.");
            Assert.That(snapshots[0].State, Is.EqualTo(TimerState.Completed));

            TestListener listener = new();
            scheduler.AddChannelListener(0, listener);

            Assert.That(listener.Events.Count, Is.EqualTo(1));
            Assert.That(listener.Events[0].IsReplay, Is.True);
            Assert.That(scheduler.GetState(h), Is.EqualTo(TimerState.Free), "Once the replay is delivered, AutoRelease should free the slot.");
        }

        [Test]
        public void ProductionQueue_NeverLeavesCompletedTimerInSnapshot()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            ProductionQueue queue = new(scheduler, "factory", capacity: 3, channel: 5);

            queue.TryEnqueue("item1", 1000);
            clock.Advance(1000);
            scheduler.Tick(0);

            List<TimerEntrySnapshot> snapshots = new();
            scheduler.CaptureSnapshot(snapshots);

            Assert.That(snapshots.Count, Is.EqualTo(0), "ProductionQueue must release its timer the moment the item completes.");
        }

        [Test]
        public void RegenerationCounter_NeverLeavesCompletedTimerInSnapshot()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            RegenerationCounterRegistry registry = new(scheduler, clock);
            RegenerationCounter counter = registry.Create("lives", max: 5, intervalMs: 1000);
            counter.TryConsume(5, clock.UtcNowMs);

            clock.Advance(1000);
            scheduler.Tick(0);

            List<TimerEntrySnapshot> snapshots = new();
            scheduler.CaptureSnapshot(snapshots);

            Assert.That(snapshots.Count, Is.EqualTo(0), "RegenerationCounter must release its timer after each fire.");
        }

        [Test]
        public void ExceedingCompletedWarningThreshold_RaisesWarning()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock) { CompletedWarningThreshold = 3 };

            List<string> warnings = new();
            scheduler.OnWarning += warnings.Add;

            for (int i = 0; i < 5; i++)
                scheduler.TryStart(new TimerSpec { Key = "t" + i, DurationMs = 1000 }, out _);

            clock.Advance(1000);
            scheduler.Tick(0); // 5 Completed, none released, none delivered (no listener) -> over threshold.

            Assert.That(warnings.Count, Is.GreaterThan(0));
        }
    }
}
