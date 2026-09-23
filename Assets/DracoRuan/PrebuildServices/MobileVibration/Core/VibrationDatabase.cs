using System.Collections.Generic;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Logic;

namespace DracoRuan.PrebuildServices.MobileVibration.Core
{
    /// <summary>
    /// The runtime index over a <see cref="VibrationCollection"/>: id in, entry out, in O(1).
    /// </summary>
    /// <remarks>
    /// <para>The <c>int</c> index this hands out is what the cooldown gate keys on, so a play costs
    /// one dictionary lookup and then array indexing for the rest of the request — the same shape as
    /// <c>AudioDatabase</c>.</para>
    ///
    /// <para>Unlike <c>AudioDatabase</c>, there is no <c>GetOrRegisterTransient</c>: the only entry
    /// point is <c>Play(string vibrationId)</c>, so every entry that can ever play is already in the
    /// collection by the time <see cref="Initialize"/> runs.</para>
    ///
    /// <para>The index is built by an explicit <see cref="Initialize"/> rather than in
    /// <c>OnEnable</c>, which fires at times the Editor chooses and not reliably in a build.</para>
    /// </remarks>
    public sealed class VibrationDatabase
    {
        private readonly VibrationIdRegistry _registry = new VibrationIdRegistry();
        private readonly List<VibrationEntry> _entries = new List<VibrationEntry>();

        /// <summary>How many entries are indexed.</summary>
        public int Count => this._entries.Count;

        /// <summary>Entries in index order.</summary>
        public IReadOnlyList<VibrationEntry> Entries => this._entries;

        /// <summary>
        /// Indexes every entry in <paramref name="collection"/>.
        /// </summary>
        /// <exception cref="System.ArgumentException">Two entries share an id.</exception>
        public void Initialize(VibrationCollection collection)
        {
            this.Clear();

            if (collection == null)
                return;

            List<VibrationEntry> collected = new List<VibrationEntry>();
            collection.CollectEntries(collected);

            for (int i = 0; i < collected.Count; i++)
            {
                VibrationEntry entry = collected[i];
                int index = this._registry.Add(entry.Id, entry.name);

                // Registry indices are dense and issued in order, so the two lists stay aligned.
                this._entries.Insert(index, entry);
            }
        }

        /// <summary>Looks up an entry by id.</summary>
        public bool TryGetEntry(string vibrationId, out VibrationEntry entry)
        {
            if (this._registry.TryGetIndex(vibrationId, out int index))
            {
                entry = this._entries[index];
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>Looks up an entry's index by id.</summary>
        public bool TryGetIndex(string vibrationId, out int index) =>
            this._registry.TryGetIndex(vibrationId, out index);

        /// <summary>The entry at <paramref name="index"/>.</summary>
        public VibrationEntry GetEntry(int index) => this._entries[index];

        /// <summary>Forgets everything. For service teardown.</summary>
        public void Clear()
        {
            this._registry.Clear();
            this._entries.Clear();
        }
    }
}
