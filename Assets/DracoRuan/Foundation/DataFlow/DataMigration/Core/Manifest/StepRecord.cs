using System;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Manifest
{
    [Serializable]
    public class StepRecord
    {
        public string domain;
        public int fromVersion;
        public int toVersion;
        public bool isCompleted;
        public string completedAt;
        public string snapshotKey;
    }
}