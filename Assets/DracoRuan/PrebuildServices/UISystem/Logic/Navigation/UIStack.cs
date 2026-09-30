using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.UISystem.Logic.DracoRuan.PrebuildServices.UISystem.Logic.Navigation
{
    /// <summary>
    /// Generic push/pop/replace/popToRoot stack for screen navigation. Engine-agnostic:
    /// callers push whatever handle type (e.g. a view-model instance) represents an
    /// open screen.
    /// </summary>
    public sealed class UIStack<T>
    {
        private readonly List<T> _items = new();

        public int Count => this._items.Count;
        public bool IsEmpty => this._items.Count == 0;
        public IReadOnlyList<T> Items => this._items;

        public bool TryPeek(out T current)
        {
            if (this._items.Count == 0)
            {
                current = default;
                return false;
            }

            current = this._items[^1];
            return true;
        }

        public void Push(T item) => this._items.Add(item);

        public bool TryPop(out T popped)
        {
            if (this._items.Count == 0)
            {
                popped = default;
                return false;
            }

            int lastIndex = this._items.Count - 1;
            popped = this._items[lastIndex];
            this._items.RemoveAt(lastIndex);
            return true;
        }

        /// <summary>
        /// Swaps the top item without growing history (e.g. Loading -> Home). If the stack is
        /// empty there is nothing to swap: the item is pushed as the new root instead, and this
        /// returns false so callers can tell "seeded a root" apart from "replaced an existing
        /// top" — it never leaves the caller needing to Push separately.
        /// </summary>
        public bool TryReplace(T item, out T replaced)
        {
            if (this._items.Count == 0)
            {
                replaced = default;
                this._items.Add(item);
                return false;
            }

            int lastIndex = this._items.Count - 1;
            replaced = this._items[lastIndex];
            this._items[lastIndex] = item;
            return true;
        }

        /// <summary>
        /// Removes a specific item regardless of its position (not just the top), e.g. force-
        /// closing one screen out of several stacked ones when its owning scope tears down.
        /// Uses default equality, so reference types are matched by reference. Returns false if
        /// the item isn't in the stack.
        /// </summary>
        public bool Remove(T item)
        {
            int index = this._items.IndexOf(item);
            if (index < 0)
                return false;

            this._items.RemoveAt(index);
            return true;
        }

        /// <summary>Pops every item except the root, returned top-first (LIFO pop order).</summary>
        public IReadOnlyList<T> PopToRoot()
        {
            var popped = new List<T>();
            while (this._items.Count > 1)
            {
                int lastIndex = this._items.Count - 1;
                popped.Add(this._items[lastIndex]);
                this._items.RemoveAt(lastIndex);
            }

            return popped;
        }
    }
}
