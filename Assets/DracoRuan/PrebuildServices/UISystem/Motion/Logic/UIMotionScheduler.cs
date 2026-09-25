using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.UISystem.Motion.Logic
{
    /// <summary>
    /// Computes each track's absolute start time within its timeline from its startMode
    /// and offset. Pure and side-effect free so it can run once at Awake and be re-run
    /// whenever track data changes in the editor.
    /// </summary>
    public static class UIMotionScheduler
    {
        public static float[] ComputeStartTimes(IReadOnlyList<UIMotionScheduleInput> tracks)
        {
            var starts = new float[tracks.Count];
            float previousStart = 0f;
            float previousDuration = 0f;

            for (int i = 0; i < tracks.Count; i++)
            {
                UIMotionScheduleInput track = tracks[i];
                float start;
                switch (track.StartMode)
                {
                    case UIMotionStartMode.AfterPrevious:
                        start = (i == 0 ? 0f : previousStart + previousDuration) + track.Offset;
                        break;
                    case UIMotionStartMode.AtTime:
                        start = track.Offset;
                        break;
                    case UIMotionStartMode.WithPrevious:
                    default:
                        start = (i == 0 ? 0f : previousStart) + track.Offset;
                        break;
                }

                starts[i] = start;
                previousStart = start;
                previousDuration = track.Duration;
            }

            return starts;
        }

        public static float ComputeTotalDuration(IReadOnlyList<UIMotionScheduleInput> tracks, float[] startTimes)
        {
            float total = 0f;
            for (int i = 0; i < tracks.Count; i++)
            {
                float end = startTimes[i] + tracks[i].Duration;
                if (end > total)
                    total = end;
            }

            return total;
        }
    }
}
