using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// At-least-once delivery guarantees for Completed/StageChanged events (see REWRITE_PLAN.md Q2/5.2,
    /// 6.5 DeliveryGuaranteeTests).
    /// </summary>
    [TestFixture]
    public sealed class DeliveryGuaranteeTests
    {
        [Test]
        public void TickWithNoListener_ThenAddChannelListener_ReceivesExactlyOneCompleted()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(new TimerSpec { Key = "t", DurationMs = 1000 }, out _);

            clock.Advance(1000);
            scheduler.Tick(0); // No listener attached yet: event goes to the undelivered buffer.

            TestListener listener = new();
            scheduler.AddChannelListener(0, listener);

            Assert.That(listener.Events.Count, Is.EqualTo(1));
            Assert.That(listener.Events[0].Type, Is.EqualTo(TimerEventType.Completed));
            Assert.That(listener.Events[0].IsReplay, Is.True);
        }

        [Test]
        public void StagesSkippedWhileOffline_LateListenerRegistration_ReceivesAllInOrder()
        {
            FakeClock clock = new(0);
            TimerScheduler scheduler = new(clock);
            scheduler.TryStart(
                new TimerSpec { Key = "t", StageDurationsMs = new long[] { 1000, 1000, 1000 } },
                out _);

            clock.Advance(3500);
            scheduler.Tick(0); // Nobody listening: 3 events (2 stage changes + Completed) buffered.

            TestListener listener = new();
            scheduler.AddChannelListener(0, listener);

            Assert.That(listener.Events.Count, Is.EqualTo(3));
            Assert.That(listener.Events[0].Stage, Is.EqualTo(1));
            Assert.That(listener.Events[1].Stage, Is.EqualTo(2));
            Assert.That(listener.Events[2].Type, Is.EqualTo(TimerEventType.Completed));
        }

        [Test]
        public void CompletedNotReleased_AfterRestore_ReplaysAsIsReplay()
        {
            FakeClock clock1 = new(0);
            TimerScheduler scheduler1 = new(clock1);
            scheduler1.TryStart(new TimerSpec { Key = "t", DurationMs = 1000 }, out _);

            clock1.Advance(1000);
            scheduler1.Tick(0); // Completed, but never delivered (no listener) and never released.

            List<TimerEntrySnapshot> snapshots = new();
            scheduler1.CaptureSnapshot(snapshots);

            FakeClock clock2 = new(1000);
            TimerScheduler scheduler2 = new(clock2);
            TestListener listener = new();
            scheduler2.Restore(snapshots);
            scheduler2.AddChannelListener(0, listener);

            Assert.That(listener.Events.Count, Is.EqualTo(1));
            Assert.That(listener.Events[0].IsReplay, Is.True);
            Assert.That(listener.Events[0].Type, Is.EqualTo(TimerEventType.Completed));
        }

        [Test]
        public void CompletedAlreadyReleased_AfterRestore_DoesNotReplay()
        {
            FakeClock clock1 = new(0);
            TimerScheduler scheduler1 = new(clock1);
            scheduler1.TryStart(new TimerSpec { Key = "t", DurationMs = 1000 }, out TimerHandle h);

            TestListener listener1 = new();
            scheduler1.SetListener(h, listener1); // Listener present, so Completed is delivered live.

            clock1.Advance(1000);
            scheduler1.Tick(0);
            scheduler1.Release(h);

            List<TimerEntrySnapshot> snapshots = new();
            scheduler1.CaptureSnapshot(snapshots);

            Assert.That(snapshots.Count, Is.EqualTo(0), "Released timer must not appear in the snapshot at all.");

            FakeClock clock2 = new(1000);
            TimerScheduler scheduler2 = new(clock2);
            TestListener listener2 = new();
            scheduler2.Restore(snapshots);
            scheduler2.AddChannelListener(0, listener2);

            Assert.That(listener2.Events.Count, Is.EqualTo(0));
        }
    }
}
