using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// The set of audio entries a game loads, grouped into categories.
    /// </summary>
    /// <remarks>
    /// <para>This is the <i>curated</i> list: what the running game knows about. It is deliberately
    /// not the same question as "what entries exist on disk", which is what the generated
    /// <c>AudioId</c> class answers. Keeping them separate is what lets the editor tool point out
    /// an entry that was created but never registered, instead of hiding the mistake.</para>
    ///
    /// <para>No index is built here. Building it in <c>OnEnable</c> would run at unpredictable times
    /// in the Editor, so the service asks for one explicitly during its own initialisation.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "AudioCollection", menuName = "DracoRuan/AudioSystem/AudioCollection")]
    public class AudioCollection : ScriptableObject
    {
        [ListDrawerSettings(ShowFoldout = true)]
        [SerializeField] private List<AudioCategory> categories = new();

        public List<AudioCategory> Categories => this.categories;

        /// <summary>
        /// Appends every non-null entry to <paramref name="destination"/>, skipping duplicates
        /// caused by the same asset being filed under two categories.
        /// </summary>
        public void CollectEntries(List<AudioEntry> destination)
        {
            if (destination == null)
                return;

            for (int categoryIndex = 0; categoryIndex < this.categories.Count; categoryIndex++)
            {
                List<AudioEntry> entries = this.categories[categoryIndex]?.Entries;
                if (entries == null)
                    continue;

                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    AudioEntry entry = entries[entryIndex];
                    if (!entry || destination.Contains(entry))
                        continue;

                    destination.Add(entry);
                }
            }
        }

        /// <summary>Whether <paramref name="entry"/> is filed under any category.</summary>
        public bool Contains(AudioEntry entry)
        {
            for (int categoryIndex = 0; categoryIndex < this.categories.Count; categoryIndex++)
            {
                List<AudioEntry> entries = this.categories[categoryIndex]?.Entries;
                if (entries != null && entries.Contains(entry))
                    return true;
            }

            return false;
        }
    }
}
