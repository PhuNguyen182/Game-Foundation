using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.CompleteTimerIntegration
{
    /// <summary>
    /// Copies timer state between the scheduler's <see cref="TimerEntrySnapshot"/>s and the persisted
    /// <see cref="TimerEntryV1"/> list, without allocating per timer once the list has reached its size.
    /// </summary>
    /// <remarks>
    /// <para>Saves overwrite the existing <see cref="TimerEntryV1"/> instances in place instead of
    /// clearing the list and building new ones, so a repeated save of N timers allocates nothing.
    /// That is safe because the codec serializes synchronously on the calling thread
    /// (<c>DynamicGameDataController.Save</c>), so nothing is reading the entries while they are rewritten.</para>
    /// <para><c>StageEnds</c> arrays are shared rather than copied in both directions. They are immutable
    /// on the scheduler side, the serializer only reads them, and <see cref="TimerScheduler.Restore"/>
    /// copies or interns whatever it keeps instead of holding on to the caller's array.</para>
    /// </remarks>
    public static class TimerSaveMapper
    {
        /// <summary>Writes <paramref name="snapshots"/> into <paramref name="entries"/>, reusing existing entries and trimming any surplus.</summary>
        public static void WriteEntries(List<TimerEntrySnapshot> snapshots, List<TimerEntryV1> entries)
        {
            int count = snapshots.Count;

            for (int i = 0; i < count; i++)
            {
                TimerEntryV1 entry = i < entries.Count ? entries[i] : null;
                if (entry == null)
                {
                    entry = new TimerEntryV1();
                    if (i < entries.Count)
                        entries[i] = entry;
                    else
                        entries.Add(entry);
                }

                TimerEntrySnapshot snapshot = snapshots[i];
                entry.Key = snapshot.Key;
                entry.Channel = snapshot.Channel;
                entry.State = (int)snapshot.State;
                entry.StartMs = snapshot.StartMs;
                entry.DurationMs = snapshot.DurationMs;
                entry.PausedAtMs = snapshot.PausedAtMs;
                entry.StageEnds = snapshot.StageEnds;
                entry.DispatchedStage = snapshot.DispatchedStage;
                entry.AutoRelease = snapshot.AutoRelease;
                entry.CompletedAtMs = snapshot.CompletedAtMs;
                entry.CompletedDelivered = snapshot.CompletedDelivered;
            }

            if (entries.Count > count)
                entries.RemoveRange(count, entries.Count - count);
        }

        /// <summary>Replaces the contents of <paramref name="into"/> with one snapshot per non-null entry.</summary>
        public static void ReadSnapshots(List<TimerEntryV1> entries, List<TimerEntrySnapshot> into)
        {
            into.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                TimerEntryV1 entry = entries[i];
                if (entry == null)
                    continue;

                into.Add(new TimerEntrySnapshot
                {
                    Key = entry.Key,
                    Channel = entry.Channel,
                    State = (TimerState)entry.State,
                    StartMs = entry.StartMs,
                    DurationMs = entry.DurationMs,
                    PausedAtMs = entry.PausedAtMs,
                    StageEnds = entry.StageEnds,
                    DispatchedStage = entry.DispatchedStage,
                    AutoRelease = entry.AutoRelease,
                    CompletedAtMs = entry.CompletedAtMs,
                    CompletedDelivered = entry.CompletedDelivered
                });
            }
        }
    }
}
