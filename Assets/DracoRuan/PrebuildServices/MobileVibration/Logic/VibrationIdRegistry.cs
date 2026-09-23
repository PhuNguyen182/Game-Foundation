using System;
using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.MobileVibration.Logic
{
    /// <summary>
    /// The string-to-index table behind O(1) entry lookup.
    /// </summary>
    /// <remarks>
    /// <para>The index it hands out, not the id, is what the rest of the service keys on: the
    /// cooldown gate is an array indexed by it. One dictionary lookup per <c>Play</c> therefore buys
    /// an array index for everything downstream, and no string is hashed again for the life of that
    /// request.</para>
    ///
    /// <para>A duplicate id throws instead of overwriting, the way <c>AudioIdRegistry</c> does, and
    /// the message names both owners. Silently overwriting would leave one of the two vibrations
    /// permanently unreachable with nothing to show for it.</para>
    /// </remarks>
    public sealed class VibrationIdRegistry
    {
        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _ids = new List<string>();
        private readonly List<string> _owners = new List<string>();

        /// <summary>How many ids are registered.</summary>
        public int Count => this._ids.Count;

        /// <summary>
        /// Registers <paramref name="vibrationId"/> and returns the index it will be known by.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The id breaks <see cref="VibrationIdFormat"/>, or another owner already registered it.
        /// </exception>
        public int Add(string vibrationId, string ownerPath)
        {
            VibrationIdFormat.Validate(vibrationId);

            if (this._indexById.TryGetValue(vibrationId, out int existing))
                throw new ArgumentException(
                    $"Vibration id '{vibrationId}' is already registered by {this._owners[existing]}; " +
                    $"{ownerPath} cannot reuse it.", nameof(vibrationId));

            int index = this._ids.Count;
            this._indexById.Add(vibrationId, index);
            this._ids.Add(vibrationId);
            this._owners.Add(ownerPath);

            return index;
        }

        /// <summary>
        /// Looks up the index for <paramref name="vibrationId"/>. Case-sensitive, because the
        /// generated constants are.
        /// </summary>
        /// <remarks>
        /// On failure <paramref name="index"/> is -1 rather than 0, so a caller that forgets to check
        /// the return value indexes out of range instead of silently playing the first entry.
        /// </remarks>
        public bool TryGetIndex(string vibrationId, out int index)
        {
            if (vibrationId != null && this._indexById.TryGetValue(vibrationId, out index))
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
                    nameof(index), index, $"No vibration id is registered at index {index}.");
        }
    }
}
