using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// Crop-growth scenario from REWRITE_PLAN.md Q5: stage A = 1', B = 3', C = 8' (cumulative ends
    /// 1', 4', 12').
    /// </summary>
    [TestFixture]
    public sealed class MultiStageTierTests
    {
        private static readonly long[] StageDurations = { 60_000, 180_000, 480_000 }; // A, B, C

        private FakeClock _clock;
        private TimerScheduler _scheduler;
        private TimerHandle _handle;

        [SetUp]
        public void SetUp()
        {
            this._clock = new FakeClock(0);
            this._scheduler = new TimerScheduler(this._clock);
            this._scheduler.TryStart(new TimerSpec { Key = "crop", StageDurationsMs = StageDurations }, out this._handle);
        }

        [Test]
        public void At30Seconds_Stage0_HalfProgress_ElevenThirtyRemaining()
        {
            this._clock.Advance(30_000);
            this._scheduler.Tick(0);

            Assert.That(this._scheduler.GetCurrentStage(this._handle), Is.EqualTo(0));
            Assert.That(this._scheduler.GetStageProgress01(this._handle), Is.EqualTo(0.5).Within(0.0001));
            Assert.That(this._scheduler.GetRemainingMs(this._handle), Is.EqualTo(11 * 60_000 + 30_000));
        }

        [Test]
        public void StageChangedFiresAtOneAndFourMinutes_CompletedAtTwelve()
        {
            TestListener listener = new();
            this._scheduler.SetListener(this._handle, listener);

            this._clock.Advance(12 * 60_000);
            this._scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(3));
            Assert.That(listener.Events[0].Type, Is.EqualTo(TimerEventType.StageChanged));
            Assert.That(listener.Events[0].Stage, Is.EqualTo(1));
            Assert.That(listener.Events[0].AtMs, Is.EqualTo(60_000));
            Assert.That(listener.Events[1].Type, Is.EqualTo(TimerEventType.StageChanged));
            Assert.That(listener.Events[1].Stage, Is.EqualTo(2));
            Assert.That(listener.Events[1].AtMs, Is.EqualTo(240_000));
            Assert.That(listener.Events[2].Type, Is.EqualTo(TimerEventType.Completed));
            Assert.That(listener.Events[2].Stage, Is.EqualTo(3));
            Assert.That(listener.Events[2].AtMs, Is.EqualTo(720_000));
        }

        [Test]
        public void OfflineFiveMinutes_BeforeTick_CurrentStageAndRemainingAreCorrect()
        {
            // Simulate closing the app right after creation and reopening 5 minutes later, before
            // any Tick has happened - GetCurrentStage must compute from now, not DispatchedStage.
            this._clock.Advance(5 * 60_000);

            Assert.That(this._scheduler.GetCurrentStage(this._handle), Is.EqualTo(2));
            Assert.That(this._scheduler.GetStageRemainingMs(this._handle), Is.EqualTo(7 * 60_000));

            TestListener listener = new();
            this._scheduler.SetListener(this._handle, listener);
            this._scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(2));
            Assert.That(listener.Events[0].Stage, Is.EqualTo(1));
            Assert.That(listener.Events[1].Stage, Is.EqualTo(2));
        }

        [Test]
        public void Offline20Minutes_AddsLateCompletedEvent()
        {
            this._clock.Advance(20 * 60_000);

            TestListener listener = new();
            this._scheduler.SetListener(this._handle, listener);
            this._scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(3));
            TimerEvent completed = listener.Events[2];
            Assert.That(completed.Type, Is.EqualTo(TimerEventType.Completed));
            Assert.That(completed.AtMs, Is.EqualTo(720_000));
            Assert.That(completed.IsLate, Is.True);
        }

        [Test]
        public void SkipCurrentStage_At2Minutes_WhileInStageB_MaturesAt10Minutes()
        {
            this._clock.Advance(2 * 60_000); // Inside stage B (ends at 4').

            TestListener listener = new();
            this._scheduler.SetListener(this._handle, listener);

            this._scheduler.SkipCurrentStage(this._handle);

            Assert.That(listener.Events.Count, Is.EqualTo(1));
            Assert.That(listener.Events[0].Stage, Is.EqualTo(2));
            Assert.That(listener.Events[0].AtMs, Is.EqualTo(2 * 60_000));

            Assert.That(this._scheduler.GetEndUtcMs(this._handle), Is.EqualTo(10 * 60_000));
        }

        [Test]
        public void SpeedUpTwoMinutes_At30Seconds_WhileInStageB_LeavesOneMinuteThirty()
        {
            this._clock.Advance(30_000); // Still in stage A.
            this._scheduler.Tick(0);

            this._scheduler.SpeedUp(this._handle, 2 * 60_000);

            // 30s elapsed + 2' sped up = 2'30" total elapsed, which lands inside stage B (1'-4').
            Assert.That(this._scheduler.GetCurrentStage(this._handle), Is.EqualTo(1));
            Assert.That(this._scheduler.GetStageRemainingMs(this._handle), Is.EqualTo(90_000));
        }

        [Test]
        public void PauseAt2Minutes_Offline10Minutes_Resume_LeavesExactlyTwoMinutesInStageB()
        {
            this._clock.Advance(2 * 60_000);
            this._scheduler.Tick(0);

            this._scheduler.Pause(this._handle);
            this._clock.Advance(10 * 60_000);
            this._scheduler.Resume(this._handle);

            Assert.That(this._scheduler.GetCurrentStage(this._handle), Is.EqualTo(1));
            Assert.That(this._scheduler.GetStageRemainingMs(this._handle), Is.EqualTo(2 * 60_000));
        }

        [Test]
        public void MutatingSourceArrayAfterTryStart_DoesNotAffectAlreadyStartedTimer()
        {
            long[] durations = { 60_000, 60_000 };
            this._scheduler.TryStart(new TimerSpec { Key = "crop2", StageDurationsMs = durations }, out TimerHandle handle);

            durations[0] = 999_999_999;

            Assert.That(this._scheduler.GetEndUtcMs(handle), Is.EqualTo(120_000));
        }

        [Test]
        public void NonPositiveDuration_TryStart_ReturnsFalse()
        {
            bool ok1 = this._scheduler.TryStart(new TimerSpec { Key = "zero", DurationMs = 0 }, out _);
            bool ok2 = this._scheduler.TryStart(new TimerSpec { Key = "neg", DurationMs = -100 }, out _);
            bool ok3 = this._scheduler.TryStart(
                new TimerSpec { Key = "badstage", StageDurationsMs = new long[] { 1000, 0, 1000 } }, out _);

            Assert.That(ok1, Is.False);
            Assert.That(ok2, Is.False);
            Assert.That(ok3, Is.False);
        }
    }
}
