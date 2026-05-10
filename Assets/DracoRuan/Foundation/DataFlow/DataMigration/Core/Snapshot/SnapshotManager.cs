using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.DataMigration.Migrator;
using UnityEngine;
using ZLinq;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Snapshot
{
    public class SnapshotManager
    {
        private const string SnapshotKey = "Snapshot";
        
        private readonly Dictionary<string, string> _snapshots = new();

        public string TakeSnapshot(MigrationContext migrationContext, string domain, int version)
        {
            string snapshotKey = BuildSnapshotKey(domain, version);
            object data = migrationContext.GetData<object>(domain);
            SnapshotWrapper wrapper = new SnapshotWrapper
            {
                json = JsonUtility.ToJson(data)
            }; 
            
            string snapshotData = data != null ? JsonUtility.ToJson(wrapper) : null;
            Debug.Log($"[{SnapshotKey}] Take snapshot {domain} v{version} succeed!");
            this._snapshots[snapshotKey] = snapshotData;
            return snapshotKey;
        }

        public string TakeFullSnapshot(MigrationContext migrationContext)
        {
            Dictionary<string, string> allData = new();
            string snapshotKey = BuildSnapshotKey(migrationContext.PlayerId);

            foreach (var kvp in migrationContext.Data)
            {
                string data = kvp.Value != null ? JsonUtility.ToJson(kvp.Value) : null;
                allData.Add(kvp.Key, data);
            }

            DictionaryWrapper wrapper = new DictionaryWrapper()
            {
                keys = allData.Keys.AsValueEnumerable().ToList(),
                values = allData.Values.AsValueEnumerable().ToList(),
            };
            
            string snapshot = JsonUtility.ToJson(wrapper);
            this._snapshots[snapshotKey] = snapshot;
            Debug.Log($"[{SnapshotKey}] Take full snapshot {snapshotKey} succeed!");
            return snapshotKey;
        }

        public bool RestoreSnapshot(MigrationContext migrationContext, string domain, int version, string snapshotKey)
        {
            if (!this._snapshots.TryGetValue(snapshotKey, out string snapshot))
            {
                Debug.LogError($"[{SnapshotKey}] Snapshot {snapshotKey} not found!");
                return false;
            }

            if (string.IsNullOrEmpty(snapshot))
            {
                migrationContext.Data.Remove(domain);
                return true;
            }

            try
            {
                SnapshotWrapper snapshotWrapper = JsonUtility.FromJson<SnapshotWrapper>(snapshot);
                object snapshotData = JsonUtility.FromJson<object>(snapshotWrapper.json);
                migrationContext.SetData(domain, snapshotData);
                Debug.Log($"[{SnapshotKey}] Restore snapshot {snapshotKey} succeed!]");
                return true;
            }
            catch (Exception e)
            {
                Debug.Log($"[{SnapshotKey}] Restore snapshot {snapshotKey} failed! More information: {e.Message}]");
                return false;
            }
        }
        
        public void ClearSnapshot(string snapshotKey) => this._snapshots.Remove(snapshotKey);

        public void ClearAll() => this._snapshots.Clear();

        private static string BuildSnapshotKey(string domain, int version)
            => $"{domain}_v{version}_{DateTime.UtcNow:O}";

        private static string BuildSnapshotKey(int playerId)
            => $"Full_{playerId}_{DateTime.UtcNow:O}";
    }
}
