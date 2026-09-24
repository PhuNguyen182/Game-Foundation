using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Regeneration;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>Lives scenario (5 max, 30-minute interval) from REWRITE_PLAN.md Q3/5.3, 6.5 RegenerationCounterTests.</summary>
    [TestFixture]
    public sealed class RegenerationCounterTests
    {
        private const long IntervalMs = 30 * 60_000;

        private FakeClock _clock;
        private TimerScheduler _scheduler;
        private RegenerationCounterRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            this._clock = new FakeClock(0);
            this._scheduler = new TimerScheduler(this._clock);
            this._registry = new RegenerationCounterRegistry(this._scheduler, this._clock);
        }

        [Test]
        public void ConsumeWhenFull_RefillsFirstUnitAfterInterval()
        {
            RegenerationCounter counter = this._registry.Create("lives", max: 5, IntervalMs);
            counter.TryConsume(5, this._clock.UtcNowMs);
            Assert.That(counter.GetCurrent(this._clock.UtcNowMs), Is.EqualTo(0));

            this._clock.Advance(IntervalMs);
            this._scheduler.Tick(0);

            Assert.That(counter.GetCurrent(this._clock.UtcNowMs), Is.EqualTo(1));
        }

        [Test]
        public void OneOfFive_Offline70Minutes_ThreeOfFive_NextRegenIn20Minutes()
        {
            RegenerationCounter counter = this._registry.Create("lives", max: 5, IntervalMs);
            counter.TryConsume(4, this._clock.UtcNowMs); // 5 -> 1

            this._clock.Advance(70 * 60_000);
            this._scheduler.Tick(0);

            Assert.That(counter.GetCurrent(this._clock.UtcNowMs), Is.EqualTo(3));
            Assert.That(counter.GetNextRegenRemainingMs(this._clock.UtcNowMs), Is.EqualTo(20 * 60_000));
        }

        [Test]
        public void Offline2Hours_SingleCallback_GainedFour()
        {
            RegenerationCounter counter = this._registry.Create("lives", max: 5, IntervalMs);
            counter.TryConsume(4, this._clock.UtcNowMs); // 5 -> 1

            List<(long gained, long current, long atMs)> calls = new();
            counter.OnRegenerated += (gained, current, atMs) => calls.Add((gained, current, atMs));

            this._clock.Advance(2 * 60 * 60_000); // 2 hours = 4 intervals of 30 min.
            this._scheduler.Tick(0);

            Assert.That(calls.Count, Is.EqualTo(1));
            Assert.That(calls[0].gained, Is.EqualTo(4));
            Assert.That(counter.GetCurrent(this._clock.UtcNowMs), Is.EqualTo(5));
        }

        [Test]
        public void ConsumeMidRegen_DoesNotResetInProgressInterval()
        {
            RegenerationCounter counter = this._registry.Create("lives", max: 5, IntervalMs);
            counter.TryConsume(5, this._clock.UtcNowMs); // 5 -> 0, anchor = now.

            this._clock.Advance(IntervalMs / 2); // Halfway through the first regen interval.

            // Consuming zero-available should fail, but if the player had e.g. 1 already and
            // consumes it, the in-progress interval toward the *next* unit must not reset.
            counter.Add(1, this._clock.UtcNowMs); // Grant 1 without affecting anchor rules improperly.
            long remainingBefore = counter.GetNextRegenRemainingMs(this._clock.UtcNowMs);

            counter.TryConsume(1, this._clock.UtcNowMs);
            long remainingAfter = counter.GetNextRegenRemainingMs(this._clock.UtcNowMs);

            Assert.That(remainingAfter, Is.EqualTo(remainingBefore),
                "Consuming while not full must not reset progress toward the next regen tick.");
        }

        [Test]
        public void FiveHundredMsLatePerTick_AcrossFiveTicks_FinalMomentStillAbsolutelyCorrect()
        {
            RegenerationCounter counter = this._registry.Create("lives", max: 5, IntervalMs);
            counter.TryConsume(5, this._clock.UtcNowMs); // 0/5, anchor = 0.

            long expectedAnchor = 0;
            for (int i = 0; i < 5; i++)
            {
                expectedAnchor += IntervalMs;
                this._clock.Advance(IntervalMs + 500); // Tick arrives 500ms late each time.
                this._scheduler.Tick(0);
            }

            // Regardless of dispatch lateness, folded intervals are computed from the absolute
            // anchor, so 5 full intervals means fully refilled with the anchor exactly caught up.
            Assert.That(counter.GetCurrent(this._clock.UtcNowMs), Is.EqualTo(5));
        }

        [Test]
        public void Refill_FillsImmediately()
        {
            RegenerationCounter counter = this._registry.Create("lives", max: 5, IntervalMs);
            counter.TryConsume(3, this._clock.UtcNowMs);

            counter.Refill(this._clock.UtcNowMs);

            Assert.That(counter.GetCurrent(this._clock.UtcNowMs), Is.EqualTo(5));
        }

        [Test]
        public void AddBeyondMax_AllowOverMax_ExceedsMaxAndStopsRegen()
        {
            RegenerationCounter counter = this._registry.Create("lives", max: 5, IntervalMs);

            counter.Add(3, this._clock.UtcNowMs, allowOverMax: true);

            Assert.That(counter.GetCurrent(this._clock.UtcNowMs), Is.EqualTo(8));
            Assert.That(counter.GetNextRegenRemainingMs(this._clock.UtcNowMs), Is.EqualTo(0));
        }

        [Test]
        public void CaptureAndRestore_PreservesStoredMaxIntervalAnchor()
        {
            RegenerationCounter counter = this._registry.Create("lives", max: 5, IntervalMs);
            counter.TryConsume(2, this._clock.UtcNowMs);
            this._clock.Advance(1000);

            List<RegenerationSnapshot> snapshots = new();
            this._registry.Capture(snapshots);

            FakeClock clock2 = new(this._clock.UtcNowMs);
            TimerScheduler scheduler2 = new(clock2);
            RegenerationCounterRegistry registry2 = new(scheduler2, clock2);
            registry2.Create("lives", max: 5, IntervalMs); // Pre-registered, same as every session.
            registry2.Restore(snapshots);

            registry2.TryGet("lives", out RegenerationCounter restored);

            Assert.That(restored.Stored, Is.EqualTo(counter.Stored));
            Assert.That(restored.Max, Is.EqualTo(counter.Max));
            Assert.That(restored.IntervalMs, Is.EqualTo(counter.IntervalMs));
            Assert.That(restored.AnchorMs, Is.EqualTo(counter.AnchorMs));
        }
    }
}
