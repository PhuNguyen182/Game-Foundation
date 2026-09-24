using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Clock;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>
    /// Single min-heap-backed scheduler for every timer in the game.
    /// </summary>
    /// <remarks>
    /// <para><b>One scheduler, one heap.</b> Each frame only the heap's top (the single nearest
    /// deadline across every timer) is inspected, so an idle frame with 10,000 running timers costs
    /// O(1) - not O(N) - and popping a timer that is due costs O(log N). This is the fix for the old
    /// per-timer-per-frame <c>DateTime.UtcNow</c> + tier loop described in REWRITE_PLAN.md 1.3.</para>
    ///
    /// <para><b>Implements <see cref="IUpdateHandler"/></b> so <c>UpdateServiceManager</c> can drive
    /// it, but it reads time exclusively from the injected <see cref="ITimeProvider"/> - the
    /// <c>deltaTime</c> parameter only triggers a check, it is never accumulated into any timer's
    /// state (that accumulation bug is exactly what corrupted the old implementation).</para>
    /// </remarks>
    public sealed class TimerScheduler : IUpdateHandler
    {
        /// <summary>Default warning threshold for the number of Completed-but-unreleased timers.</summary>
        public const int DefaultCompletedWarningThreshold = 1000;

        /// <summary>An event is considered late when dispatched more than this long after its theoretical <see cref="TimerEvent.AtMs"/>.</summary>
        private const long LateThresholdMs = 1000;

        private readonly ITimeProvider _clock;
        private readonly int _maxEventsPerTick;

        private TimerRecord[] _records;
        private readonly TimerMinHeap _heap;
        private readonly Dictionary<string, int> _keyToIndex;
        private readonly Stack<int> _freeList;
        private readonly Dictionary<int, ITimerListener> _channelListeners;
        private readonly List<TimerEvent> _undelivered;
        private readonly List<TimerEvent> _deliveryScratch;

        private long _sequenceCounter;
        private bool _structureChangedThisTick;
        private long _cachedNowMs;
        private int _completedCount;

        public TimerScheduler(ITimeProvider clock, int initialCapacity = 256, int maxEventsPerTick = 10000)
        {
            this._clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this._maxEventsPerTick = maxEventsPerTick;

            int capacity = Math.Max(4, initialCapacity);
            this._records = new TimerRecord[capacity];
            this._keyToIndex = new Dictionary<string, int>(capacity);
            this._freeList = new Stack<int>(capacity);
            this._channelListeners = new Dictionary<int, ITimerListener>();
            this._undelivered = new List<TimerEvent>();
            this._deliveryScratch = new List<TimerEvent>();

            for (int i = capacity - 1; i >= 0; i--)
            {
                this._records[i] = new TimerRecord { HeapIndex = -1 };
                this._freeList.Push(i);
            }

            this._heap = new TimerMinHeap(this._records, capacity);

            this._cachedNowMs = this._clock.UtcNowMs;
        }

        /// <summary>Threshold for <see cref="OnWarning"/> when too many timers sit Completed without being Released.</summary>
        public int CompletedWarningThreshold { get; set; } = DefaultCompletedWarningThreshold;

        /// <summary>Number of timer count currently in <see cref="TimerState.Completed"/> state.</summary>
        public int CompletedCount => this._completedCount;

        /// <summary>Cached "now" from the most recent <see cref="Tick"/> (or construction, before the first tick).</summary>
        public long NowMs => this._cachedNowMs;

        /// <summary>Fired for non-fatal problems (bad snapshot entries, threshold breaches). Core has no Unity, so logging is a callback.</summary>
        public event Action<string> OnWarning;

        /// <summary>Fired once per Tick (at most) when any timer was created, cancelled, paused, resumed, sped up, completed or released.</summary>
        public event Action OnStructureChanged;

        // ------------------------------------------------------------------
        // Creation / lookup
        // ------------------------------------------------------------------

        public bool TryStart(in TimerSpec spec, out TimerHandle handle)
        {
            if (string.IsNullOrEmpty(spec.Key) || this._keyToIndex.ContainsKey(spec.Key))
            {
                handle = TimerHandle.Invalid;
                return false;
            }

            long[] stageEnds = BuildStageEnds(spec);
            if (stageEnds == null)
            {
                handle = TimerHandle.Invalid;
                return false;
            }

            int index = this.AcquireSlot();
            TimerRecord record = this._records[index];

            long startMs = spec.StartUtcMs ?? this._cachedNowMs;

            record.Key = spec.Key;
            record.Channel = spec.Channel;
            record.State = TimerState.Running;
            record.StartMs = startMs;
            record.StageEnds = stageEnds;
            record.PausedAtMs = 0;
            record.DispatchedStage = 0;
            record.NextDeadlineMs = startMs + stageEnds[0];
            record.Sequence = this._sequenceCounter++;
            record.Listener = null;
            record.AutoRelease = spec.AutoRelease;
            record.CompletedAtMs = 0;
            record.CompletedDelivered = false;

            this._keyToIndex[spec.Key] = index;
            this._heap.Push(index);

            this._structureChangedThisTick = true;
            handle = new TimerHandle(index, record.Version);
            return true;
        }

        public bool TryGetHandle(string key, out TimerHandle handle)
        {
            if (key != null && this._keyToIndex.TryGetValue(key, out int index))
            {
                handle = new TimerHandle(index, this._records[index].Version);
                return true;
            }

            handle = TimerHandle.Invalid;
            return false;
        }

        private static long[] BuildStageEnds(in TimerSpec spec)
        {
            if (spec.StageDurationsMs != null && spec.StageDurationsMs.Length > 0)
            {
                long[] durations = spec.StageDurationsMs;
                long[] ends = new long[durations.Length];
                long cumulative = 0;

                for (int i = 0; i < durations.Length; i++)
                {
                    if (durations[i] <= 0)
                        return null;

                    cumulative += durations[i];
                    ends[i] = cumulative;
                }

                return ends;
            }

            if (spec.DurationMs <= 0)
                return null;

            return new[] { spec.DurationMs };
        }

        private int AcquireSlot()
        {
            if (this._freeList.Count > 0)
                return this._freeList.Pop();

            int oldCapacity = this._records.Length;
            int newCapacity = oldCapacity * 2;
            Array.Resize(ref this._records, newCapacity);
            this._heap.OnRecordsArrayReplaced(this._records);

            for (int i = oldCapacity; i < newCapacity; i++)
            {
                this._records[i] = new TimerRecord { HeapIndex = -1 };
                if (i != oldCapacity)
                    this._freeList.Push(i);
            }

            return oldCapacity;
        }

        // ------------------------------------------------------------------
        // Control
        // ------------------------------------------------------------------

        public void Pause(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record) || record.State != TimerState.Running)
                return;

            record.PausedAtMs = this._cachedNowMs;
            record.State = TimerState.Paused;
            this._heap.Remove(h.Index);
            this._structureChangedThisTick = true;
        }

        public void Resume(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record) || record.State != TimerState.Paused)
                return;

            long shift = this._cachedNowMs - record.PausedAtMs;
            record.StartMs += shift;
            record.NextDeadlineMs += shift;
            record.State = TimerState.Running;
            this._heap.Push(h.Index);
            this._structureChangedThisTick = true;
        }

        public void SpeedUp(TimerHandle h, long ms) => this.ShiftDeadlines(h, ms);

        public void Delay(TimerHandle h, long ms) => this.ShiftDeadlines(h, -ms);

        /// <summary>
        /// Shifts every remaining boundary by <paramref name="deltaMs"/>: positive moves
        /// <c>StartMs</c> earlier (speed up - elapsed time increases, deadlines arrive sooner),
        /// negative moves it later (delay).
        /// </summary>
        private void ShiftDeadlines(TimerHandle h, long deltaMs)
        {
            if (!this.TryResolve(h, out TimerRecord record))
                return;

            if (record.State != TimerState.Running && record.State != TimerState.Paused)
                return;

            record.StartMs -= deltaMs;

            if (record.State == TimerState.Running)
            {
                record.NextDeadlineMs -= deltaMs;
                this._heap.Update(h.Index);
            }

            this._structureChangedThisTick = true;
        }

        /// <summary>Shifts <c>StartMs</c> so the current stage ends exactly at <c>now</c>, then dispatches synchronously (see REWRITE_PLAN.md 6.1.2).</summary>
        public void SkipCurrentStage(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record) || record.State != TimerState.Running)
                return;

            int currentStage = this.ComputeCurrentStage(record, this._cachedNowMs);
            long currentStageEnd = record.StartMs + record.StageEnds[currentStage];
            long shift = currentStageEnd - this._cachedNowMs;

            record.StartMs -= shift;
            record.NextDeadlineMs -= shift;
            this._heap.Update(h.Index);

            this.DrainDueTimer(h.Index, allowMultiple: false);
            this._structureChangedThisTick = true;
        }

        /// <summary>Synchronously dispatches every remaining stage change and the final Completed.</summary>
        public void CompleteNow(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record) || record.State != TimerState.Running)
                return;

            long finalDeadline = record.StartMs + record.StageEnds[record.StageEnds.Length - 1];

            // Force every remaining boundary to be <= now by moving StartMs back just enough for the
            // final stage to land on now; earlier boundaries then fall before or at now too since
            // StageEnds is monotonically increasing.
            long shift = finalDeadline - this._cachedNowMs;
            if (shift > 0)
            {
                record.StartMs -= shift;
                record.NextDeadlineMs -= shift;
            }

            this._heap.Update(h.Index);
            this.DrainDueTimer(h.Index, allowMultiple: true);
            this._structureChangedThisTick = true;
        }

        public void Cancel(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record))
                return;

            if (record.State == TimerState.Running)
                this._heap.Remove(h.Index);

            if (record.State == TimerState.Completed)
                this._completedCount--;

            this.FreeSlot(h.Index);
            this._structureChangedThisTick = true;
        }

        public void Release(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record) || record.State != TimerState.Completed)
                return;

            this._completedCount--;
            this.FreeSlot(h.Index);
            this._structureChangedThisTick = true;
        }

        private void FreeSlot(int index)
        {
            TimerRecord record = this._records[index];
            if (record.Key != null)
                this._keyToIndex.Remove(record.Key);

            record.Version++;
            record.Reset();
            this._freeList.Push(index);
        }

        // ------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------

        public TimerState GetState(TimerHandle h) =>
            this.TryResolve(h, out TimerRecord record) ? record.State : TimerState.Free;

        public long GetRemainingMs(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record))
                return 0;

            long total = record.StageEnds[record.StageEnds.Length - 1];
            long elapsed = this.GetElapsedMsInternal(record);
            long remaining = total - elapsed;
            return remaining > 0 ? remaining : 0;
        }

        public long GetElapsedMs(TimerHandle h) =>
            this.TryResolve(h, out TimerRecord record) ? this.GetElapsedMsInternal(record) : 0;

        private long GetElapsedMsInternal(TimerRecord record)
        {
            long referenceNow = record.State == TimerState.Paused ? record.PausedAtMs : this._cachedNowMs;
            long elapsed = referenceNow - record.StartMs;
            long total = record.StageEnds[record.StageEnds.Length - 1];

            if (elapsed < 0) elapsed = 0;
            if (elapsed > total) elapsed = total;
            return elapsed;
        }

        public double GetProgress01(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record))
                return 0;

            long total = record.StageEnds[record.StageEnds.Length - 1];
            if (total <= 0)
                return 1;

            return (double)this.GetElapsedMsInternal(record) / total;
        }

        public long GetEndUtcMs(TimerHandle h) =>
            this.TryResolve(h, out TimerRecord record) ? record.StartMs + record.StageEnds[record.StageEnds.Length - 1] : 0;

        public int GetStageCount(TimerHandle h) =>
            this.TryResolve(h, out TimerRecord record) ? record.StageEnds.Length : 0;

        /// <summary>
        /// Current stage index (0-based), computed fresh from <c>now</c> by scanning
        /// <c>StageEnds</c> - not read from <c>DispatchedStage</c> - so it is correct even before the
        /// first Tick after Restore or while <c>ProcessingEnabled</c> is false (see REWRITE_PLAN.md 6.1.2).
        /// </summary>
        public int GetCurrentStage(TimerHandle h) =>
            this.TryResolve(h, out TimerRecord record) ? this.ComputeCurrentStage(record, this.ReferenceNow(record)) : 0;

        private long ReferenceNow(TimerRecord record) =>
            record.State == TimerState.Paused ? record.PausedAtMs : this._cachedNowMs;

        private int ComputeCurrentStage(TimerRecord record, long now)
        {
            long elapsed = now - record.StartMs;
            long[] ends = record.StageEnds;

            for (int i = 0; i < ends.Length; i++)
            {
                if (elapsed < ends[i])
                    return i;
            }

            return ends.Length - 1;
        }

        public long GetStageRemainingMs(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record))
                return 0;

            long now = this.ReferenceNow(record);
            int stage = this.ComputeCurrentStage(record, now);
            long stageEnd = record.StartMs + record.StageEnds[stage];
            long remaining = stageEnd - now;
            return remaining > 0 ? remaining : 0;
        }

        public long GetStageElapsedMs(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record))
                return 0;

            long now = this.ReferenceNow(record);
            int stage = this.ComputeCurrentStage(record, now);
            long stageStart = stage == 0 ? record.StartMs : record.StartMs + record.StageEnds[stage - 1];
            long elapsed = now - stageStart;
            return elapsed > 0 ? elapsed : 0;
        }

        public double GetStageProgress01(TimerHandle h)
        {
            if (!this.TryResolve(h, out TimerRecord record))
                return 0;

            long now = this.ReferenceNow(record);
            int stage = this.ComputeCurrentStage(record, now);
            long stageStart = stage == 0 ? record.StartMs : record.StartMs + record.StageEnds[stage - 1];
            long stageEnd = record.StartMs + record.StageEnds[stage];
            long stageDuration = stageEnd - stageStart;

            if (stageDuration <= 0)
                return 1;

            double elapsed = now - stageStart;
            return elapsed / stageDuration;
        }

        public long GetStageEndUtcMs(TimerHandle h, int stage)
        {
            if (!this.TryResolve(h, out TimerRecord record) || stage < 0 || stage >= record.StageEnds.Length)
                return 0;

            return record.StartMs + record.StageEnds[stage];
        }

        private bool TryResolve(TimerHandle h, out TimerRecord record)
        {
            if (h.IsValid && h.Index < this._records.Length)
            {
                TimerRecord candidate = this._records[h.Index];
                if (candidate.Version == h.Version && candidate.State != TimerState.Free)
                {
                    record = candidate;
                    return true;
                }
            }

            record = null;
            return false;
        }

        // ------------------------------------------------------------------
        // Listeners
        // ------------------------------------------------------------------

        public void SetListener(TimerHandle h, ITimerListener listener)
        {
            if (!this.TryResolve(h, out TimerRecord record))
                return;

            record.Listener = listener;
            if (listener != null)
                this.FlushUndeliveredFor(h.Index, listener);
        }

        public void AddChannelListener(int channel, ITimerListener listener)
        {
            this._channelListeners[channel] = listener;
            this.FlushUndeliveredForChannel(channel, listener);
        }

        public void RemoveChannelListener(int channel, ITimerListener listener)
        {
            if (this._channelListeners.TryGetValue(channel, out ITimerListener current) && current == listener)
                this._channelListeners.Remove(channel);
        }

        private void FlushUndeliveredFor(int recordIndex, ITimerListener listener) =>
            this.DeliverStoredPendingInOrder(recordIndex, listener, isChannelListener: false);

        private void FlushUndeliveredForChannel(int channel, ITimerListener listener)
        {
            List<int> indices = null;
            for (int i = 0; i < this._undelivered.Count; i++)
            {
                if (this._undelivered[i].Channel == channel)
                {
                    indices ??= new List<int>();
                    if (!indices.Contains(this._undelivered[i].Handle.Index))
                        indices.Add(this._undelivered[i].Handle.Index);
                }
            }

            if (indices == null)
                return;

            for (int i = 0; i < indices.Count; i++)
                this.DeliverStoredPendingInOrder(indices[i], listener, isChannelListener: true, channelFilter: channel);
        }

        private void DeliverStoredPendingInOrder(int recordIndex, ITimerListener listener, bool isChannelListener, int channelFilter = 0)
        {
            this._deliveryScratch.Clear();
            for (int i = 0; i < this._undelivered.Count; i++)
            {
                TimerEvent pending = this._undelivered[i];
                if (pending.Handle.Index != recordIndex)
                    continue;
                if (isChannelListener && pending.Channel != channelFilter)
                    continue;

                this._deliveryScratch.Add(pending);
            }

            if (this._deliveryScratch.Count == 0)
                return;

            for (int i = 0; i < this._deliveryScratch.Count; i++)
                this._undelivered.Remove(this._deliveryScratch[i]);

            for (int i = 0; i < this._deliveryScratch.Count; i++)
            {
                TimerEvent original = this._deliveryScratch[i];
                TimerEvent replay = new(
                    original.Handle, original.Key, original.Channel, original.Type, original.Stage,
                    original.AtMs, original.IsLate, isReplay: true);

                listener.OnTimerEvent(in replay);
                this.MarkDelivered(recordIndex, replay);
            }
        }

        // ------------------------------------------------------------------
        // Persistence
        // ------------------------------------------------------------------

        public void CaptureSnapshot(List<TimerEntrySnapshot> into)
        {
            if (into == null)
                return;

            into.Clear();

            for (int i = 0; i < this._records.Length; i++)
            {
                TimerRecord record = this._records[i];
                if (record.State == TimerState.Free)
                    continue;

                long[] stageEndsCopy = new long[record.StageEnds.Length];
                Array.Copy(record.StageEnds, stageEndsCopy, stageEndsCopy.Length);

                into.Add(new TimerEntrySnapshot
                {
                    Key = record.Key,
                    Channel = record.Channel,
                    State = record.State,
                    StartMs = record.StartMs,
                    DurationMs = stageEndsCopy[stageEndsCopy.Length - 1],
                    PausedAtMs = record.PausedAtMs,
                    StageEnds = stageEndsCopy,
                    DispatchedStage = record.DispatchedStage,
                    AutoRelease = record.AutoRelease,
                    CompletedAtMs = record.CompletedAtMs,
                    CompletedDelivered = record.CompletedDelivered
                });
            }
        }

        public void Restore(IReadOnlyList<TimerEntrySnapshot> entries)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                TimerEntrySnapshot entry = entries[i];

                if (string.IsNullOrEmpty(entry.Key) || entry.StageEnds == null || entry.StageEnds.Length == 0)
                {
                    this.OnWarning?.Invoke($"[TimerScheduler] Skipped corrupt snapshot entry (key='{entry.Key}').");
                    continue;
                }

                if (this._keyToIndex.ContainsKey(entry.Key))
                {
                    this.OnWarning?.Invoke($"[TimerScheduler] Skipped duplicate key on restore: '{entry.Key}'.");
                    continue;
                }

                int index = this.AcquireSlot();
                TimerRecord record = this._records[index];

                long[] stageEnds = new long[entry.StageEnds.Length];
                Array.Copy(entry.StageEnds, stageEnds, stageEnds.Length);

                record.Key = entry.Key;
                record.Channel = entry.Channel;
                record.StartMs = entry.StartMs;
                record.StageEnds = stageEnds;
                record.PausedAtMs = entry.PausedAtMs;
                record.DispatchedStage = entry.DispatchedStage;
                record.Sequence = this._sequenceCounter++;
                record.Listener = null;
                record.AutoRelease = entry.AutoRelease;
                record.CompletedAtMs = entry.CompletedAtMs;
                record.CompletedDelivered = entry.CompletedDelivered;

                this._keyToIndex[entry.Key] = index;

                if (entry.State == TimerState.Completed)
                {
                    record.State = TimerState.Completed;
                    record.NextDeadlineMs = long.MaxValue;
                    this._completedCount++;

                    if (!entry.CompletedDelivered)
                    {
                        // Replay: this was never confirmed delivered before the last save, so queue
                        // it again rather than silently dropping the notification.
                        int stage = record.StageEnds.Length;
                        long atMs = record.StartMs + record.StageEnds[stage - 1];
                        this.EnqueueUndelivered(new TimerEvent(
                            new TimerHandle(index, record.Version), record.Key, record.Channel,
                            TimerEventType.Completed, stage, atMs, isLate: true, isReplay: true));
                    }
                }
                else if (entry.State == TimerState.Paused)
                {
                    record.State = TimerState.Paused;
                    record.NextDeadlineMs = long.MaxValue;
                }
                else
                {
                    record.State = TimerState.Running;
                    int stage = record.DispatchedStage;
                    record.NextDeadlineMs = stage < record.StageEnds.Length
                        ? record.StartMs + record.StageEnds[stage]
                        : long.MaxValue;
                    this._heap.Push(index);
                }
            }

            this._structureChangedThisTick = true;
        }

        // ------------------------------------------------------------------
        // Tick
        // ------------------------------------------------------------------

        public void Tick(float deltaTime)
        {
            this._cachedNowMs = this._clock.UtcNowMs;
            this._structureChangedThisTick = false;

            int processed = 0;
            while (processed < this._maxEventsPerTick &&
                   this._heap.TryPeek(out int recordIndex) &&
                   this._records[recordIndex].NextDeadlineMs <= this._cachedNowMs)
            {
                this._heap.Pop();
                processed += this.ProcessDueRecord(recordIndex);
            }

            if (this._completedCount > this.CompletedWarningThreshold)
            {
                this.OnWarning?.Invoke(
                    $"[TimerScheduler] {this._completedCount} timers are Completed and unreleased " +
                    $"(threshold {this.CompletedWarningThreshold}). Call Release() once gameplay has consumed them.");
            }

            if (this._structureChangedThisTick)
                this.OnStructureChanged?.Invoke();
        }

        /// <summary>Pops and processes every boundary already due for one record within a single Tick, without re-peeking the heap in between (used by CompleteNow/SkipCurrentStage which pre-popped via DrainDueTimer, and by Tick's own loop).</summary>
        private int ProcessDueRecord(int recordIndex)
        {
            TimerRecord record = this._records[recordIndex];
            int eventsDispatched = 0;

            long deadline = record.NextDeadlineMs;
            record.DispatchedStage++;

            if (record.DispatchedStage < record.StageEnds.Length)
            {
                record.NextDeadlineMs = record.StartMs + record.StageEnds[record.DispatchedStage];
                this._heap.Push(recordIndex);

                this.Dispatch(new TimerEvent(
                    new TimerHandle(recordIndex, record.Version), record.Key, record.Channel,
                    TimerEventType.StageChanged, record.DispatchedStage, deadline,
                    isLate: this._cachedNowMs - deadline > LateThresholdMs, isReplay: false));

                eventsDispatched++;
            }
            else
            {
                record.State = TimerState.Completed;
                record.CompletedAtMs = deadline;
                record.NextDeadlineMs = long.MaxValue;
                this._completedCount++;

                // Dispatch() itself calls MarkDelivered() when the event reaches a listener, which
                // is what sets CompletedDelivered and, for AutoRelease timers, frees the slot - so
                // nothing further needs to happen here based on the return value.
                this.Dispatch(new TimerEvent(
                    new TimerHandle(recordIndex, record.Version), record.Key, record.Channel,
                    TimerEventType.Completed, record.DispatchedStage, deadline,
                    isLate: this._cachedNowMs - deadline > LateThresholdMs, isReplay: false));

                eventsDispatched++;
            }

            this._structureChangedThisTick = true;
            return eventsDispatched;
        }

        /// <summary>Drains every deadline already due for a single record, used by CompleteNow/SkipCurrentStage for synchronous processing outside the normal Tick loop.</summary>
        private void DrainDueTimer(int recordIndex, bool allowMultiple)
        {
            TimerRecord record;

            do
            {
                // ProcessDueRecord expects the record to already be out of the heap (Tick's own
                // loop pops before calling it), and it pushes the record back in itself when the
                // timer advances to a new stage rather than completing - so each pass here must
                // remove it again before re-processing, or the same index would end up pushed twice.
                if (this._records[recordIndex].HeapIndex >= 0)
                    this._heap.Remove(recordIndex);

                this.ProcessDueRecord(recordIndex);
                record = this._records[recordIndex];
            }
            while (allowMultiple && record.State == TimerState.Running && record.NextDeadlineMs <= this._cachedNowMs);
        }

        /// <summary>Dispatches to the per-timer listener first, then the channel listener, buffering if neither is present. Returns true if handed to at least one listener.</summary>
        private bool Dispatch(in TimerEvent e)
        {
            TimerRecord record = this._records[e.Handle.Index];
            bool delivered = false;

            if (record.Listener != null)
            {
                record.Listener.OnTimerEvent(in e);
                delivered = true;
            }

            if (this._channelListeners.TryGetValue(e.Channel, out ITimerListener channelListener))
            {
                channelListener.OnTimerEvent(in e);
                delivered = true;
            }

            if (!delivered)
                this.EnqueueUndelivered(e);
            else
                this.MarkDelivered(e.Handle.Index, e);

            return delivered;
        }

        private void EnqueueUndelivered(TimerEvent e) => this._undelivered.Add(e);

        private void MarkDelivered(int recordIndex, in TimerEvent e)
        {
            if (e.Type != TimerEventType.Completed)
                return;

            TimerRecord record = this._records[recordIndex];
            if (record.State != TimerState.Completed)
                return;

            record.CompletedDelivered = true;

            if (record.AutoRelease)
            {
                this._completedCount--;
                this.FreeSlot(recordIndex);
            }
        }
    }
}
