using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Migrator
{
    public abstract class DataMigratorBase : IDataMigrator
    {
        public abstract string Domain { get; }
        public abstract int FromVersion { get; }
        public abstract int ToVersion { get; }
        
        public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();

        public abstract UniTask<MigrationResult> MigrateData(MigrationContext context);

        public virtual UniTask<bool> ValidateData(MigrationContext context) => UniTask.FromResult(true);

        protected TData GetData<TData>(MigrationContext context) where TData : class
            => context.GetData<TData>(this.Domain);

        protected void SetData<TData>(MigrationContext context, TData data) where TData : class
            => context.SetData(this.Domain, data);

        protected TData GetDependencyData<TData>(MigrationContext context, string domain) where TData : class
            => context.GetData<TData>(domain);
    }
}
