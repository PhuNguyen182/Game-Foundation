using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies.Resolver;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Manifest;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Orchestrator;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Snapshot;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core
{
    /// <summary>
    /// This installer is responsible for registering all necessary services of Data Migration.
    /// To sufficiently migrate all available data, please create a new Installer of type IAsyncInstallable for
    /// registering all available data migrators in the project 
    /// </summary>
    /// <code>
    /// [AutoInstall(InstallerInstanceType = nameof(InstallerType.PlainCSharp))]
    /// public class DataMigratorInstaller : IAsyncInstallable
    /// {
    ///     private bool _isInstalled;
    /// 
    ///     public bool IsInstalled() => this._isInstalled;
    /// 
    ///     public void Install(IContainerBuilder builder)
    ///     {
    ///         builder.Register&lt;IDataMigrator, InventoryDataMigratorV1ToV2&gt;(Lifetime.Scoped).AsSelf();
    ///         builder.Register&lt;IDataMigrator, QuestDataMigratorV3ToV4&gt;(Lifetime.Scoped).AsSelf();
    ///         // Continue register all available data migrators here ...
    ///     }
    /// }
    /// </code>
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
        }
    }
}
