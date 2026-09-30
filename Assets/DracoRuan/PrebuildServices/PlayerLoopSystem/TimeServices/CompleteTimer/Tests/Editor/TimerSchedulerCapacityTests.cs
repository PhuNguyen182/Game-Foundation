using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Tests
{
    /// <summary>
    /// Covers slot bookkeeping: records are created lazily, the record array grows past its initial
    /// capacity, slots are reused with a bumped version, and equal deadlines keep creation order.
    /// </summary>
    [TestFixture]
    public sealed class TimerSchedulerCapacityTests
    {
        private FakeClock _clock;

        [SetUp]
        public void SetUp()
        {
            this._clock = new FakeClock(0);
        }

        [Test]
        public void StartingMoreThanInitialCapacity_GrowsAndKeepsEveryTimerResolvable()
        {
            TimerScheduler scheduler = new(this._clock, initialCapacity: 4);
            TestListener listener = new();
            scheduler.AddChannelListener(0, listener);

            const int count = 50;
            TimerHandle[] handles = new TimerHandle[count];
            for (int i = 0; i < count; i++)
            {
                Assert.That(scheduler.TryStart(new TimerSpec { Key = "t" + i, DurationMs = (count - i) * 1000L },
                    out handles[i]), Is.True);
            }

            for (int i = 0; i < count; i++)
                Assert.That(scheduler.GetState(handles[i]), Is.EqualTo(TimerState.Running), "index " + i);

            this._clock.Advance(count * 1000L + 1);
            scheduler.Tick(0);

            Assert.That(scheduler.CompletedCount, Is.EqualTo(count));
            Assert.That(listener.Events.Count, Is.EqualTo(count));

            // Shortest duration is the last one started, so completions arrive in reverse start order.
            for (int i = 0; i < count; i++)
                Assert.That(listener.Events[i].Key, Is.EqualTo("t" + (count - 1 - i)));
        }

        [Test]
        public void HandleToNeverUsedSlot_IsTreatedAsFree()
        {
            TimerScheduler scheduler = new(this._clock, initialCapacity: 256);
            TimerHandle forged = new(200, 0);

            Assert.That(scheduler.GetState(forged), Is.EqualTo(TimerState.Free));

            Assert.DoesNotThrow(() =>
            {
                scheduler.Pause(forged);
                scheduler.Cancel(forged);
                scheduler.Release(forged);
                scheduler.CompleteNow(forged);
            });
        }

        [Test]
        public void ReleasedSlot_IsReusedWithNewVersion_AndOldHandleGoesStale()
        {
            TimerScheduler scheduler = new(this._clock);

            scheduler.TryStart(new TimerSpec { Key = "a", DurationMs = 1000 }, out TimerHandle first);
            scheduler.Cancel(first);

            scheduler.TryStart(new TimerSpec { Key = "b", DurationMs = 1000 }, out TimerHandle second);

            Assert.That(second.Index, Is.EqualTo(first.Index), "freed slot should be reused first");
            Assert.That(second.Version, Is.Not.EqualTo(first.Version));
            Assert.That(scheduler.GetState(first), Is.EqualTo(TimerState.Free));
            Assert.That(scheduler.GetState(second), Is.EqualTo(TimerState.Running));
        }

        [Test]
        public void CaptureSnapshot_AfterGrowthAndCancels_ReturnsOnlyLiveTimers()
        {
            TimerScheduler scheduler = new(this._clock, initialCapacity: 4);

            TimerHandle[] handles = new TimerHandle[10];
            for (int i = 0; i < handles.Length; i++)
                scheduler.TryStart(new TimerSpec { Key = "t" + i, DurationMs = 5000 }, out handles[i]);

            scheduler.Cancel(handles[2]);
            scheduler.Cancel(handles[5]);
            scheduler.Cancel(handles[9]);

            List<TimerEntrySnapshot> snapshot = new();
            scheduler.CaptureSnapshot(snapshot);

            Assert.That(snapshot.Count, Is.EqualTo(7));

            List<string> keys = new();
            foreach (TimerEntrySnapshot entry in snapshot)
                keys.Add(entry.Key);

            Assert.That(keys, Is.EquivalentTo(new[] { "t0", "t1", "t3", "t4", "t6", "t7", "t8" }));
        }

        [Test]
        public void EqualDeadlines_CompleteInCreationOrder()
        {
            TimerScheduler scheduler = new(this._clock);
            TestListener listener = new();
            scheduler.AddChannelListener(0, listener);

            string[] keys = { "a", "b", "c", "d", "e" };
            foreach (string key in keys)
                scheduler.TryStart(new TimerSpec { Key = key, DurationMs = 1000 }, out _);

            this._clock.Advance(1000);
            scheduler.Tick(0);

            Assert.That(listener.Events.Count, Is.EqualTo(keys.Length));
            for (int i = 0; i < keys.Length; i++)
                Assert.That(listener.Events[i].Key, Is.EqualTo(keys[i]));
        }

        [Test]
        public void SameSingleStageDuration_SharesStageEndsAcrossSnapshots()
        {
            TimerScheduler scheduler = new(this._clock);

            scheduler.TryStart(new TimerSpec { Key = "a", DurationMs = 4000 }, out _);
            scheduler.TryStart(new TimerSpec { Key = "b", DurationMs = 4000 }, out _);

            List<TimerEntrySnapshot> snapshot = new();
            scheduler.CaptureSnapshot(snapshot);

            Assert.That(snapshot[0].StageEnds, Is.SameAs(snapshot[1].StageEnds));
            Assert.That(snapshot[0].StageEnds, Is.EqualTo(new long[] { 4000 }));
            Assert.That(snapshot[0].DurationMs, Is.EqualTo(4000));
        }
    }
}
