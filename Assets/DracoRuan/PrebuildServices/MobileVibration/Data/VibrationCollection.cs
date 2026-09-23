using System;
using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Data
{
    /// <summary>
    /// The set of vibration entries a game loads.
    /// </summary>
    /// <remarks>
    /// <para>Flat, unlike <c>AudioCollection</c>'s categories: haptics realistically number a few
    /// dozen entries at most, so grouping them by category would be over-engineering at this scale.
    /// </para>
    ///
    /// <para>This is the <i>curated</i> list: what the running game knows about. It is deliberately
    /// not the same question as "what entries exist on disk", which is what the generated
    /// <c>VibrationId</c> class answers. Keeping them separate is what lets the editor tool point out
    /// an entry that was created but never registered, instead of hiding the mistake.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "VibrationCollection", menuName = "DracoRuan/MobileVibration/VibrationCollection")]
    public class VibrationCollection : ScriptableObject
    {
        [SerializeField] private List<VibrationEntry> entries = new();

        // Rebuilt lazily from `entries` on the next lookup after something changes it, rather than
        // eagerly in OnValidate: OnValidate can fire repeatedly while a list is being dragged in the
        // Inspector, and nothing here needs the index until a lookup actually happens.
        [NonSerialized] private Dictionary<string, VibrationEntry> _byId;
        [NonSerialized] private bool _isIndexDirty = true;

        public IReadOnlyList<VibrationEntry> Entries => this.entries;

        private void OnValidate() => this.InvalidateIndex();

        /// <summary>
        /// Marks the id index stale. <see cref="OnValidate"/> covers edits made through the Inspector;
        /// this is for editor code that mutates <c>entries</c> directly through a
        /// <see cref="UnityEditor.SerializedObject"/> (as <c>VibrationDatabaseLocator.Register</c> and
        /// <c>Unregister</c> do), which is not guaranteed to trigger <c>OnValidate</c> in every Editor
        /// version the same way a hand-edit in the Inspector does.
        /// </summary>
        public void InvalidateIndex() => this._isIndexDirty = true;

        /// <summary>Appends every non-null entry to <paramref name="destination"/>, skipping duplicates.</summary>
        public void CollectEntries(List<VibrationEntry> destination)
        {
            if (destination == null)
                return;

            for (int i = 0; i < this.entries.Count; i++)
            {
                VibrationEntry entry = this.entries[i];
                if (!entry || destination.Contains(entry))
                    continue;

                destination.Add(entry);
            }
        }

        /// <summary>Whether <paramref name="entry"/> is already in this collection. O(1) on the entry's id.</summary>
        public bool Contains(VibrationEntry entry) =>
            entry && this.TryGetById(entry.Id, out VibrationEntry found) && ReferenceEquals(found, entry);

        /// <summary>Looks up an entry by id. O(1) once the index has been built.</summary>
        /// <remarks>
        /// Editor-facing convenience, not what the running game plays by: that path goes through
        /// <c>VibrationDatabase</c>, which indexes by the same id but is built once at startup and
        /// never rescans an asset that might have changed underneath it.
        /// </remarks>
        public bool TryGetById(string id, out VibrationEntry entry)
        {
            this.EnsureIndexBuilt();
            return this._byId.TryGetValue(id ?? string.Empty, out entry);
        }

        private void EnsureIndexBuilt()
        {
            if (!this._isIndexDirty && this._byId != null)
                return;

            this._byId = new Dictionary<string, VibrationEntry>(this.entries.Count, StringComparer.Ordinal);

            for (int i = 0; i < this.entries.Count; i++)
            {
                VibrationEntry entry = this.entries[i];

                // A blank or duplicate id is a data problem the id generator and its conflict banner
                // already surface; TryAdd just keeps the first entry that claims an id rather than
                // throwing, so a bad asset does not also break every other lookup.
                if (entry && !string.IsNullOrEmpty(entry.Id))
                    this._byId.TryAdd(entry.Id, entry);
            }

            this._isIndexDirty = false;
        }
    }
}