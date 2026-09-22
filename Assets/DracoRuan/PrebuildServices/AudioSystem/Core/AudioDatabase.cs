using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Logic;

namespace DracoRuan.PrebuildServices.AudioSystem.Core
{
    /// <summary>
    /// The runtime index over an <see cref="AudioCollection"/>: name in, entry out, in O(1).
    /// </summary>
    /// <remarks>
    /// <para>The <c>int</c> index this hands out is the key everything downstream uses. After one
    /// dictionary lookup at <c>Play</c>, the fire-rate gate, the concurrency counts and the clip
    /// leases are all array indexing, and no string is hashed again for the rest of the request.
    /// </para>
    ///
    /// <para>An entry passed directly to <c>Play</c> that is not in the collection still gets an
    /// index, assigned on first sight. Without that, throttling would silently stop working for
    /// exactly the sounds a designer wired up by dragging an asset into a field.</para>
    ///
    /// <para>The index is built by an explicit <see cref="Initialize"/> rather than in
    /// <c>OnEnable</c>, which fires at times the Editor chooses and not reliably in a build.</para>
    /// </remarks>
    public sealed class AudioDatabase
    {
        private readonly AudioIdRegistry _registry = new AudioIdRegistry();
        private readonly List<AudioEntry> _entries = new List<AudioEntry>();
        private readonly Dictionary<int, int> _transientIndexByInstanceId = new Dictionary<int, int>();

        /// <summary>How many entries are known, including any picked up at runtime.</summary>
        public int Count => this._entries.Count;

        /// <summary>Entries in index order.</summary>
        public IReadOnlyList<AudioEntry> Entries => this._entries;

        /// <summary>
        /// Indexes every entry in <paramref name="collection"/>.
        /// </summary>
        /// <exception cref="System.ArgumentException">
        /// Two entries share an id. The message names both owners, because "duplicate id 'Click'"
        /// on its own leaves you hunting the project for the other one.
        /// </exception>
        public void Initialize(AudioCollection collection)
        {
            this.Clear();

            if (collection == null)
                return;

            List<AudioEntry> collected = new List<AudioEntry>();
            collection.CollectEntries(collected);

            for (int i = 0; i < collected.Count; i++)
            {
                AudioEntry entry = collected[i];
                int index = this._registry.Add(entry.Id, entry.name);

                // Registry indices are dense and issued in order, so the two lists stay aligned.
                this._entries.Insert(index, entry);
            }
        }

        /// <summary>Looks up an entry by id.</summary>
        public bool TryGetEntry(string audioId, out AudioEntry entry)
        {
            if (this._registry.TryGetIndex(audioId, out int index))
            {
                entry = this._entries[index];
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>Looks up an entry's index by id.</summary>
        public bool TryGetIndex(string audioId, out int index) => this._registry.TryGetIndex(audioId, out index);

        /// <summary>The entry at <paramref name="index"/>.</summary>
        public AudioEntry GetEntry(int index) => this._entries[index];

        /// <summary>
        /// The index for <paramref name="entry"/>, registering it if this is the first time it has
        /// been seen.
        /// </summary>
        /// <remarks>
        /// Keyed by instance id rather than by the entry's audio id, because an ad-hoc entry may
        /// legitimately have no id yet and must still be throttled as its own sound.
        /// </remarks>
        public int GetOrRegisterTransient(AudioEntry entry)
        {
            if (entry == null)
                return -1;

            if (!string.IsNullOrEmpty(entry.Id) && this._registry.TryGetIndex(entry.Id, out int known))
                return known;

            int instanceId = entry.GetInstanceID();
            if (this._transientIndexByInstanceId.TryGetValue(instanceId, out int transient))
                return transient;

            int index = this._entries.Count;
            this._entries.Add(entry);
            this._transientIndexByInstanceId.Add(instanceId, index);

            return index;
        }

        /// <summary>Forgets everything. For service teardown.</summary>
        public void Clear()
        {
            this._registry.Clear();
            this._entries.Clear();
            this._transientIndexByInstanceId.Clear();
        }
    }
}
