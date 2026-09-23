using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// The flat set of audio entries a game loads.
    /// </summary>
    /// <remarks>
    /// <para>This is the <i>curated</i> list: what the running game knows about. It is deliberately
    /// not the same question as "what entries exist on disk", which is what the generated
    /// <c>AudioId</c> class answers. Keeping them separate is what lets the editor tool point out
    /// an entry that was created but never registered, instead of hiding the mistake.</para>
    ///
    /// <para><see cref="Contains"/> is backed by a <see cref="HashSet{T}"/> rebuilt in
    /// <see cref="OnValidate"/>, so it stays O(1) whether the asset was edited through the
    /// Inspector or through <c>SerializedObject</c> (which triggers <c>OnValidate</c> on
    /// <c>ApplyModifiedProperties</c>).</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "AudioCollection", menuName = "DracoRuan/AudioSystem/AudioCollection")]
    public class AudioCollection : ScriptableObject
    {
        [ListDrawerSettings(ShowFoldout = true)] [SerializeField]
        private List<AudioEntry> entries = new();

        private HashSet<AudioEntry> _lookup;

        public List<AudioEntry> Entries => this.entries;

        /// <summary>
        /// Appends every non-null entry to <paramref name="destination"/>, skipping duplicates.
        /// </summary>
        public void CollectEntries(List<AudioEntry> destination)
        {
            if (destination == null)
                return;

            for (int i = 0; i < this.entries.Count; i++)
            {
                AudioEntry entry = this.entries[i];
                if (!entry || destination.Contains(entry))
                    continue;

                destination.Add(entry);
            }
        }

        /// <summary>Whether <paramref name="entry"/> is registered in this collection.</summary>
        public bool Contains(AudioEntry entry)
        {
            this._lookup ??= this.BuildLookup();
            return entry && this._lookup.Contains(entry);
        }

        private void OnValidate() => this._lookup = this.BuildLookup();

        private HashSet<AudioEntry> BuildLookup()
        {
            HashSet<AudioEntry> lookup = new(this.entries.Count);

            for (int i = 0; i < this.entries.Count; i++)
            {
                if (this.entries[i])
                    lookup.Add(this.entries[i]);
            }

            return lookup;
        }
    }
}