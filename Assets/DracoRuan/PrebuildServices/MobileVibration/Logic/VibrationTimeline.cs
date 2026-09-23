using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>One drawable slice of a haptic timeline, in milliseconds from the start of playback.</summary>
    public readonly struct VibrationTimelineSegment
    {
        public VibrationTimelineSegment(float startMs, float durationMs, float strength01)
        {
            this.StartMs = startMs;
            this.DurationMs = durationMs;
            this.Strength01 = strength01;
        }

        public float StartMs { get; }
        public float DurationMs { get; }

        /// <summary>0..1. A pulse's strength, or a curve sample's intensity.</summary>
        public float Strength01 { get; }
    }

    /// <summary>
    /// A flattened, platform-agnostic view of one haptic timeline, built from primitive arrays so the
    /// editor's UI code stays the only place that touches MOST's vendored structs.
    /// </summary>
    public readonly struct VibrationTimeline
    {
        public static readonly VibrationTimeline Empty = new VibrationTimeline(Array.Empty<VibrationTimelineSegment>(), 0f);

        private VibrationTimeline(IReadOnlyList<VibrationTimelineSegment> segments, float totalMs)
        {
            this.Segments = segments;
            this.TotalMs = totalMs;
        }

        public IReadOnlyList<VibrationTimelineSegment> Segments { get; }

        /// <summary>The span the timeline should be drawn across, in milliseconds.</summary>
        public float TotalMs { get; }

        /// <summary>
        /// Builds a timeline from a sequence of pulses, each starting after its own delay from the end
        /// of the previous one.
        /// </summary>
        /// <param name="delaysMs">Gap before each pulse starts, relative to the previous pulse's end.</param>
        /// <param name="durationsMs">
        /// How long each pulse visually spans. Pass 0 for every entry to draw zero-width markers (iOS
        /// preset pulses have no authored duration).
        /// </param>
        /// <param name="strengths01">0..1 strength per pulse, for bar height.</param>
        public static VibrationTimeline FromPulses(
            IReadOnlyList<float> delaysMs, IReadOnlyList<float> durationsMs, IReadOnlyList<float> strengths01)
        {
            if (delaysMs == null || delaysMs.Count == 0)
                return Empty;

            List<VibrationTimelineSegment> segments = new List<VibrationTimelineSegment>(delaysMs.Count);
            float cursorMs = 0f;

            for (int i = 0; i < delaysMs.Count; i++)
            {
                cursorMs += Math.Max(0f, delaysMs[i]);

                float durationMs = i < durationsMs.Count ? Math.Max(0f, durationsMs[i]) : 0f;
                float strength01 = i < strengths01.Count ? Clamp01(strengths01[i]) : 1f;

                segments.Add(new VibrationTimelineSegment(cursorMs, durationMs, strength01));
                cursorMs += durationMs;
            }

            return new VibrationTimeline(segments, cursorMs);
        }

        /// <summary>Builds a timeline from a sampled intensity curve, offset by a start delay.</summary>
        /// <param name="delayMs">Gap before the curve starts.</param>
        /// <param name="durationMs">Total curve span.</param>
        /// <param name="samples01">Intensity samples, evenly spaced across <paramref name="durationMs"/>.</param>
        public static VibrationTimeline FromCurve(float delayMs, float durationMs, IReadOnlyList<float> samples01)
        {
            delayMs = Math.Max(0f, delayMs);
            durationMs = Math.Max(0f, durationMs);

            if (samples01 == null || samples01.Count == 0 || durationMs <= 0f)
                return new VibrationTimeline(Array.Empty<VibrationTimelineSegment>(), delayMs);

            List<VibrationTimelineSegment> segments = new List<VibrationTimelineSegment>(samples01.Count);
            float stepMs = samples01.Count > 1 ? durationMs / (samples01.Count - 1) : 0f;

            for (int i = 0; i < samples01.Count; i++)
                segments.Add(new VibrationTimelineSegment(delayMs + stepMs * i, 0f, Clamp01(samples01[i])));

            return new VibrationTimeline(segments, delayMs + durationMs);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
