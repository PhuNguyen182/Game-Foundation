using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.MasterDataController;
using DracoRuan.VContainerInstallerSupport.Generated;
using Temps.Scripts;
using Temps.Scripts.TestRiseProgressData;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Test
{
    [DefaultExecutionOrder(-50)]
    public class ProjectLifetimeScope : LifetimeScope
    {
        public static Transform LifetimeScopeInstallerRoot;
        
        protected override void Configure(IContainerBuilder builder)
        {
            LifetimeScopeInstallerRoot = this.transform;
            builder.RegisterEntryPoint<AppInitializationPipelineEntryPoint>(Lifetime.Scoped);
            this.RegisterServicesAndInstaller(builder).Forget();
        }

        private async UniTask RegisterServicesAndInstaller(IContainerBuilder builder)
        {
            builder.InstallDataMigrationInstaller().Forget();
            builder.InstallMessageBrokerInstaller().Forget();
            builder.RegisterEntryPoint<TestService>(Lifetime.Scoped);
            builder.Register<IDataProviderService, DataProviderService>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
            builder.Register<RiseProgressionDataController>(Lifetime.Scoped);
            builder.Register<IStaticCustomDataManager, StaticCustomDataManager>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
            builder.Register<IDynamicCustomDataManager, DynamicCustomDataManager>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
            builder.Register<IMainDataManager, MainDataManager>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
        }
    }
}
