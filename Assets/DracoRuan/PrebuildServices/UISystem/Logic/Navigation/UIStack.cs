using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.UISystem.Logic
{
    /// <summary>
    /// Generic push/pop/replace/popToRoot stack for screen navigation. Engine-agnostic:
    /// callers push whatever handle type (e.g. a view-model instance) represents an
    /// open screen.
    /// </summary>
    public sealed class UIStack<T>
    {
        private readonly List<T> _items = new List<T>();

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

            current = this._items[this._items.Count - 1];
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

        /// <summary>Swaps the top item without growing history (e.g. Loading -> Home).</summary>
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
