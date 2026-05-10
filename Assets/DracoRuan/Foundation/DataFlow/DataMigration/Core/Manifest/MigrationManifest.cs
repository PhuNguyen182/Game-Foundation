using System;
using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Manifest
{
    [Serializable]
    public class MigrationManifest
    {
        public string playerId;
        public int startVersion;
        public int endVersion;
        public bool isMigrationCompleted;
        public string startedAt;
        public string completedAt;
        public List<StepRecord> migrationRecords = new();

        public bool IsStepCompleted(string domain, int fromVersion, int toVersion)
        {
            int recordCount = this.migrationRecords.Count;
            for (int i = 0; i < recordCount; i++)
            {
                StepRecord step = this.migrationRecords[i];
                if (string.CompareOrdinal(step.domain, domain) != 0) 
                    continue;
                
                if (step.fromVersion == fromVersion && step.toVersion == toVersion && step.isCompleted)
                    return true;
            }

            return false;
        }

        public StepRecord GetOrCreateStep(string domain, int fromVersion, int toVersion, string snapshotKey)
        {
            int recordCount = this.migrationRecords.Count;
            for (int i = 0; i < recordCount; i++)
            {
                StepRecord step = this.migrationRecords[i];
                if (string.CompareOrdinal(step.domain, domain) == 0 
                    && step.fromVersion == fromVersion 
                    && step.toVersion == toVersion)
                    return step;
            }

            StepRecord stepRecord = new StepRecord
            {
                domain =  domain,
                fromVersion = fromVersion,
                toVersion = toVersion,
                isCompleted = false,
                snapshotKey = snapshotKey,
            };
            
            this.migrationRecords.Add(stepRecord);
            return stepRecord;
        }

        public void MarkStepCompleted(string domain, int fromVersion, int toVersion)
        {
            StepRecord step = this.GetOrCreateStep(domain, fromVersion, toVersion, null);
            step.isCompleted = true;
            step.completedAt = DateTime.UtcNow.ToString("O");
        }
    }
}
