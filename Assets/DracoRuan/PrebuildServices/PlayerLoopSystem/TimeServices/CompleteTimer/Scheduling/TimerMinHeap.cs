using System;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>
    /// Binary min-heap over <see cref="TimerRecord"/> indices, ordered by
    /// <c>(NextDeadlineMs, Sequence)</c>.
    /// </summary>
    /// <remarks>
    /// .NET Standard 2.1 (the API level Unity 6000.3 targets for this project) has no
    /// <c>System.Collections.Generic.PriorityQueue&lt;,&gt;</c> - that type only ships from .NET 6 -
    /// so this hand-rolled heap exists purely to fill that gap. It stores <c>int</c> record indices
    /// rather than the records themselves so removing/updating by identity is a plain array swap,
    /// and each record remembers its own <see cref="TimerRecord.HeapIndex"/> so <see cref="Remove"/>
    /// and <see cref="Update"/> are O(log N) instead of needing a linear search.
    /// </remarks>
    internal sealed class TimerMinHeap
    {
        private TimerRecord[] _records;
        private int[] _heap;
        private int _count;

        public TimerMinHeap(TimerRecord[] records, int initialCapacity)
        {
            this._records = records;
            this._heap = new int[Math.Max(4, initialCapacity)];
            this._count = 0;
        }

        /// <summary>
        /// Repoints the heap at the scheduler's records array after it was grown.
        /// <see cref="System.Array.Resize{T}"/> allocates a brand-new array rather than growing the
        /// existing one in place, so without this call the heap would keep comparing against a stale,
        /// undersized array the moment the scheduler grew past its initial capacity.
        /// </summary>
        public void OnRecordsArrayReplaced(TimerRecord[] records) => this._records = records;

        public int Count => this._count;

        public bool TryPeek(out int recordIndex)
        {
            if (this._count == 0)
            {
                recordIndex = -1;
                return false;
            }

            recordIndex = this._heap[0];
            return true;
        }

        public void Push(int recordIndex)
        {
            if (this._count == this._heap.Length)
                Array.Resize(ref this._heap, this._heap.Length * 2);

            int slot = this._count++;
            this._heap[slot] = recordIndex;
            this._records[recordIndex].HeapIndex = slot;
            this.SiftUp(slot);
        }

        public int Pop()
        {
            int top = this._heap[0];
            this._count--;

            if (this._count > 0)
            {
                this._heap[0] = this._heap[this._count];
                this._records[this._heap[0]].HeapIndex = 0;
                this.SiftDown(0);
            }

            this._records[top].HeapIndex = -1;
            return top;
        }

        /// <summary>Removes a record from the heap given its current heap slot; no-op if not in the heap.</summary>
        public void Remove(int recordIndex)
        {
            int slot = this._records[recordIndex].HeapIndex;
            if (slot < 0)
                return;

            this._count--;
            this._records[recordIndex].HeapIndex = -1;

            if (slot == this._count)
                return;

            this._heap[slot] = this._heap[this._count];
            this._records[this._heap[slot]].HeapIndex = slot;
            this.SiftDown(slot);
            this.SiftUp(slot);
        }

        /// <summary>Re-positions a record already in the heap after its deadline changed.</summary>
        public void Update(int recordIndex)
        {
            int slot = this._records[recordIndex].HeapIndex;
            if (slot < 0)
                return;

            this.SiftDown(slot);
            this.SiftUp(slot);
        }

        private bool IsLess(int a, int b)
        {
            TimerRecord ra = this._records[a];
            TimerRecord rb = this._records[b];

            if (ra.NextDeadlineMs != rb.NextDeadlineMs)
                return ra.NextDeadlineMs < rb.NextDeadlineMs;

            return ra.Sequence < rb.Sequence;
        }

        private void SiftUp(int slot)
        {
            while (slot > 0)
            {
                int parent = (slot - 1) / 2;
                if (!this.IsLess(this._heap[slot], this._heap[parent]))
                    break;

                this.Swap(slot, parent);
                slot = parent;
            }
        }

        private void SiftDown(int slot)
        {
            while (true)
            {
                int left = slot * 2 + 1;
                int right = left + 1;
                int smallest = slot;

                if (left < this._count && this.IsLess(this._heap[left], this._heap[smallest]))
                    smallest = left;

                if (right < this._count && this.IsLess(this._heap[right], this._heap[smallest]))
                    smallest = right;

                if (smallest == slot)
                    break;

                this.Swap(slot, smallest);
                slot = smallest;
            }
        }

        private void Swap(int slotA, int slotB)
        {
            (this._heap[slotA], this._heap[slotB]) = (this._heap[slotB], this._heap[slotA]);
            this._records[this._heap[slotA]].HeapIndex = slotA;
            this._records[this._heap[slotB]].HeapIndex = slotB;
        }
    }
}
