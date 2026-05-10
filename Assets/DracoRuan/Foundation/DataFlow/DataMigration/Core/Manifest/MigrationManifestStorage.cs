using System;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Manifest
{
    public class MigrationManifestStorage
    {
        private const string ManifestKey = "MigrationManifest";
        
        public MigrationManifest Load(int playerId)
        {
            const string manifestPrefix = "MigrationManifest";
            string manifestKey = $"{manifestPrefix}{playerId}";
            string rawData = PlayerPrefs.GetString(manifestKey, null);
            
            if (string.IsNullOrEmpty(rawData))
                return null;

            try
            {
                MigrationManifest result = JsonUtility.FromJson<MigrationManifest>(rawData);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{ManifestKey}] Cannot read data migration manifest for player id: {playerId}. More information: {e.Message}");
                return null;
            }
        }

        public void Save(MigrationManifest manifest)
        {
            const string manifestPrefix = "MigrationManifest";
            string manifestKey = $"{manifestPrefix}{manifest.playerId}";
            string json = JsonUtility.ToJson(manifest);
            PlayerPrefs.SetString(manifestKey, json);
            // TODO: Can be replaced by File saving and MemoryPack serialization
        }
    }
}