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
        [SerializeField] private List<VibrationEntry> _entries = new();

        public IReadOnlyList<VibrationEntry> Entries => this._entries;

        /// <summary>Appends every non-null entry to <paramref name="destination"/>, skipping duplicates.</summary>
        public void CollectEntries(List<VibrationEntry> destination)
        {
            if (destination == null)
                return;

            for (int i = 0; i < this._entries.Count; i++)
            {
                VibrationEntry entry = this._entries[i];
                if (!entry || destination.Contains(entry))
                    continue;

                destination.Add(entry);
            }
        }

        /// <summary>Whether <paramref name="entry"/> is already in this collection.</summary>
        public bool Contains(VibrationEntry entry) => this._entries.Contains(entry);
    }
}
