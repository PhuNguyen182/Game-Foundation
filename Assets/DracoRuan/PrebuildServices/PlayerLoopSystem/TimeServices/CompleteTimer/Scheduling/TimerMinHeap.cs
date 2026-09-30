using System;
using System.Runtime.CompilerServices;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer.Scheduling
{
    /// <summary>
    /// Binary min-heap over <see cref="TimerRecord"/> indices, ordered by
    /// <c>(NextDeadlineMs, Sequence)</c>.
    /// </summary>
    /// <remarks>
    /// .NET Standard 2.1 (the API level Unity 6000.3 targets for this project) has no
    /// <c>System.Collections.Generic.PriorityQueue&lt;,&gt;</c> - that type only ships from .NET 6 -
    /// so this hand-rolled heap exists purely to fill that gap.
    /// <para>
    /// Each heap node carries a copy of its sort key, and the record-index to heap-slot map lives in a
    /// separate <c>int[]</c>, so sifting touches only those two contiguous arrays and never
    /// dereferences a <see cref="TimerRecord"/>. With 100,000 timers the records are scattered across the
    /// heap, so chasing them on every comparison of a deep sift was the dominant cost of mass completion.
    /// </para>
    /// <para>
    /// The cached key is taken from the record when it is <see cref="Push"/>ed or <see cref="Update"/>d,
    /// so the scheduler must call <see cref="Update"/> after changing <c>NextDeadlineMs</c> of a
    /// record that is in the heap.
    /// </para>
    /// </remarks>
    internal sealed class TimerMinHeap
    {
        /// <summary>
        /// Deadline plus record index, 16 bytes. The tie-break <c>Sequence</c> is deliberately not cached
        /// here: it is only consulted when two deadlines are exactly equal, so reading it from the record
        /// on those (rarer) comparisons keeps every node a third smaller.
        /// </summary>
        private struct Node
        {
            public long Deadline;
            public int Index;
        }

        private TimerRecord[] _records;
        private Node[] _nodes;

        /// <summary>Heap slot of each record index, or -1 when the record is not in the heap.</summary>
        private int[] _positions;

        private int _count;

        public TimerMinHeap(TimerRecord[] records, int initialCapacity)
        {
            this._records = records;
            this._nodes = new Node[Math.Max(4, initialCapacity)];
            this._positions = new int[records.Length];
            this._count = 0;

            for (int i = 0; i < this._positions.Length; i++)
                this._positions[i] = -1;
        }

        /// <summary>
        /// Repoints the heap at the scheduler's records array after it was grown.
        /// <see cref="System.Array.Resize{T}"/> allocates a brand-new array rather than growing the
        /// existing one in place, so without this call the heap would keep comparing against a stale,
        /// undersized array the moment the scheduler grew past its initial capacity.
        /// </summary>
        public void OnRecordsArrayReplaced(TimerRecord[] records)
        {
            this._records = records;

            int oldLength = this._positions.Length;
            if (records.Length <= oldLength)
                return;

            Array.Resize(ref this._positions, records.Length);
            for (int i = oldLength; i < this._positions.Length; i++)
                this._positions[i] = -1;
        }

        public int Count => this._count;

        /// <summary>True when the record currently sits in the heap.</summary>
        public bool Contains(int recordIndex) => this._positions[recordIndex] >= 0;

        public bool TryPeek(out int recordIndex, out long deadlineMs)
        {
            if (this._count == 0)
            {
                recordIndex = -1;
                deadlineMs = 0;
                return false;
            }

            recordIndex = this._nodes[0].Index;
            deadlineMs = this._nodes[0].Deadline;
            return true;
        }

        public void Push(int recordIndex)
        {
            if (this._count == this._nodes.Length)
                Array.Resize(ref this._nodes, this._nodes.Length * 2);

            this.SiftUp(this._count++, this.MakeNode(recordIndex));
        }

        public int Pop()
        {
            int top = this._nodes[0].Index;
            this._positions[top] = -1;
            this._count--;

            if (this._count > 0)
                this.SiftDown(0, this._nodes[this._count]);

            return top;
        }

        /// <summary>Removes a record from the heap given its current heap slot; no-op if not in the heap.</summary>
        public void Remove(int recordIndex)
        {
            int slot = this._positions[recordIndex];
            if (slot < 0)
                return;

            this._positions[recordIndex] = -1;
            this._count--;

            if (slot == this._count)
                return;

            this.Reposition(slot, this._nodes[this._count]);
        }

        /// <summary>Re-positions a record already in the heap after its deadline changed.</summary>
        public void Update(int recordIndex)
        {
            int slot = this._positions[recordIndex];
            if (slot < 0)
                return;

            this.Reposition(slot, this.MakeNode(recordIndex));
        }

        private Node MakeNode(int recordIndex)
        {
            TimerRecord record = this._records[recordIndex];
            return new Node { Deadline = record.NextDeadlineMs, Index = recordIndex };
        }

        /// <summary>Places <paramref name="node"/> at <paramref name="slot"/>, moving it up or down to where it belongs.</summary>
        private void Reposition(int slot, in Node node)
        {
            if (slot > 0 && this.IsLess(in node, in this._nodes[(slot - 1) >> 1]))
                this.SiftUp(slot, node);
            else
                this.SiftDown(slot, node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLess(in Node a, in Node b) =>
            a.Deadline != b.Deadline
                ? a.Deadline < b.Deadline
                : this._records[a.Index].Sequence < this._records[b.Index].Sequence;

        /// <summary>Moves the hole at <paramref name="slot"/> up until <paramref name="node"/> fits, then writes it.</summary>
        private void SiftUp(int slot, Node node)
        {
            while (slot > 0)
            {
                int parent = (slot - 1) >> 1;
                if (!this.IsLess(in node, in this._nodes[parent]))
                    break;

                this._nodes[slot] = this._nodes[parent];
                this._positions[this._nodes[slot].Index] = slot;
                slot = parent;
            }

            this._nodes[slot] = node;
            this._positions[node.Index] = slot;
        }

        /// <summary>Moves the hole at <paramref name="slot"/> down until <paramref name="node"/> fits, then writes it.</summary>
        private void SiftDown(int slot, Node node)
        {
            int count = this._count;

            while (true)
            {
                int child = slot * 2 + 1;
                if (child >= count)
                    break;

                int right = child + 1;
                if (right < count && this.IsLess(in this._nodes[right], in this._nodes[child]))
                    child = right;

                if (!this.IsLess(in this._nodes[child], in node))
                    break;

                this._nodes[slot] = this._nodes[child];
                this._positions[this._nodes[slot].Index] = slot;
                slot = child;
            }

            this._nodes[slot] = node;
            this._positions[node.Index] = slot;
        }
    }
}