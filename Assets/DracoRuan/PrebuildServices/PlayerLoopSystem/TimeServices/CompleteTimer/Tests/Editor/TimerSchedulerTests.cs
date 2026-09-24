using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>Core <see cref="TimerScheduler"/> behavior (see REWRITE_PLAN.md 6.5, TimerSchedulerTests).</summary>
    [TestFixture]
    public sealed class TimerSchedulerTests
    {
        private FakeClock _clock;
        private TimerScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            this._clock = new FakeClock(1_000_000);
            this._scheduler = new TimerScheduler(this._clock);
        }

        [Test]
        public void SixtySecondTimer_At30s_HasThirtySecondsRemaining()
        {
            this._scheduler.TryStart(new TimerSpec { Key = "t1", DurationMs = 60_000 }, out TimerHandle h);

            this._clock.Advance(30_000);
            this._scheduler.Tick(0);

            Assert.That(this._scheduler.GetRemainingMs(h), Is.EqualTo(30_000));
        }

        [Test]
        public void Completed_FiresAtExactMoment_OnlyOnce()
        {
            this._scheduler.TryStart(new TimerSpec { Key = "t1", DurationMs = 10_000 }, out TimerHandle h);
            TestListener listener = new();
            this._scheduler.SetListener(h, listener);

            this._clock.Advance(10_000);
            this._scheduler.Tick(0);
            this._scheduler.Tick(0); // A second Tick with no state change must not re-fire.

            Assert.That(listener.Events.Count, Is.EqualTo(1));
            Assert.That(listener.Events[0].Type, Is.EqualTo(TimerEventType.Completed));
            Assert.That(listener.Events[0].AtMs, Is.EqualTo(1_010_000));
        }

        [Test]
        public void MultiStage_FiresInOrder_WithCorrectAtMs()
        {
            this._scheduler.TryStart(
                new TimerSpec { Key = "t1", StageDurationsMs = new long[] { 1000, 2000, 3000 } },
                out TimerHandle h);
            TestListener listener = new();
            this._scheduler.SetListener(h, listener);

            this._clock.Advance(6000);
            this._scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(3));
            Assert.That(listener.Events[0].Type, Is.EqualTo(TimerEventType.StageChanged));
            Assert.That(listener.Events[0].Stage, Is.EqualTo(1));
            Assert.That(listener.Events[0].AtMs, Is.EqualTo(1_001_000));
            Assert.That(listener.Events[1].Type, Is.EqualTo(TimerEventType.StageChanged));
            Assert.That(listener.Events[1].Stage, Is.EqualTo(2));
            Assert.That(listener.Events[1].AtMs, Is.EqualTo(1_003_000));
            Assert.That(listener.Events[2].Type, Is.EqualTo(TimerEventType.Completed));
            Assert.That(listener.Events[2].Stage, Is.EqualTo(3));
            Assert.That(listener.Events[2].AtMs, Is.EqualTo(1_006_000));
        }

        [Test]
        public void PauseThenResume_FreezesRemainingDuringPause()
        {
            this._scheduler.TryStart(new TimerSpec { Key = "t1", DurationMs = 60_000 }, out TimerHandle h);

            this._clock.Advance(10_000);
            this._scheduler.Tick(0);
            this._scheduler.Pause(h);

            this._clock.Advance(20_000); // Offline while paused.
            Assert.That(this._scheduler.GetRemainingMs(h), Is.EqualTo(50_000));

            this._scheduler.Resume(h);
            Assert.That(this._scheduler.GetRemainingMs(h), Is.EqualTo(50_000));

            this._clock.Advance(5_000);
            this._scheduler.Tick(0);
            Assert.That(this._scheduler.GetRemainingMs(h), Is.EqualTo(45_000));
        }

        [Test]
        public void SpeedUp_CanSkipMultipleStages()
        {
            this._scheduler.TryStart(
                new TimerSpec { Key = "t1", StageDurationsMs = new long[] { 1000, 1000, 1000 } },
                out TimerHandle h);
            TestListener listener = new();
            this._scheduler.SetListener(h, listener);

            this._scheduler.SpeedUp(h, 2500); // Should jump past stage 0 and stage 1 boundaries.

            Assert.That(listener.Events.Count, Is.EqualTo(2));
            Assert.That(listener.Events[0].Stage, Is.EqualTo(1));
            Assert.That(listener.Events[1].Stage, Is.EqualTo(2));
            Assert.That(this._scheduler.GetStageRemainingMs(h), Is.EqualTo(500));
        }

        [Test]
        public void CompleteNow_DispatchesAllRemainingStagesThenCompleted()
        {
            this._scheduler.TryStart(
                new TimerSpec { Key = "t1", StageDurationsMs = new long[] { 1000, 1000 } },
                out TimerHandle h);
            TestListener listener = new();
            this._scheduler.SetListener(h, listener);

            this._scheduler.CompleteNow(h);

            Assert.That(listener.Events.Count, Is.EqualTo(2));
            Assert.That(listener.Events[0].Type, Is.EqualTo(TimerEventType.StageChanged));
            Assert.That(listener.Events[1].Type, Is.EqualTo(TimerEventType.Completed));
            Assert.That(this._scheduler.GetState(h), Is.EqualTo(TimerState.Completed));
        }

        [Test]
        public void CancelAndRelease_InsideCallback_DoesNotThrow()
        {
            this._scheduler.TryStart(new TimerSpec { Key = "t1", DurationMs = 1000 }, out TimerHandle h1);
            this._scheduler.TryStart(new TimerSpec { Key = "t2", DurationMs = 1000 }, out TimerHandle h2);

            CallbackListener listener = new(this._scheduler, h2);
            this._scheduler.SetListener(h1, listener);

            this._clock.Advance(1000);

            Assert.DoesNotThrow(() => this._scheduler.Tick(0));
        }

        private sealed class CallbackListener : ITimerListener
        {
            private readonly TimerScheduler _scheduler;
            private readonly TimerHandle _otherHandle;

            public CallbackListener(TimerScheduler scheduler, TimerHandle otherHandle)
            {
                this._scheduler = scheduler;
                this._otherHandle = otherHandle;
            }

            public void OnTimerEvent(in TimerEvent e)
            {
                this._scheduler.Cancel(this._otherHandle);
                this._scheduler.Release(e.Handle);
            }
        }

        [Test]
        public void OldHandle_AfterRelease_IsRejectedByQueries()
        {
            this._scheduler.TryStart(new TimerSpec { Key = "t1", DurationMs = 1000 }, out TimerHandle h);
            this._clock.Advance(1000);
            this._scheduler.Tick(0);
            this._scheduler.Release(h);

            Assert.That(this._scheduler.GetState(h), Is.EqualTo(TimerState.Free));
            Assert.That(this._scheduler.GetRemainingMs(h), Is.EqualTo(0));
        }

        [Test]
        public void DuplicateKey_TryStart_ReturnsFalse()
        {
            this._scheduler.TryStart(new TimerSpec { Key = "dup", DurationMs = 1000 }, out _);
            bool second = this._scheduler.TryStart(new TimerSpec { Key = "dup", DurationMs = 2000 }, out TimerHandle handle2);

            Assert.That(second, Is.False);
            Assert.That(handle2.IsValid, Is.False);
        }

        [Test]
        public void OfflineCatchUp_MultipleTimers_DeliveredInAtMsOrder()
        {
            this._scheduler.TryStart(new TimerSpec { Key = "a", DurationMs = 3000 }, out TimerHandle ha);
            this._scheduler.TryStart(new TimerSpec { Key = "b", DurationMs = 1000 }, out TimerHandle hb);
            this._scheduler.TryStart(new TimerSpec { Key = "c", DurationMs = 2000 }, out TimerHandle hc);

            TestListener listener = new();
            this._scheduler.AddChannelListener(0, listener);

            this._clock.Advance(5000);
            this._scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(3));
            Assert.That(listener.Events[0].Key, Is.EqualTo("b"));
            Assert.That(listener.Events[1].Key, Is.EqualTo("c"));
            Assert.That(listener.Events[2].Key, Is.EqualTo("a"));
        }

        [Test]
        public void MaxEventsPerTick_LimitsProcessingPerTick()
        {
            TimerScheduler limited = new(this._clock, maxEventsPerTick: 2);
            limited.TryStart(new TimerSpec { Key = "a", DurationMs = 1000 }, out _);
            limited.TryStart(new TimerSpec { Key = "b", DurationMs = 1000 }, out _);
            limited.TryStart(new TimerSpec { Key = "c", DurationMs = 1000 }, out _);

            TestListener listener = new();
            limited.AddChannelListener(0, listener);

            this._clock.Advance(5000);
            limited.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(2));

            limited.Tick(0);
            Assert.That(listener.Events.Count, Is.EqualTo(3));
        }
    }
}
