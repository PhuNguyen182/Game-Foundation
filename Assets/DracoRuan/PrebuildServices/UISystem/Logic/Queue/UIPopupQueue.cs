using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.UISystem.Logic
{
    /// <summary>
    /// Priority queue with FIFO tie-breaking: higher priority dequeues first, and items
    /// enqueued earlier at the same priority dequeue before later ones. Can be paused
    /// (e.g. during a tutorial/cutscene) without losing already-queued items.
    /// </summary>
    public sealed class UIPopupQueue<T>
    {
        private struct Entry
        {
            public T Item;
            public int Priority;
            public long Sequence;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private long _sequenceCounter;

        public bool IsPaused { get; private set; }
        public int Count => this._entries.Count;

        public void Enqueue(T item, int priority = 0)
        {
            this._entries.Add(new Entry { Item = item, Priority = priority, Sequence = this._sequenceCounter++ });
        }

        public bool TryDequeue(out T item)
        {
            item = default;
            if (this.IsPaused || this._entries.Count == 0)
                return false;

            int bestIndex = 0;
            for (int i = 1; i < this._entries.Count; i++)
            {
                Entry candidate = this._entries[i];
                Entry best = this._entries[bestIndex];
                bool candidateWins = candidate.Priority > best.Priority ||
                                      (candidate.Priority == best.Priority && candidate.Sequence < best.Sequence);
                if (candidateWins)
                    bestIndex = i;
            }

            item = this._entries[bestIndex].Item;
            this._entries.RemoveAt(bestIndex);
            return true;
        }

        public void Pause() => this.IsPaused = true;
        public void Resume() => this.IsPaused = false;
    }
}
