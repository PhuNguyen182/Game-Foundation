using System;
using System.IO;
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
            
            string directoryPath = "DataMigration";
            string dataPath = Path.Combine(directoryPath, $"{manifestKey}.json");
            
            if (!File.Exists(dataPath))
                return null;

            using StreamReader streamReader = new(dataPath);
            string rawData = streamReader.ReadToEnd();
            
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
            string directoryPath = "DataMigration";
            string dataPath = Path.Combine(directoryPath, $"{manifestKey}.json");
            
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            
            using FileStream fileStream = new(dataPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: false);
            using StreamWriter writer = new(fileStream);
            writer.WriteLineAsync(json);
        }
    }
}