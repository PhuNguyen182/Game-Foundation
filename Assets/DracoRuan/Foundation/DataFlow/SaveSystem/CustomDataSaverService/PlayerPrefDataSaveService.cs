using System;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService
{
    /// <summary>
    /// Stores save payloads in <see cref="PlayerPrefs"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Payloads are Base64-encoded, not UTF-8 round-tripped.</b> The previous version did
    /// <c>Encoding.UTF8.GetString</c> on the way in and <c>GetBytes</c> on the way out. That is
    /// lossless only for text: arbitrary binary — which is what MessagePack produces — contains byte
    /// sequences that are not valid UTF-8, and decoding replaces each of them with U+FFFD. The
    /// original bytes are then unrecoverable, and the corruption appears at load time rather than at
    /// save time, far from its cause.</para>
    ///
    /// <para><b>PlayerPrefs is not a save backend for anything that matters.</b> It is capped in size
    /// on some platforms, is trivially editable by the player, and on Android lives in shared
    /// preferences that a backup or restore can replace wholesale. Prefer
    /// <see cref="FileDataSaveService"/>; this exists for small, non-critical values.</para>
    /// </remarks>
    public class PlayerPrefDataSaveService : IDataSaveService
    {
        private const string LogTag = "PlayerPrefSave";

        public bool IsDataExist(string dataName) => PlayerPrefs.HasKey(dataName);

        public byte[] LoadData(string name)
        {
            if (!PlayerPrefs.HasKey(name))
                return null;

            string encoded = PlayerPrefs.GetString(name);
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                return Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                // Most likely a value written by the old UTF-8 implementation, which cannot be
                // recovered. Report it rather than returning something that looks like real data.
                Debug.LogError(
                    $"[{LogTag}] '{name}' is not valid Base64 and cannot be decoded. It was probably " +
                    "written by an older build that stored payloads as UTF-8 text.");
                return null;
            }
        }

        public void SaveData(string name, byte[] serializedData)
        {
            if (serializedData == null)
                return;

            PlayerPrefs.SetString(name, Convert.ToBase64String(serializedData));

            // PlayerPrefs only reaches disk on Save(); without it a crash loses everything written
            // since the last natural flush.
            PlayerPrefs.Save();
        }

        public void DeleteData(string name)
        {
            PlayerPrefs.DeleteKey(name);
            PlayerPrefs.Save();
        }
    }
}