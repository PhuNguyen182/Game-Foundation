using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Production
{
    /// <summary>One item waiting for or currently occupying the queue's active slot.</summary>
    public readonly struct ProductionQueueItem
    {
        public ProductionQueueItem(string itemId, long durationMs)
        {
            this.ItemId = itemId;
            this.DurationMs = durationMs;
        }

        public string ItemId { get; }

        public long DurationMs { get; }
    }

    /// <summary>One item that finished production and is waiting to be claimed.</summary>
    public readonly struct ProductionQueueOutput
    {
        public ProductionQueueOutput(string itemId, long atMs)
        {
            this.ItemId = itemId;
            this.AtMs = atMs;
        }

        public string ItemId { get; }

        /// <summary>Absolute UTC ms moment this item finished.</summary>
        public long AtMs { get; }
    }

    /// <summary>
    /// FIFO production queue backed by exactly one <see cref="TimerScheduler"/> timer at a time - the
    /// item currently running.
    /// </summary>
    /// <remarks>
    /// <para><b>Only the active item owns a timer.</b> Queued items are plain data; the moment the
    /// active item completes, <see cref="OnTimerEvent"/> starts the next item with
    /// <c>StartUtcMs = e.AtMs</c> (the completed item's theoretical finish moment, not "now"). If that
    /// computed deadline is already in the past (offline catch-up), the newly started timer is
    /// already due and the scheduler pops it again within the very same <see cref="TimerScheduler.Tick"/>,
    /// so an entire offline queue can drain in one frame with every output's <see cref="ProductionQueueOutput.AtMs"/>
    /// exactly where it should be (see REWRITE_PLAN.md 6.1.3).</para>
    /// </remarks>
    public sealed class ProductionQueue : ITimerListener
    {
        private readonly TimerScheduler _scheduler;
        private readonly List<ProductionQueueItem> _items;
        private readonly List<ProductionQueueOutput> _outputs;

        private TimerHandle _activeHandle;
        private string _activeTimerKey;
        private long _sequence;
        private bool _isPaused;
        private long _pausedRemainingMs;

        public ProductionQueue(TimerScheduler scheduler, string queueKey, int capacity, int channel)
        {
            this._scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.QueueKey = queueKey ?? throw new ArgumentNullException(nameof(queueKey));
            this.Capacity = capacity;
            this.Channel = channel;

            this._items = new List<ProductionQueueItem>(capacity);
            this._outputs = new List<ProductionQueueOutput>(capacity);
            this._activeHandle = TimerHandle.Invalid;
        }

        public string QueueKey { get; }

        public int Capacity { get; }

        public int Channel { get; }

        /// <summary>Items waiting or currently running, in FIFO order; index 0 is the active item when a timer is running.</summary>
        public IReadOnlyList<ProductionQueueItem> Items => this._items;

        /// <summary>Finished items waiting to be claimed via <see cref="ClaimOutputs"/>.</summary>
        public IReadOnlyList<ProductionQueueOutput> Outputs => this._outputs;

        /// <summary>Handle of the timer driving the currently active item, or <see cref="TimerHandle.Invalid"/> if nothing is running.</summary>
        public TimerHandle ActiveHandle => this._activeHandle;

        public bool IsPaused => this._isPaused;

        /// <summary>Raised when an item finishes production and moves into <see cref="Outputs"/>.</summary>
        public event Action<ProductionQueue, string, long> OnItemCompleted;

        /// <summary>
        /// Enqueues an item. If the queue is idle (no active timer and not paused) it starts
        /// immediately; otherwise it waits behind whatever is already queued.
        /// </summary>
        public bool TryEnqueue(string itemId, long durationMs)
        {
            if (string.IsNullOrEmpty(itemId) || durationMs <= 0)
                return false;

            if (this._items.Count >= this.Capacity)
                return false;

            this._items.Add(new ProductionQueueItem(itemId, durationMs));

            if (this._items.Count == 1 && !this._isPaused)
                this.StartActive(startUtcMs: null);

            return true;
        }

        /// <summary>
        /// Removes the item at <paramref name="index"/>. If it was the active (running) item, the
        /// next queued item - if any - starts immediately at "now".
        /// </summary>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= this._items.Count)
                return;

            bool wasActive = index == 0 && this._activeHandle.IsValid;

            if (wasActive)
            {
                this._scheduler.Cancel(this._activeHandle);
                this._activeHandle = TimerHandle.Invalid;
                this._activeTimerKey = null;
            }

            this._items.RemoveAt(index);

            if (wasActive && this._items.Count > 0 && !this._isPaused)
                this.StartActive(startUtcMs: null);
        }

        public void Pause()
        {
            if (this._isPaused || !this._activeHandle.IsValid)
            {
                this._isPaused = true;
                return;
            }

            this._pausedRemainingMs = this._scheduler.GetRemainingMs(this._activeHandle);
            this._scheduler.Pause(this._activeHandle);
            this._isPaused = true;
        }

        public void Resume()
        {
            if (!this._isPaused)
                return;

            this._isPaused = false;

            if (this._activeHandle.IsValid)
                this._scheduler.Resume(this._activeHandle);
            else if (this._items.Count > 0)
                this.StartActive(startUtcMs: null);
        }

        public void SpeedUpActive(long ms)
        {
            if (this._activeHandle.IsValid)
                this._scheduler.SpeedUp(this._activeHandle, ms);
        }

        public void CompleteActiveNow()
        {
            if (this._activeHandle.IsValid)
                this._scheduler.CompleteNow(this._activeHandle);
        }

        /// <summary>Sum of the active item's remaining time plus the full duration of every queued item behind it.</summary>
        public long GetTotalRemainingMs()
        {
            long total = 0;

            if (this._activeHandle.IsValid)
                total += this._scheduler.GetRemainingMs(this._activeHandle);
            else if (this._isPaused && this._items.Count > 0)
                total += this._pausedRemainingMs;

            for (int i = 1; i < this._items.Count; i++)
                total += this._items[i].DurationMs;

            return total;
        }

        /// <summary>Moves every finished output into <paramref name="into"/> and clears them from the queue.</summary>
        public void ClaimOutputs(List<ProductionQueueOutput> into)
        {
            if (into == null)
                return;

            for (int i = 0; i < this._outputs.Count; i++)
                into.Add(this._outputs[i]);

            this._outputs.Clear();
        }

        private void StartActive(long? startUtcMs)
        {
            if (this._items.Count == 0)
                return;

            ProductionQueueItem item = this._items[0];
            string timerKey = this.QueueKey + "/" + this._sequence++;

            TimerSpec spec = new()
            {
                Key = timerKey,
                Channel = this.Channel,
                DurationMs = item.DurationMs,
                StartUtcMs = startUtcMs,
                AutoRelease = true
            };

            if (this._scheduler.TryStart(in spec, out TimerHandle handle))
            {
                this._activeHandle = handle;
                this._activeTimerKey = timerKey;
                this._scheduler.SetListener(handle, this);
            }
        }

        void ITimerListener.OnTimerEvent(in TimerEvent e)
        {
            if (e.Type != TimerEventType.Completed)
                return;

            if (this._items.Count == 0)
                return;

            ProductionQueueItem finished = this._items[0];
            this._items.RemoveAt(0);
            this._activeHandle = TimerHandle.Invalid;
            this._activeTimerKey = null;

            this._outputs.Add(new ProductionQueueOutput(finished.ItemId, e.AtMs));
            this.OnItemCompleted?.Invoke(this, finished.ItemId, e.AtMs);

            if (this._items.Count > 0 && !this._isPaused)
                this.StartActive(startUtcMs: e.AtMs);
        }

        // ------------------------------------------------------------------
        // Persistence (used by ProductionQueueRegistry)
        // ------------------------------------------------------------------

        internal ProductionQueueSnapshot Capture()
        {
            ProductionQueueItemSnapshot[] items = new ProductionQueueItemSnapshot[this._items.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = new ProductionQueueItemSnapshot { ItemId = this._items[i].ItemId, DurationMs = this._items[i].DurationMs };

            ProductionQueueOutputSnapshot[] outputs = new ProductionQueueOutputSnapshot[this._outputs.Count];
            for (int i = 0; i < outputs.Length; i++)
                outputs[i] = new ProductionQueueOutputSnapshot { ItemId = this._outputs[i].ItemId, AtMs = this._outputs[i].AtMs };

            return new ProductionQueueSnapshot
            {
                QueueKey = this.QueueKey,
                Capacity = this.Capacity,
                Channel = this.Channel,
                Items = items,
                Outputs = outputs,
                ActiveTimerKey = this._activeHandle.IsValid ? this._activeTimerKey : null,
                Sequence = this._sequence,
                IsPaused = this._isPaused
            };
        }

        internal void Restore(ProductionQueueSnapshot snapshot)
        {
            this._items.Clear();
            if (snapshot.Items != null)
            {
                for (int i = 0; i < snapshot.Items.Length; i++)
                    this._items.Add(new ProductionQueueItem(snapshot.Items[i].ItemId, snapshot.Items[i].DurationMs));
            }

            this._outputs.Clear();
            if (snapshot.Outputs != null)
            {
                for (int i = 0; i < snapshot.Outputs.Length; i++)
                    this._outputs.Add(new ProductionQueueOutput(snapshot.Outputs[i].ItemId, snapshot.Outputs[i].AtMs));
            }

            this._sequence = snapshot.Sequence;
            this._isPaused = snapshot.IsPaused;
            this._activeHandle = TimerHandle.Invalid;
            this._activeTimerKey = null;

            if (!string.IsNullOrEmpty(snapshot.ActiveTimerKey) &&
                this._scheduler.TryGetHandle(snapshot.ActiveTimerKey, out TimerHandle handle))
            {
                this._activeHandle = handle;
                this._activeTimerKey = snapshot.ActiveTimerKey;
                this._scheduler.SetListener(handle, this);
            }
        }
    }
}
