using System.Collections.Generic;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using DracoRuan.PrebuildServices.UISystem.Motion.Logic.DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public class UIMotionSchedulerTests
    {
        [Test]
        public void ComputeStartTimes_SingleTrack_AfterPrevious_StartsAtOffset()
        {
            var tracks = new List<UIMotionScheduleInput>
            {
                new UIMotionScheduleInput(UIMotionStartMode.AfterPrevious, offset: 0.5f, duration: 1f),
            };

            float[] starts = UIMotionScheduler.ComputeStartTimes(tracks);

            Assert.AreEqual(1, starts.Length);
            Assert.AreEqual(0.5f, starts[0], 1e-6f);
        }

        [Test]
        public void ComputeStartTimes_SingleTrack_WithPrevious_StartsAtOffset()
        {
            var tracks = new List<UIMotionScheduleInput>
            {
                new UIMotionScheduleInput(UIMotionStartMode.WithPrevious, offset: 0.3f, duration: 1f),
            };

            float[] starts = UIMotionScheduler.ComputeStartTimes(tracks);

            Assert.AreEqual(0.3f, starts[0], 1e-6f);
        }

        [Test]
        public void ComputeStartTimes_SingleTrack_AtTime_StartsAtAbsoluteOffset()
        {
            var tracks = new List<UIMotionScheduleInput>
            {
                new UIMotionScheduleInput(UIMotionStartMode.AtTime, offset: 2f, duration: 1f),
            };

            float[] starts = UIMotionScheduler.ComputeStartTimes(tracks);

            Assert.AreEqual(2f, starts[0], 1e-6f);
        }

        [Test]
        public void ComputeStartTimes_WithPrevious_RunsInParallel()
        {
            var tracks = new List<UIMotionScheduleInput>
            {
                new UIMotionScheduleInput(UIMotionStartMode.AfterPrevious, offset: 0f, duration: 1f),
                new UIMotionScheduleInput(UIMotionStartMode.WithPrevious, offset: 0.2f, duration: 1f),
            };

            float[] starts = UIMotionScheduler.ComputeStartTimes(tracks);

            // Second track starts relative to the first track's own start time, not its end.
            Assert.AreEqual(0f, starts[0], 1e-6f);
            Assert.AreEqual(0.2f, starts[1], 1e-6f);
        }

        [Test]
        public void ComputeStartTimes_AfterPrevious_WaitsForPreviousToFinish()
        {
            var tracks = new List<UIMotionScheduleInput>
            {
                new UIMotionScheduleInput(UIMotionStartMode.AfterPrevious, offset: 0f, duration: 1f),
                new UIMotionScheduleInput(UIMotionStartMode.AfterPrevious, offset: 0.5f, duration: 1f),
            };

            float[] starts = UIMotionScheduler.ComputeStartTimes(tracks);

            Assert.AreEqual(0f, starts[0], 1e-6f);
            // previous start (0) + previous duration (1) + this offset (0.5)
            Assert.AreEqual(1.5f, starts[1], 1e-6f);
        }

        [Test]
        public void ComputeStartTimes_AtTime_IgnoresPreviousEntirely()
        {
            var tracks = new List<UIMotionScheduleInput>
            {
                new UIMotionScheduleInput(UIMotionStartMode.AfterPrevious, offset: 0f, duration: 5f),
                new UIMotionScheduleInput(UIMotionStartMode.AtTime, offset: 0.75f, duration: 1f),
            };

            float[] starts = UIMotionScheduler.ComputeStartTimes(tracks);

            Assert.AreEqual(0.75f, starts[1], 1e-6f);
        }

        [Test]
        public void ComputeStartTimes_MixedChain_MatchesExpectedSchedule()
        {
            // A (AfterPrevious, offset 0, dur 1) -> starts 0, ends 1
            // B (WithPrevious relative to A, offset 0.1, dur 0.5) -> starts 0.1 (parallel with A)
            // C (AfterPrevious relative to B, offset 0, dur 2) -> starts at B.start + B.duration = 0.6
            var tracks = new List<UIMotionScheduleInput>
            {
                new UIMotionScheduleInput(UIMotionStartMode.AfterPrevious, offset: 0f, duration: 1f),
                new UIMotionScheduleInput(UIMotionStartMode.WithPrevious, offset: 0.1f, duration: 0.5f),
                new UIMotionScheduleInput(UIMotionStartMode.AfterPrevious, offset: 0f, duration: 2f),
            };

            float[] starts = UIMotionScheduler.ComputeStartTimes(tracks);

            Assert.AreEqual(0f, starts[0], 1e-6f);
            Assert.AreEqual(0.1f, starts[1], 1e-6f);
            Assert.AreEqual(0.6f, starts[2], 1e-6f);
        }

        [Test]
        public void ComputeStartTimes_EmptyList_ReturnsEmptyArray()
        {
            float[] starts = UIMotionScheduler.ComputeStartTimes(new List<UIMotionScheduleInput>());
            Assert.AreEqual(0, starts.Length);
        }

        [Test]
        public void ComputeTotalDuration_ReturnsLatestTrackEnd()
        {
            var tracks = new List<UIMotionScheduleInput>
            {
                new UIMotionScheduleInput(UIMotionStartMode.AfterPrevious, offset: 0f, duration: 1f),
                new UIMotionScheduleInput(UIMotionStartMode.WithPrevious, offset: 0f, duration: 3f),
            };
            float[] starts = UIMotionScheduler.ComputeStartTimes(tracks);

            float total = UIMotionScheduler.ComputeTotalDuration(tracks, starts);

            // Second track starts at 0 (WithPrevious of first, offset 0) and runs 3s -> ends at 3.
            Assert.AreEqual(3f, total, 1e-6f);
        }

        [Test]
        public void ComputeTotalDuration_EmptyList_ReturnsZero()
        {
            float total = UIMotionScheduler.ComputeTotalDuration(
                new List<UIMotionScheduleInput>(), new float[0]);
            Assert.AreEqual(0f, total, 1e-6f);
        }
    }
}
