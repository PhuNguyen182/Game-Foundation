using System;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Sync
{
    /// <summary>
    /// Identifies the player whose saves are being read and written.
    /// </summary>
    /// <remarks>
    /// Replaces the hard-coded identity the migration system previously used, which was the literal
    /// string <c>"TData"</c> — <c>nameof</c> applied to a generic type parameter yields the
    /// parameter's own name, so every domain in the game shared one identity and one manifest.
    /// </remarks>
    public interface IPlayerIdentityProvider
    {
        /// <summary>Stable identifier for the current player.</summary>
        string PlayerId { get; }

        /// <summary>
        /// Identifies this installation. Two installs of the same account have different epochs,
        /// which is what lets a sync implementation tell "revision 5 from this device" apart from
        /// "revision 5 from the device before the reinstall".
        /// </summary>
        Guid DeviceEpochId { get; }
    }

    /// <summary>
    /// Local-only identity: a GUID generated on first run and kept in <see cref="PlayerPrefs"/>.
    /// </summary>
    /// <remarks>
    /// <para>A placeholder for offline builds. Once accounts exist, register an implementation backed
    /// by the real account id instead; nothing else has to change, because every consumer takes this
    /// interface.</para>
    ///
    /// <para>Values live in PlayerPrefs rather than a save file on purpose: they describe the
    /// <i>device</i>, so they must not travel with a save that gets restored onto another one.</para>
    /// </remarks>
    public sealed class LocalPlayerIdentityProvider : IPlayerIdentityProvider
    {
        private const string PlayerIdKey = "DracoRuan.DataFlow.PlayerId";
        private const string DeviceEpochKey = "DracoRuan.DataFlow.DeviceEpochId";

        private string _playerId;
        private Guid _deviceEpochId;

        public string PlayerId => this._playerId ??= GetOrCreateString(PlayerIdKey);

        public Guid DeviceEpochId
        {
            get
            {
                if (this._deviceEpochId == Guid.Empty)
                    this._deviceEpochId = Guid.TryParse(GetOrCreateString(DeviceEpochKey), out Guid parsed)
                        ? parsed
                        : Guid.NewGuid();

                return this._deviceEpochId;
            }
        }

        private static string GetOrCreateString(string key)
        {
            string existing = PlayerPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(existing))
                return existing;

            string created = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key, created);

            // PlayerPrefs only reaches disk on Save(); without it a crash before the next natural
            // flush would hand out a different identity on the following run.
            PlayerPrefs.Save();
            return created;
        }
    }
}
