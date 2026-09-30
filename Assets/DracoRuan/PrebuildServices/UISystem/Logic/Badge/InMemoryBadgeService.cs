using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.UISystem.Logic.DracoRuan.PrebuildServices.UISystem.Logic.Badge
{
    /// <summary>
    /// Default IBadgeService: counts held in memory only, lost on process exit. This is the
    /// reference implementation the abstraction ships with, not the only one a game may need -
    /// a save-file-backed or server-synced IBadgeService is a separate class entirely, written
    /// against the same interface.
    /// </summary>
    public sealed class InMemoryBadgeService : IBadgeService
    {
        private readonly Dictionary<BadgeKey, int> _ownCounts = new();

        public event Action<BadgeKey> Changed;

        public void SetCount(BadgeKey key, int count)
        {
            int clamped = Math.Max(0, count);

            if (this._ownCounts.TryGetValue(key, out int existing) && existing == clamped)
                return;

            if (clamped == 0)
                this._ownCounts.Remove(key);
            else
                this._ownCounts[key] = clamped;

            this.Changed?.Invoke(key);
        }

        public int GetCount(BadgeKey key)
        {
            int total = 0;
            foreach (KeyValuePair<BadgeKey, int> pair in this._ownCounts)
            {
                if (key.IsAncestorOrSelfOf(pair.Key))
                    total += pair.Value;
            }

            return total;
        }

        public bool HasAny(BadgeKey key) => this.GetCount(key) > 0;
    }
}
