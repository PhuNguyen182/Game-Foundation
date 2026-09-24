using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Production
{
    /// <summary>
    /// Owns every <see cref="ProductionQueue"/> in the game and handles their bulk
    /// capture/restore for the integration layer.
    /// </summary>
    public sealed class ProductionQueueRegistry
    {
        private readonly TimerScheduler _scheduler;
        private readonly Dictionary<string, ProductionQueue> _queues;

        public ProductionQueueRegistry(TimerScheduler scheduler)
        {
            this._scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this._queues = new Dictionary<string, ProductionQueue>();
        }

        /// <summary>Creates and registers a new queue. Throws if <paramref name="queueKey"/> is already registered.</summary>
        public ProductionQueue Create(string queueKey, int capacity, int channel)
        {
            if (this._queues.ContainsKey(queueKey))
                throw new InvalidOperationException($"Production queue '{queueKey}' already exists.");

            ProductionQueue queue = new(this._scheduler, queueKey, capacity, channel);
            this._queues[queueKey] = queue;
            return queue;
        }

        public bool TryGet(string queueKey, out ProductionQueue queue) => this._queues.TryGetValue(queueKey, out queue);

        public void Capture(List<ProductionQueueSnapshot> into)
        {
            if (into == null)
                return;

            into.Clear();
            foreach (KeyValuePair<string, ProductionQueue> pair in this._queues)
                into.Add(pair.Value.Capture());
        }

        /// <summary>
        /// Restores every entry into its matching already-registered queue (by <see cref="ProductionQueueSnapshot.QueueKey"/>).
        /// Entries whose queue was not registered this session are skipped - queues must be created
        /// up front by gameplay code, in the same way they are every session, before restoring.
        /// </summary>
        public void Restore(IReadOnlyList<ProductionQueueSnapshot> entries, Action<string> onWarning = null)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                ProductionQueueSnapshot entry = entries[i];
                if (this._queues.TryGetValue(entry.QueueKey, out ProductionQueue queue))
                {
                    queue.Restore(entry);
                }
                else
                {
                    onWarning?.Invoke(
                        $"[ProductionQueueRegistry] Skipped restore for unregistered queue '{entry.QueueKey}'.");
                }
            }
        }
    }
}
