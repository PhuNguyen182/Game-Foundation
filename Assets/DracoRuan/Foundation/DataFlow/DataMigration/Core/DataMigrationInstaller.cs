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

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core
{
    [AutoInstall(InstallerInstanceType = nameof(InstallerType.PlainCSharp))]
    public class DataMigrationInstaller : IAsyncInstallable
    {
        private bool _isInstalled;
        private MigrationRegistry _migrationRegistry;
        private MigrationManifestStorage _migrationManifestStorage;
        private DataMigrationOrchestrator _migrationOrchestrator;
        private MigrationResolver _migrationResolver;
        private SnapshotManager _snapshotManager;
        
        public bool IsInstalled() => this._isInstalled;

        public void Install(IContainerBuilder builder)
        {
            this.RegisterDependencies(builder);
        }

        private void RegisterDependencies(IContainerBuilder builder)
        {
            this._migrationRegistry = new MigrationRegistry();
            this._migrationManifestStorage = new MigrationManifestStorage();
            this._migrationResolver = new MigrationResolver();
            this._snapshotManager = new SnapshotManager();
            this.RegisterDataMigrators();
            this._migrationOrchestrator = new DataMigrationOrchestrator(this._migrationRegistry,
                this._migrationResolver, this._snapshotManager, this._migrationManifestStorage)
            {
                Strategy = RollbackStrategy.Fully
            };
        }

        private void RegisterDataMigrators()
        {
            // Register data migrator for each persistant data model here
        }

        public async UniTask<bool> RunDataMigration(MigrationContext context, int targetVersion)
        {
            context.TargetVersion = targetVersion;
            MigrationResult result = await this._migrationOrchestrator.MigrateData(context);

            if (!result.IsSuccess)
            {
                Debug.LogError(result.ToString());
            }
            
            return result.IsSuccess;
        }
    }
}
