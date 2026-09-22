using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.AudioSystem.Tests
{
    /// <summary>
    /// Protects the gate that decides whether a play request is allowed to take a voice.
    /// </summary>
    /// <remarks>
    /// The clock is passed in rather than read from <c>Time.time</c>, which is what makes this
    /// testable at all, and the per-entry timestamps live here rather than on the
    /// <c>AudioEntry</c> asset. Writing them to the asset would be the obvious shortcut and is a
    /// trap: runtime state on a ScriptableObject survives leaving play mode in the Editor, so the
    /// second play session of the day would behave differently from the first.
    /// </remarks>
    [TestFixture]
    public sealed class AudioFireRateGateTests
    {
        private const int Footstep = 0;
        private const int Alarm = 1;

        private AudioFireRateGate _gate;
        private List<int> _slotsToStop;

        [SetUp]
        public void SetUp()
        {
            this._gate = new AudioFireRateGate(entryCapacity: 4);
            this._slotsToStop = new List<int>();
        }

        private static AudioFireRateRule Rule(
            float minInterval = 0f,
            int maxConcurrent = 0,
            AudioConcurrencyPolicy policy = AudioConcurrencyPolicy.DropNewest) =>
            new AudioFireRateRule(minInterval, maxConcurrent, policy);

        private bool TryAcquire(int entry, float now, AudioFireRateRule rule, out AudioThrottleReason reason) =>
            this._gate.TryAcquire(entry, now, rule, this._slotsToStop, out reason);

        // ---- Fire rate ----

        [Test]
        public void TryAcquire_TheFirstEverPlay_IsAllowed()
        {
            bool allowed = this.TryAcquire(Footstep, now: 0f, Rule(minInterval: 0.1f), out AudioThrottleReason reason);

            Assert.That(allowed, Is.True);
            Assert.That(reason, Is.EqualTo(AudioThrottleReason.None));
        }

        [Test]
        public void TryAcquire_InsideTheInterval_IsBlocked()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 3);

            bool allowed = this.TryAcquire(Footstep, now: 0.05f, Rule(minInterval: 0.1f), out AudioThrottleReason reason);

            Assert.That(allowed, Is.False);
            Assert.That(reason, Is.EqualTo(AudioThrottleReason.FireRate));
        }

        [Test]
        public void TryAcquire_OnceTheIntervalHasElapsed_IsAllowedAgain()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 3);

            bool allowed = this.TryAcquire(Footstep, now: 0.1f, Rule(minInterval: 0.1f), out AudioThrottleReason reason);

            Assert.That(allowed, Is.True);
            Assert.That(reason, Is.EqualTo(AudioThrottleReason.None));
        }

        [Test]
        public void TryAcquire_WithNoInterval_NeverThrottles()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 3);

            Assert.That(this.TryAcquire(Footstep, 0f, Rule(minInterval: 0f), out _), Is.True);
        }

        [Test]
        public void TryAcquire_OneEntry_DoesNotThrottleAnother()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 3);

            bool allowed = this.TryAcquire(Alarm, now: 0.01f, Rule(minInterval: 5f), out _);

            Assert.That(allowed, Is.True);
        }

        // ---- Concurrency ----

        [Test]
        public void TryAcquire_BelowTheConcurrencyCap_IsAllowedAndStopsNothing()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 1);

            bool allowed = this.TryAcquire(Footstep, 1f, Rule(maxConcurrent: 2), out _);

            Assert.That(allowed, Is.True);
            Assert.That(this._slotsToStop, Is.Empty);
        }

        [Test]
        public void TryAcquire_AtTheCapWithDropNewest_IsBlocked()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 1);
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 2);

            bool allowed = this.TryAcquire(Footstep, 1f,
                Rule(maxConcurrent: 2, policy: AudioConcurrencyPolicy.DropNewest),
                out AudioThrottleReason reason);

            Assert.That(allowed, Is.False);
            Assert.That(reason, Is.EqualTo(AudioThrottleReason.ConcurrencyLimit));
            Assert.That(this._slotsToStop, Is.Empty);
        }

        [Test]
        public void TryAcquire_AtTheCapWithStealOldest_TakesTheSlotThatStartedFirst()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 7);
            this._gate.NotifyStarted(Footstep, now: 1f, slot: 4);

            bool allowed = this.TryAcquire(Footstep, 2f,
                Rule(maxConcurrent: 2, policy: AudioConcurrencyPolicy.StealOldest), out _);

            Assert.That(allowed, Is.True);
            Assert.That(this._slotsToStop, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void TryAcquire_BelowTheCapWithStealOldest_StopsNothing()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 7);

            this.TryAcquire(Footstep, 2f, Rule(maxConcurrent: 2, policy: AudioConcurrencyPolicy.StealOldest), out _);

            Assert.That(this._slotsToStop, Is.Empty);
        }

        [Test]
        public void TryAcquire_WithNoCap_NeverBlocksOnConcurrency()
        {
            for (int slot = 0; slot < 20; slot++)
                this._gate.NotifyStarted(Footstep, now: 0f, slot: slot);

            Assert.That(this.TryAcquire(Footstep, 1f, Rule(maxConcurrent: 0), out _), Is.True);
        }

        [Test]
        public void NotifyStopped_GivesTheCapSlotBack()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 1);
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 2);
            this._gate.NotifyStopped(Footstep, slot: 1);

            Assert.That(this.TryAcquire(Footstep, 1f, Rule(maxConcurrent: 2), out _), Is.True);
        }

        // ---- Restart ----

        [Test]
        public void TryAcquire_WithRestart_StopsEveryLiveInstanceOfThatEntry()
        {
            this._gate.NotifyStarted(Alarm, now: 0f, slot: 5);
            this._gate.NotifyStarted(Alarm, now: 1f, slot: 6);

            bool allowed = this.TryAcquire(Alarm, 2f,
                Rule(maxConcurrent: 4, policy: AudioConcurrencyPolicy.Restart), out _);

            Assert.That(allowed, Is.True);
            Assert.That(this._slotsToStop, Is.EquivalentTo(new[] { 5, 6 }));
        }

        [Test]
        public void TryAcquire_WithRestart_LeavesOtherEntriesAlone()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 9);
            this._gate.NotifyStarted(Alarm, now: 0f, slot: 5);

            this.TryAcquire(Alarm, 1f, Rule(policy: AudioConcurrencyPolicy.Restart), out _);

            Assert.That(this._slotsToStop, Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void TryAcquire_WithRestartAndNothingPlaying_StopsNothing()
        {
            bool allowed = this.TryAcquire(Alarm, 0f, Rule(policy: AudioConcurrencyPolicy.Restart), out _);

            Assert.That(allowed, Is.True);
            Assert.That(this._slotsToStop, Is.Empty);
        }

        // ---- Ordering between the two gates ----

        [Test]
        public void TryAcquire_WhenBothWouldBlock_ReportsFireRate_BecauseItIsCheckedFirst()
        {
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 1);
            this._gate.NotifyStarted(Footstep, now: 0f, slot: 2);

            this.TryAcquire(Footstep, 0.01f, Rule(minInterval: 1f, maxConcurrent: 2),
                out AudioThrottleReason reason);

            Assert.That(reason, Is.EqualTo(AudioThrottleReason.FireRate));
        }

        [Test]
        public void TryAcquire_WhenBlocked_LeavesTheStopListUntouched()
        {
            this._gate.NotifyStarted(Alarm, now: 0f, slot: 5);
            this._slotsToStop.Add(99);

            this.TryAcquire(Alarm, 0.01f, Rule(minInterval: 1f), out _);

            Assert.That(this._slotsToStop, Is.Empty, "The list is cleared on entry, so a stale slot cannot leak through.");
        }

        // ---- Capacity ----

        [Test]
        public void TryAcquire_ForAnEntryBeyondTheInitialCapacity_Grows()
        {
            const int transient = 50;

            this._gate.NotifyStarted(transient, now: 0f, slot: 1);

            Assert.That(this.TryAcquire(transient, 0.01f, Rule(minInterval: 1f), out AudioThrottleReason reason),
                Is.False);
            Assert.That(reason, Is.EqualTo(AudioThrottleReason.FireRate));
        }
    }
}
