using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.UISystem.Logic
{
    /// <summary>
    /// Hands out increasing sort-order slots for nested canvases within a UI layer.
    /// Releasing the current top slot shrinks the allocator back (stack behavior), so
    /// closing views in LIFO order reuses slots immediately instead of growing forever.
    /// Out-of-order releases are kept in a free list and reused before growing.
    /// </summary>
    public sealed class SortOrderAllocator
    {
        private readonly int _baseSortOrder;
        private readonly int _step;
        private readonly List<int> _freeSlots = new List<int>();
        private int _nextSlot;

        public SortOrderAllocator(int baseSortOrder, int step)
        {
            if (step <= 0)
                throw new ArgumentOutOfRangeException(nameof(step), "step must be positive.");

            this._baseSortOrder = baseSortOrder;
            this._step = step;
            this._nextSlot = baseSortOrder;
        }

        public int Allocate()
        {
            if (this._freeSlots.Count > 0)
            {
                int lastIndex = this._freeSlots.Count - 1;
                int reused = this._freeSlots[lastIndex];
                this._freeSlots.RemoveAt(lastIndex);
                return reused;
            }

            int allocated = this._nextSlot;
            this._nextSlot += this._step;
            return allocated;
        }

        public void Release(int sortOrder)
        {
            int currentTop = this._nextSlot - this._step;
            if (sortOrder != currentTop)
            {
                this._freeSlots.Add(sortOrder);
                return;
            }

            this._nextSlot = currentTop;
            while (this._nextSlot > this._baseSortOrder && this._freeSlots.Remove(this._nextSlot - this._step))
                this._nextSlot -= this._step;
        }
    }
}
