using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// Covers <see cref="TimerClock"/>'s anchoring, clock-rollback protection, server sync and
    /// deep-sleep drift handling (see REWRITE_PLAN.md 6.5, TimerClockTests).
    /// </summary>
    [TestFixture]
    public sealed class TimerClockTests
    {
        private long _deviceMs;
        private long _monoMs;

        private TimerClock CreateClock(long startUtcMs)
        {
            this._deviceMs = startUtcMs;
            this._monoMs = 0;
            return new TimerClock(() => this._deviceMs, () => this._monoMs);
        }

        [Test]
        public void DeviceClockMovingBackward_DoesNotMoveNowBackward()
        {
            TimerClock clock = this.CreateClock(1_000_000);

            this._monoMs += 5000;
            this._deviceMs += 5000;
            long beforeRollback = clock.UtcNowMs;

            // User sets the device clock back by an hour.
            this._deviceMs -= 3_600_000;
            clock.ReanchorFromDevice();

            Assert.That(clock.UtcNowMs, Is.EqualTo(beforeRollback),
                "Now must never move backward when the device clock rolls back.");
        }

        [Test]
        public void ReanchorFromDevice_Backward_RaisesAnomaly()
        {
            TimerClock clock = this.CreateClock(1_000_000);
            ClockAnomaly? captured = null;
            clock.OnAnomaly += a => captured = a;

            this._deviceMs -= 10_000;
            clock.ReanchorFromDevice();

            Assert.That(captured.HasValue, Is.True);
            Assert.That(captured.Value.Kind, Is.EqualTo(ClockAnomalyKind.Backward));
            Assert.That(captured.Value.DeltaMs, Is.EqualTo(10_000));
        }

        [Test]
        public void ApplyFloor_RaisesAnchorWhenPersistedFloorIsAhead()
        {
            TimerClock clock = this.CreateClock(1_000_000);

            // Something (a previous session) saw a later time than this fresh anchor computes.
            clock.ApplyFloor(1_050_000);

            Assert.That(clock.UtcNowMs, Is.GreaterThanOrEqualTo(1_050_000));
        }

        [Test]
        public void ApplyFloor_DoesNotLowerNow_WhenFloorIsBehindCurrent()
        {
            TimerClock clock = this.CreateClock(1_000_000);
            this._monoMs += 100_000;
            this._deviceMs += 100_000;
            clock.CheckDrift();

            long before = clock.UtcNowMs;
            clock.ApplyFloor(1_000_000); // Behind current now.

            Assert.That(clock.UtcNowMs, Is.EqualTo(before));
        }

        [Test]
        public void SyncWithServer_SetsIsServerSynced_AndAdoptsServerTime()
        {
            TimerClock clock = this.CreateClock(1_000_000);

            clock.SyncWithServer(2_000_000);

            Assert.That(clock.IsServerSynced, Is.True);
            Assert.That(clock.UtcNowMs, Is.EqualTo(2_000_000));
        }

        [Test]
        public void ReanchorAfterServerSync_RaisesOnResyncRequired()
        {
            TimerClock clock = this.CreateClock(1_000_000);
            clock.SyncWithServer(2_000_000);

            bool resyncRequired = false;
            clock.OnResyncRequired += () => resyncRequired = true;

            clock.ReanchorFromDevice();

            Assert.That(clock.IsServerSynced, Is.False);
            Assert.That(resyncRequired, Is.True);
        }

        [Test]
        public void DeepSleep_MonotonicFrozen_DeviceClockAdvances_RemainingDropsByThatAmount()
        {
            TimerClock clock = this.CreateClock(1_000_000);

            // Simulate 10 minutes passing while the app is suspended: the OS clock advances but the
            // Stopwatch-backed monotonic source is frozen because the process itself was frozen.
            this._deviceMs += 10 * 60_000;

            long beforeReanchor = clock.UtcNowMs; // Still old, since monotonic hasn't moved.
            clock.ReanchorFromDevice();
            long afterReanchor = clock.UtcNowMs;

            Assert.That(afterReanchor - beforeReanchor, Is.EqualTo(10 * 60_000));
        }
    }
}
