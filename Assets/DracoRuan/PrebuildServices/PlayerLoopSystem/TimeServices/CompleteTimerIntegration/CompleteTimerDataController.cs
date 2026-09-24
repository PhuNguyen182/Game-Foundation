using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.Runtime;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Production;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Regeneration;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.CompleteTimerIntegration
{
    /// <summary>
    /// Repository bridging the pure-C# <see cref="TimerScheduler"/> / <see cref="ProductionQueueRegistry"/> /
    /// <see cref="RegenerationCounterRegistry"/> to DataFlow's MessagePack persistence.
    /// </summary>
    /// <remarks>
    /// <para>The scheduler and registries are the actual source of truth at runtime; <see cref="DynamicGameDataController{TData}.Data"/>
    /// only holds a snapshot copy, refreshed in <see cref="OnBeforeSave"/> right before a flush - so a
    /// long play session never keeps two copies of the same state in sync on every mutation, only at
    /// the moment it is about to be written (see REWRITE_PLAN.md Q4/6.2).</para>
    /// </remarks>
    [DynamicGameDataController(Id)]
    public sealed class CompleteTimerDataController : DynamicGameDataController<TimerSaveDataV1>
    {
        /// <summary>Permanent save identifier; never rename, or every player's timer save is orphaned.</summary>
        public const string Id = "complete_timer";

        private readonly TimerClock _clock;
        private readonly TimerScheduler _scheduler;
        private readonly ProductionQueueRegistry _queues;
        private readonly RegenerationCounterRegistry _regenerations;

        private readonly List<TimerEntrySnapshot> _timerSnapshotBuffer = new();
        private readonly List<ProductionQueueSnapshot> _queueSnapshotBuffer = new();
        private readonly List<RegenerationSnapshot> _regenSnapshotBuffer = new();

        public CompleteTimerDataController(
            DataControllerContext context,
            TimerClock clock,
            TimerScheduler scheduler,
            ProductionQueueRegistry queues,
            RegenerationCounterRegistry regenerations)
            : base(context)
        {
            this._clock = clock;
            this._scheduler = scheduler;
            this._queues = queues;
            this._regenerations = regenerations;
        }

        public override string DomainId => Id;

        public override int SchemaVersion => 1;

        /// <summary>Raised whenever a bad entry is skipped during restore, so the runtime can forward it to <c>Debug.LogWarning</c>.</summary>
        public event System.Action<string> OnWarning;

        protected override void OnAfterLoad()
        {
            this._clock.ApplyFloor(this.Data.LastSeenUtcMs);

            this.RestoreSchedulerFromData();
            this._queues.Restore(BuildQueueSnapshots(this.Data.Queues), this.RaiseWarning);
            this._regenerations.Restore(BuildRegenSnapshots(this.Data.Regenerations), this.RaiseWarning);

            // Any create/cancel/complete/release the restore performed - or any that happens during
            // normal play afterward - should schedule a flush; the data on disk is stale the instant
            // a timer's state diverges from what was last written.
            // ProductionQueue and RegenerationCounter only ever mutate persisted state by creating,
            // cancelling or completing a scheduler timer, so this single subscription also covers
            // every queue/regeneration change - no separate "structure changed" event is needed from
            // either registry.
            this._scheduler.OnStructureChanged += this.MarkDirty;
        }

        protected override void OnBeforeSave()
        {
            this._scheduler.CaptureSnapshot(this._timerSnapshotBuffer);
            this._queues.Capture(this._queueSnapshotBuffer);
            this._regenerations.Capture(this._regenSnapshotBuffer);

            this.Data.Timers.Clear();
            for (int i = 0; i < this._timerSnapshotBuffer.Count; i++)
            {
                TimerEntrySnapshot snapshot = this._timerSnapshotBuffer[i];
                this.Data.Timers.Add(new TimerEntryV1
                {
                    Key = snapshot.Key,
                    Channel = snapshot.Channel,
                    State = (int)snapshot.State,
                    StartMs = snapshot.StartMs,
                    DurationMs = snapshot.DurationMs,
                    PausedAtMs = snapshot.PausedAtMs,
                    StageEnds = CopyArray(snapshot.StageEnds),
                    DispatchedStage = snapshot.DispatchedStage,
                    AutoRelease = snapshot.AutoRelease,
                    CompletedAtMs = snapshot.CompletedAtMs,
                    CompletedDelivered = snapshot.CompletedDelivered
                });
            }

            this.Data.Queues.Clear();
            for (int i = 0; i < this._queueSnapshotBuffer.Count; i++)
            {
                ProductionQueueSnapshot snapshot = this._queueSnapshotBuffer[i];
                ProductionQueueEntryV1 entry = new()
                {
                    QueueKey = snapshot.QueueKey,
                    Capacity = snapshot.Capacity,
                    Channel = snapshot.Channel,
                    ActiveTimerKey = snapshot.ActiveTimerKey,
                    Sequence = snapshot.Sequence,
                    IsPaused = snapshot.IsPaused
                };

                if (snapshot.Items != null)
                {
                    for (int j = 0; j < snapshot.Items.Length; j++)
                    {
                        entry.Items.Add(new ProductionQueueItemEntryV1
                        {
                            ItemId = snapshot.Items[j].ItemId,
                            DurationMs = snapshot.Items[j].DurationMs
                        });
                    }
                }

                if (snapshot.Outputs != null)
                {
                    for (int j = 0; j < snapshot.Outputs.Length; j++)
                    {
                        entry.Outputs.Add(new ProductionQueueOutputEntryV1
                        {
                            ItemId = snapshot.Outputs[j].ItemId,
                            AtMs = snapshot.Outputs[j].AtMs
                        });
                    }
                }

                this.Data.Queues.Add(entry);
            }

            this.Data.Regenerations.Clear();
            for (int i = 0; i < this._regenSnapshotBuffer.Count; i++)
            {
                RegenerationSnapshot snapshot = this._regenSnapshotBuffer[i];
                this.Data.Regenerations.Add(new RegenerationEntryV1
                {
                    Key = snapshot.Key,
                    Stored = snapshot.Stored,
                    Max = snapshot.Max,
                    IntervalMs = snapshot.IntervalMs,
                    AnchorMs = snapshot.AnchorMs
                });
            }

            this.Data.LastSeenUtcMs = this._clock.LastSeenUtcMs;
        }

        /// <summary>Forces an immediate flush, bypassing the debounced autosave schedule (e.g. right before a gem-spending purchase).</summary>
        public void SaveNow() => this.Save();

        private void RestoreSchedulerFromData()
        {
            this._timerSnapshotBuffer.Clear();
            for (int i = 0; i < this.Data.Timers.Count; i++)
            {
                TimerEntryV1 entry = this.Data.Timers[i];
                this._timerSnapshotBuffer.Add(new TimerEntrySnapshot
                {
                    Key = entry.Key,
                    Channel = entry.Channel,
                    State = (TimerState)entry.State,
                    StartMs = entry.StartMs,
                    DurationMs = entry.DurationMs,
                    PausedAtMs = entry.PausedAtMs,
                    StageEnds = CopyArray(entry.StageEnds),
                    DispatchedStage = entry.DispatchedStage,
                    AutoRelease = entry.AutoRelease,
                    CompletedAtMs = entry.CompletedAtMs,
                    CompletedDelivered = entry.CompletedDelivered
                });
            }

            this._scheduler.OnWarning += this.RaiseWarning;
            this._scheduler.Restore(this._timerSnapshotBuffer);
        }

        private static List<ProductionQueueSnapshot> BuildQueueSnapshots(List<ProductionQueueEntryV1> entries)
        {
            List<ProductionQueueSnapshot> result = new(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                ProductionQueueEntryV1 entry = entries[i];

                ProductionQueueItemSnapshot[] items = new ProductionQueueItemSnapshot[entry.Items.Count];
                for (int j = 0; j < items.Length; j++)
                    items[j] = new ProductionQueueItemSnapshot { ItemId = entry.Items[j].ItemId, DurationMs = entry.Items[j].DurationMs };

                ProductionQueueOutputSnapshot[] outputs = new ProductionQueueOutputSnapshot[entry.Outputs.Count];
                for (int j = 0; j < outputs.Length; j++)
                    outputs[j] = new ProductionQueueOutputSnapshot { ItemId = entry.Outputs[j].ItemId, AtMs = entry.Outputs[j].AtMs };

                result.Add(new ProductionQueueSnapshot
                {
                    QueueKey = entry.QueueKey,
                    Capacity = entry.Capacity,
                    Channel = entry.Channel,
                    Items = items,
                    Outputs = outputs,
                    ActiveTimerKey = entry.ActiveTimerKey,
                    Sequence = entry.Sequence,
                    IsPaused = entry.IsPaused
                });
            }

            return result;
        }

        private static List<RegenerationSnapshot> BuildRegenSnapshots(List<RegenerationEntryV1> entries)
        {
            List<RegenerationSnapshot> result = new(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                RegenerationEntryV1 entry = entries[i];
                result.Add(new RegenerationSnapshot
                {
                    Key = entry.Key,
                    Stored = entry.Stored,
                    Max = entry.Max,
                    IntervalMs = entry.IntervalMs,
                    AnchorMs = entry.AnchorMs
                });
            }

            return result;
        }

        private static long[] CopyArray(long[] source)
        {
            if (source == null)
                return null;

            long[] copy = new long[source.Length];
            System.Array.Copy(source, copy, source.Length);
            return copy;
        }

        private void RaiseWarning(string message) => this.OnWarning?.Invoke(message);
    }
}
