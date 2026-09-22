using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.AudioSystem.Logic
{
    /// <summary>
    /// The string-to-index table behind O(1) entry lookup.
    /// </summary>
    /// <remarks>
    /// <para>The index it hands out, not the id, is what the rest of the service keys on: fire-rate
    /// timestamps, concurrency counts and clip leases are all arrays indexed by it. One dictionary
    /// lookup per <c>Play</c> therefore buys an array index for everything downstream, and no
    /// string is hashed again for the life of that request.</para>
    ///
    /// <para>A duplicate id throws instead of overwriting, and the message names both owners, the
    /// way <c>StaticDataRegistry</c> reports the same mistake. Silently overwriting would leave one
    /// of the two sounds permanently unreachable with nothing to show for it.</para>
    /// </remarks>
    public sealed class AudioIdRegistry
    {
        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _ids = new List<string>();
        private readonly List<string> _owners = new List<string>();

        /// <summary>How many ids are registered.</summary>
        public int Count => this._ids.Count;

        /// <summary>
        /// Registers <paramref name="audioId"/> and returns the index it will be known by.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The id breaks <see cref="AudioIdFormat"/>, or another owner already registered it.
        /// </exception>
        public int Add(string audioId, string ownerPath)
        {
            AudioIdFormat.Validate(audioId);

            if (this._indexById.TryGetValue(audioId, out int existing))
                throw new ArgumentException(
                    $"Audio id '{audioId}' is already registered by {this._owners[existing]}; " +
                    $"{ownerPath} cannot reuse it.", nameof(audioId));

            int index = this._ids.Count;
            this._indexById.Add(audioId, index);
            this._ids.Add(audioId);
            this._owners.Add(ownerPath);

            return index;
        }

        /// <summary>
        /// Looks up the index for <paramref name="audioId"/>. Case-sensitive, because the generated
        /// constants are.
        /// </summary>
        /// <remarks>
        /// On failure <paramref name="index"/> is -1 rather than 0, so a caller that forgets to
        /// check the return value indexes out of range instead of silently playing the first entry.
        /// </remarks>
        public bool TryGetIndex(string audioId, out int index)
        {
            if (audioId != null && this._indexById.TryGetValue(audioId, out index))
                return true;

            index = -1;
            return false;
        }

        /// <summary>The id registered at <paramref name="index"/>.</summary>
        public string GetId(int index)
        {
            this.ValidateIndex(index);
            return this._ids[index];
        }

        /// <summary>Who registered <paramref name="index"/>. For diagnostics.</summary>
        public string GetOwner(int index)
        {
            this.ValidateIndex(index);
            return this._owners[index];
        }

        /// <summary>Forgets everything, so the next <see cref="Add"/> starts again at zero.</summary>
        public void Clear()
        {
            this._indexById.Clear();
            this._ids.Clear();
            this._owners.Clear();
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= this._ids.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, $"No audio id is registered at index {index}.");
        }
    }
}