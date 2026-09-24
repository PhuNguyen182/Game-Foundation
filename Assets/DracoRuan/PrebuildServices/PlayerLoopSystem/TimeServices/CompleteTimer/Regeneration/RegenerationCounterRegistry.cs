using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Regeneration
{
    /// <summary>Owns every <see cref="RegenerationCounter"/> in the game and handles their bulk capture/restore.</summary>
    public sealed class RegenerationCounterRegistry
    {
        private readonly TimerScheduler _scheduler;
        private readonly ITimeProvider _clock;
        private readonly Dictionary<string, RegenerationCounter> _counters;

        public RegenerationCounterRegistry(TimerScheduler scheduler, ITimeProvider clock)
        {
            this._scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this._clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this._counters = new Dictionary<string, RegenerationCounter>();
        }

        /// <summary>Creates and registers a new counter, full at creation time. Throws if <paramref name="key"/> is already registered.</summary>
        public RegenerationCounter Create(string key, long max, long intervalMs)
        {
            if (this._counters.ContainsKey(key))
                throw new InvalidOperationException($"Regeneration counter '{key}' already exists.");

            long nowMs = this._clock.UtcNowMs;
            RegenerationCounter counter = new(this._scheduler, key, stored: max, max, intervalMs, anchorMs: nowMs);
            this._counters[key] = counter;
            return counter;
        }

        public bool TryGet(string key, out RegenerationCounter counter) => this._counters.TryGetValue(key, out counter);

        public void Capture(List<RegenerationSnapshot> into)
        {
            if (into == null)
                return;

            into.Clear();
            foreach (KeyValuePair<string, RegenerationCounter> pair in this._counters)
                into.Add(pair.Value.Capture());
        }

        /// <summary>
        /// Restores every entry into its matching already-registered counter (by <see cref="RegenerationSnapshot.Key"/>).
        /// Counters must be created up front by gameplay code before restoring, exactly as with
        /// <see cref="Production.ProductionQueueRegistry"/>.
        /// </summary>
        public void Restore(IReadOnlyList<RegenerationSnapshot> entries, Action<string> onWarning = null)
        {
            if (entries == null)
                return;

            long nowMs = this._clock.UtcNowMs;

            for (int i = 0; i < entries.Count; i++)
            {
                RegenerationSnapshot entry = entries[i];
                if (this._counters.TryGetValue(entry.Key, out RegenerationCounter counter))
                {
                    counter.RestoreFrom(entry, nowMs);
                }
                else
                {
                    onWarning?.Invoke(
                        $"[RegenerationCounterRegistry] Skipped restore for unregistered counter '{entry.Key}'.");
                }
            }
        }
    }
}
