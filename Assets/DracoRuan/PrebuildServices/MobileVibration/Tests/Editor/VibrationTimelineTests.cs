using DracoRuan.PrebuildServices.MobileVibration.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.MobileVibration.Tests
{
    public class VibrationTimelineTests
    {
        [Test]
        public void FromPulses_EmptyInput_ReturnsEmpty()
        {
            VibrationTimeline timeline = VibrationTimeline.FromPulses(
                System.Array.Empty<float>(), System.Array.Empty<float>(), System.Array.Empty<float>());

            Assert.AreEqual(0, timeline.Segments.Count);
            Assert.AreEqual(0f, timeline.TotalMs);
        }

        [Test]
        public void FromPulses_AccumulatesStartTimesAcrossDelayAndDuration()
        {
            VibrationTimeline timeline = VibrationTimeline.FromPulses(
                new float[] { 10f, 20f, 5f },
                new float[] { 50f, 30f, 0f },
                new float[] { 1f, 0.5f, 0.2f });

            Assert.AreEqual(3, timeline.Segments.Count);

            Assert.AreEqual(10f, timeline.Segments[0].StartMs);
            Assert.AreEqual(50f, timeline.Segments[0].DurationMs);
            Assert.AreEqual(1f, timeline.Segments[0].Strength01);

            // Second pulse starts after the first one's end (10 + 50) plus its own delay (20).
            Assert.AreEqual(80f, timeline.Segments[1].StartMs);
            Assert.AreEqual(30f, timeline.Segments[1].DurationMs);

            // Third pulse starts after the second one's end (80 + 30) plus its own delay (5).
            Assert.AreEqual(115f, timeline.Segments[2].StartMs);
            Assert.AreEqual(0f, timeline.Segments[2].DurationMs);

            Assert.AreEqual(115f, timeline.TotalMs);
        }

        [Test]
        public void FromPulses_ZeroDurations_ProducesZeroWidthMarkers()
        {
            VibrationTimeline timeline = VibrationTimeline.FromPulses(
                new float[] { 0f, 100f }, new float[] { 0f, 0f }, new float[] { 1f, 1f });

            Assert.AreEqual(0f, timeline.Segments[0].DurationMs);
            Assert.AreEqual(0f, timeline.Segments[1].DurationMs);
            Assert.AreEqual(100f, timeline.Segments[1].StartMs);
        }

        [Test]
        public void FromPulses_NegativeDelayAndDuration_ClampedToZero()
        {
            VibrationTimeline timeline = VibrationTimeline.FromPulses(
                new float[] { -10f }, new float[] { -5f }, new float[] { 1f });

            Assert.AreEqual(0f, timeline.Segments[0].StartMs);
            Assert.AreEqual(0f, timeline.Segments[0].DurationMs);
            Assert.AreEqual(0f, timeline.TotalMs);
        }

        [Test]
        public void FromPulses_StrengthOutOfRange_ClampedTo01()
        {
            VibrationTimeline timeline = VibrationTimeline.FromPulses(
                new float[] { 0f, 0f }, new float[] { 0f, 0f }, new float[] { -1f, 2f });

            Assert.AreEqual(0f, timeline.Segments[0].Strength01);
            Assert.AreEqual(1f, timeline.Segments[1].Strength01);
        }

        [Test]
        public void FromCurve_OffsetsSamplesByDelay_AndSpreadsAcrossDuration()
        {
            VibrationTimeline timeline = VibrationTimeline.FromCurve(20f, 100f, new float[] { 0f, 0.5f, 1f });

            Assert.AreEqual(3, timeline.Segments.Count);
            Assert.AreEqual(20f, timeline.Segments[0].StartMs);
            Assert.AreEqual(70f, timeline.Segments[1].StartMs);
            Assert.AreEqual(120f, timeline.Segments[2].StartMs);
            Assert.AreEqual(120f, timeline.TotalMs);
        }

        [Test]
        public void FromCurve_EmptySamples_ReturnsNoSegmentsButKeepsDelayAsTotal()
        {
            VibrationTimeline timeline = VibrationTimeline.FromCurve(15f, 100f, System.Array.Empty<float>());

            Assert.AreEqual(0, timeline.Segments.Count);
            Assert.AreEqual(15f, timeline.TotalMs);
        }

        [Test]
        public void FromCurve_ZeroDuration_ReturnsNoSegments()
        {
            VibrationTimeline timeline = VibrationTimeline.FromCurve(0f, 0f, new float[] { 0f, 1f });

            Assert.AreEqual(0, timeline.Segments.Count);
        }

        [Test]
        public void FromCurve_NegativeDelay_ClampedToZero()
        {
            VibrationTimeline timeline = VibrationTimeline.FromCurve(-5f, 10f, new float[] { 1f });

            Assert.AreEqual(0f, timeline.Segments[0].StartMs);
        }
    }
}
