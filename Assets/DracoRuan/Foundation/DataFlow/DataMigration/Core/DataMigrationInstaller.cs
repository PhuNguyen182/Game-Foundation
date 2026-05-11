using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies.Resolver;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Manifest;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Orchestrator;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Snapshot;
using DracoRuan.Foundation.DataFlow.DataMigration.Migrator;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core
{
    [AutoInstall(InstallerInstanceType = nameof(InstallerType.PlainCSharp))]
    public class DataMigrationInstaller : IAsyncInstallable
    {
        private bool _isInstalled;
        
        public bool IsInstalled() => this._isInstalled;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<DataMigrationOrchestrator>(Lifetime.Scoped);
            builder.Register<MigrationRegistry>(Lifetime.Scoped);
            builder.Register<MigrationManifestStorage>(Lifetime.Scoped);
            builder.Register<MigrationResolver>(Lifetime.Scoped);
            builder.Register<SnapshotManager>(Lifetime.Scoped);
            this.RegisterDataMigrators(builder);
        }

        private void RegisterDataMigrators(IContainerBuilder builder)
        {
            builder.Register<IDataMigrator, SampleDataMigrator>(Lifetime.Scoped).AsSelf();
        }
    }

    public class SampleDataMigrator : DataMigratorBase
    {
        public override string Domain { get; }
        public override int FromVersion { get; }
        public override int ToVersion { get; }
        public override UniTask<MigrationResult> MigrateData(MigrationContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}
