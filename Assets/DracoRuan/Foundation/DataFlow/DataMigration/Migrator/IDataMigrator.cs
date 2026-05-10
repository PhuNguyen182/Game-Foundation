using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Migrator
{
    public interface IDataMigrator
    {
        public string Domain { get; }
        public int FromVersion { get; }
        public int ToVersion { get; }
        
        public IReadOnlyList<string> Dependencies { get; }

        public UniTask<MigrationResult> MigrateData(MigrationContext context);
        public UniTask<bool> ValidateData(MigrationContext context);
    }
}
