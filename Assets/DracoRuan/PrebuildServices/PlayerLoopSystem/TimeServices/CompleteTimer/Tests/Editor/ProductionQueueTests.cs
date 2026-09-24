using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Production;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>Covers <see cref="ProductionQueue"/> behavior (see REWRITE_PLAN.md 6.5, ProductionQueueTests).</summary>
    [TestFixture]
    public sealed class ProductionQueueTests
    {
        private FakeClock _clock;
        private TimerScheduler _scheduler;
        private ProductionQueue _queue;

        [SetUp]
        public void SetUp()
        {
            this._clock = new FakeClock(0);
            this._scheduler = new TimerScheduler(this._clock);
            this._queue = new ProductionQueue(this._scheduler, "factory", capacity: 5, channel: 1);
        }

        [Test]
        public void ThreeItems_Offline2AndHalfDurations_TwoOutputsAtCorrectMoments_ThirdHalfway()
        {
            this._queue.TryEnqueue("item1", 1000);
            this._queue.TryEnqueue("item2", 1000);
            this._queue.TryEnqueue("item3", 1000);

            this._clock.Advance(2500);
            this._scheduler.Tick(0);

            List<ProductionQueueOutput> outputs = new();
            this._queue.ClaimOutputs(outputs);

            Assert.That(outputs.Count, Is.EqualTo(2));
            Assert.That(outputs[0].ItemId, Is.EqualTo("item1"));
            Assert.That(outputs[0].AtMs, Is.EqualTo(1000));
            Assert.That(outputs[1].ItemId, Is.EqualTo("item2"));
            Assert.That(outputs[1].AtMs, Is.EqualTo(2000));

            Assert.That(this._queue.Items.Count, Is.EqualTo(1));
            Assert.That(this._queue.Items[0].ItemId, Is.EqualTo("item3"));
            Assert.That(this._scheduler.GetProgress01(this._queue.ActiveHandle), Is.EqualTo(0.5).Within(0.0001));
        }

        [Test]
        public void Pause_FreezesActiveItem_QueueStaysIdleUntilResume()
        {
            this._queue.TryEnqueue("item1", 1000);
            this._clock.Advance(300);
            this._scheduler.Tick(0);

            this._queue.Pause();
            this._clock.Advance(5000);
            this._scheduler.Tick(0);

            List<ProductionQueueOutput> outputs = new();
            this._queue.ClaimOutputs(outputs);
            Assert.That(outputs.Count, Is.EqualTo(0), "Paused queue must not produce output while offline.");

            this._queue.Resume();
            this._clock.Advance(700);
            this._scheduler.Tick(0);

            this._queue.ClaimOutputs(outputs);
            Assert.That(outputs.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemoveActiveItem_StartsNextItemAtNow()
        {
            this._queue.TryEnqueue("item1", 1000);
            this._queue.TryEnqueue("item2", 2000);

            this._clock.Advance(400);
            this._scheduler.Tick(0);

            this._queue.RemoveAt(0);

            Assert.That(this._queue.Items.Count, Is.EqualTo(1));
            Assert.That(this._queue.Items[0].ItemId, Is.EqualTo("item2"));
            Assert.That(this._scheduler.GetEndUtcMs(this._queue.ActiveHandle), Is.EqualTo(400 + 2000));
        }

        [Test]
        public void RestoreReattachesListenerToActiveTimer()
        {
            // Build the queue through a registry, exactly as the integration layer does, so
            // Capture()/Restore() are exercised through their real public entry points.
            ProductionQueueRegistry registry = new(this._scheduler);
            ProductionQueue queue = registry.Create("factory", capacity: 5, channel: 1);
            queue.TryEnqueue("item1", 1000);
            queue.TryEnqueue("item2", 1000);

            List<ProductionQueueSnapshot> queueSnapshots = new();
            registry.Capture(queueSnapshots);

            List<TimerEntrySnapshot> timerSnapshots = new();
            this._scheduler.CaptureSnapshot(timerSnapshots);

            // Simulate a fresh session: new scheduler + registry + queue, restored from what was captured.
            FakeClock clock2 = new(0);
            TimerScheduler scheduler2 = new(clock2);
            scheduler2.Restore(timerSnapshots);

            ProductionQueueRegistry registry2 = new(scheduler2);
            registry2.Create("factory", capacity: 5, channel: 1); // Gameplay re-creates the queue up front, same as every session.
            registry2.Restore(queueSnapshots);

            clock2.Advance(1000);
            scheduler2.Tick(0);

            registry2.TryGet("factory", out ProductionQueue restoredQueue);
            List<ProductionQueueOutput> outputs = new();
            restoredQueue.ClaimOutputs(outputs);

            Assert.That(outputs.Count, Is.EqualTo(1));
            Assert.That(outputs[0].ItemId, Is.EqualTo("item1"));
        }
    }
}
