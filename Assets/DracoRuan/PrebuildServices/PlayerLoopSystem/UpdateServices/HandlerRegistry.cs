using System;
using System.Runtime.CompilerServices;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices
{
    /// <summary>
    /// Ordered handler list shared by <see cref="UpdateServiceManager"/> and <see cref="FixedUpdateServiceManager"/>.
    /// Safe to mutate from inside a tick: nothing is ever shifted while a pass is running, so no handler is skipped
    /// or ticked twice.
    /// <list type="bullet">
    /// <item>Registration goes to a pending list and joins the active list when the next pass begins; a handler
    /// registered during a pass therefore first ticks in the following one.</item>
    /// <item>Removal is O(1): the slot is nulled and nothing else moves, so deregistering a handler that has not
    /// ticked yet stops its tick. Holes are compacted once enough pile up.</item>
    /// <item>A handler is registered at most once, null is ignored, and removing an unknown handler is a no-op.</item>
    /// </list>
    /// Iterate <see cref="Slots"/> from <c>SlotCount - 1</c> down to 0 between <see cref="BeginTick"/> and
    /// <see cref="EndTick"/> and skip null slots. Not thread-safe; the managers enforce main-thread use.
    /// </summary>
    /// <remarks>
    /// Memory and speed: slots are a plain array (a <c>List</c> indexer costs more than the virtual call it feeds),
    /// and identity lookups use an open-addressing table of <c>int</c> slot numbers that takes its keys from the slot
    /// array, about 16 to 30 bytes per live handler all-in instead of the ~100 of a Dictionary-backed design.
    /// </remarks>
    internal sealed class HandlerRegistry<T> where T : class
    {
        private const int PendingInitialCapacity = 64;

        /// <summary>A single frame that registers more than about this many handlers (a level load, a big wave) has its scratch space released afterwards.</summary>
        private const int PendingTrimCapacity = 512;

        private const int MinHolesToCompact = 16;

        private readonly SlotList _active;
        private readonly SlotList _pending;
        private bool _isTicking;

        public HandlerRegistry(int capacity)
        {
            this._active = new SlotList(capacity);
            this._pending = new SlotList(PendingInitialCapacity);
        }

        /// <summary>
        /// The active slots, including null holes left by removals. The array is replaced only by
        /// <see cref="BeginTick"/>, never during a pass, so the reference and <see cref="SlotCount"/> read at the
        /// start of a pass stay valid throughout it.
        /// </summary>
        public T[] Slots => this._active.Items;

        /// <summary>Number of used entries in <see cref="Slots"/>, holes included.</summary>
        public int SlotCount => this._active.Count;

        /// <summary>Handlers that will tick in the next pass (active, not counting pending ones).</summary>
        public int ActiveCount => this._active.Count - this._active.Holes;

        public int PendingCount => this._pending.Count - this._pending.Holes;

        /// <summary>Bytes held by the backing arrays (slots and index of both lists); the registry's objects are negligible next to them.</summary>
        public long ArrayBytes => this._active.ArrayBytes + this._pending.ArrayBytes;

        public bool Register(T handler)
        {
            if (handler == null || this._active.IndexOf(handler) >= 0)
                return false;

            return this._pending.Add(handler);
        }

        public bool Deregister(T handler)
        {
            if (handler == null)
                return false;

            if (this._active.Remove(handler))
            {
                this.TryCompact(this._active);
                return true;
            }

            if (!this._pending.Remove(handler))
                return false;

            this.TryCompact(this._pending);
            return true;
        }

        /// <summary>Moves pending handlers into the active list. Returns false if a pass is already running.</summary>
        public bool BeginTick()
        {
            if (this._isTicking)
                return false;

            this._isTicking = true;

            T[] pending = this._pending.Items;
            int pendingCount = this._pending.Count;
            for (int i = 0; i < pendingCount; i++)
            {
                T handler = pending[i];
                if (handler != null)
                    this._active.Append(handler); // cannot already be active: Register checked
            }

            this._pending.ResetSlots();
            if (this._pending.Items.Length > PendingTrimCapacity)
                this._pending.Reallocate(PendingInitialCapacity);

            return true;
        }

        public void EndTick()
        {
            this._isTicking = false;
            this.TryCompact(this._active);
        }

        public void Clear()
        {
            this._pending.Clear();

            // A pass may be iterating the active list: null its slots instead of shrinking it underneath the loop.
            if (this._isTicking)
                this._active.NullAll();
            else
                this._active.Clear();
        }

        private void TryCompact(SlotList list)
        {
            // Never reshuffle the active list while a pass is iterating it; EndTick compacts afterwards.
            if (this._isTicking && ReferenceEquals(list, this._active))
                return;

            int holes = list.Holes;
            if (holes == 0)
                return;

            if (holes == list.Count)
                list.ResetSlots();
            else if (holes >= MinHolesToCompact && holes * 4 >= list.Count)
                list.Compact();
        }

        /// <summary>
        /// Insertion-ordered slots with O(1) removal by identity (null holes) and O(1) membership tests.
        /// <para>
        /// The index is a linear-probing table of <c>slot + 1</c> (0 = empty) keyed by the handler's identity hash. It
        /// stores no keys: an entry matches when the slot it names currently holds the handler being looked up. Removal
        /// only nulls the slot, which turns the entry into a tombstone that probing walks past; tombstones and entries
        /// left behind by <see cref="ResetSlots"/> are swept when the table is rebuilt. Cells are never emptied
        /// individually, so probe chains stay intact, and the table never holds more than half non-empty cells.
        /// </para>
        /// </summary>
        private sealed class SlotList
        {
            public T[] Items;
            public int Count;
            public int Holes;

            private int[] _table;
            private int _shift;
            private int _used;

            public SlotList(int capacity)
            {
                this.Items = new T[Math.Max(capacity, 4)];
                this.AllocateTable(TableSizeFor(capacity));
            }

            public long ArrayBytes => (long)this.Items.Length * IntPtr.Size + (long)this._table.Length * sizeof(int);

            public int IndexOf(T item)
            {
                int[] table = this._table;
                int mask = table.Length - 1;
                int cell = this.CellOf(item);

                while (true)
                {
                    int entry = table[cell];
                    if (entry == 0)
                        return -1;

                    int slot = entry - 1;
                    if (slot < this.Count && ReferenceEquals(this.Items[slot], item))
                        return slot;

                    cell = (cell + 1) & mask;
                }
            }

            public bool Add(T item)
            {
                if (this.IndexOf(item) >= 0)
                    return false;

                this.Append(item);
                return true;
            }

            /// <summary>Adds an item known not to be present.</summary>
            public void Append(T item)
            {
                if (this.Count == this.Items.Length)
                    Array.Resize(ref this.Items, this.Items.Length * 2);

                if ((this._used + 1) * 2 > this._table.Length)
                    this.RebuildTable(1);

                int slot = this.Count++;
                this.Items[slot] = item;
                this.InsertIntoTable(item, slot);
            }

            public bool Remove(T item)
            {
                int slot = this.IndexOf(item);
                if (slot < 0)
                    return false;

                this.Items[slot] = null;
                this.Holes++;
                return true;
            }

            /// <summary>
            /// Empties the slots once every item has been removed or moved elsewhere. The table is left alone (its
            /// entries now name slots that are empty or hold other items, which lookups ignore) so this costs only
            /// the slots in use, however large the table has grown.
            /// </summary>
            public void ResetSlots()
            {
                Array.Clear(this.Items, 0, this.Count);
                this.Count = 0;
                this.Holes = 0;
            }

            /// <summary>Drops the holes, keeping the order of the remaining items.</summary>
            public void Compact()
            {
                int write = 0;
                for (int read = 0; read < this.Count; read++)
                {
                    T item = this.Items[read];
                    if (item != null)
                        this.Items[write++] = item;
                }

                Array.Clear(this.Items, write, this.Count - write);
                this.Count = write;
                this.Holes = 0;
                this.RebuildTable(0);
            }

            /// <summary>Nulls every slot without changing <see cref="Count"/>, for use while a pass is iterating.</summary>
            public void NullAll()
            {
                Array.Clear(this.Items, 0, this.Count);
                this.Holes = this.Count;
            }

            public void Clear()
            {
                Array.Clear(this.Items, 0, this.Count);
                Array.Clear(this._table, 0, this._table.Length);
                this.Count = 0;
                this.Holes = 0;
                this._used = 0;
            }

            /// <summary>Replaces the arrays with fresh ones of the given capacity and discards the contents.</summary>
            public void Reallocate(int capacity)
            {
                this.Items = new T[Math.Max(capacity, 4)];
                this.Count = 0;
                this.Holes = 0;
                this.AllocateTable(TableSizeFor(capacity));
            }

            private int CellOf(T item) =>
                (int)(((uint)RuntimeHelpers.GetHashCode(item) * 2654435769u) >> this._shift);

            private void InsertIntoTable(T item, int slot)
            {
                int mask = this._table.Length - 1;
                int cell = this.CellOf(item);
                while (this._table[cell] != 0)
                    cell = (cell + 1) & mask;

                this._table[cell] = slot + 1;
                this._used++;
            }

            /// <summary>Re-indexes the live items into a table with room for <paramref name="extra"/> more, sweeping stale entries. The table only ever grows.</summary>
            private void RebuildTable(int extra)
            {
                int size = Math.Max(this._table.Length, TableSizeFor(this.Count - this.Holes + extra));
                if (size != this._table.Length)
                    this.AllocateTable(size);
                else
                    Array.Clear(this._table, 0, size);

                this._used = 0;
                for (int slot = 0; slot < this.Count; slot++)
                {
                    T item = this.Items[slot];
                    if (item != null)
                        this.InsertIntoTable(item, slot);
                }
            }

            private void AllocateTable(int size)
            {
                this._table = new int[size];
                this._used = 0;

                int bits = 0;
                while ((1 << bits) < size)
                    bits++;

                this._shift = 32 - bits;
            }

            /// <summary>Power of two giving a load of 0.2 to 0.4 for the given number of live items, so a rebuild buys many inserts.</summary>
            private static int TableSizeFor(int live)
            {
                int wanted = (live * 5 + 1) / 2 + 2;
                int size = 16;
                while (size < wanted)
                    size <<= 1;

                return size;
            }
        }
    }
}
