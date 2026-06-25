using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.RemoteConfig;
using DracoRuan.VContainerInstallerSupport.Generated;
using Temps.Scripts;
using Temps.Scripts.TestRiseProgressData;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Test
{
    [DefaultExecutionOrder(-50)]
    public class SampleProjectLifetimeScope : LifetimeScope
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
            await this.LoadAllInstallers(builder);
            this.RegisterInstaller(builder);
            this.RegisterServices(builder);
        }

        private async UniTask LoadAllInstallers(IContainerBuilder builder)
        {
            await builder.InitializeDataMigrationInstaller();
            await builder.InitializeMessageBrokerInstaller();
        }

        private void RegisterInstaller(IContainerBuilder builder)
        {
            builder.LoadAndInstallDataMigrationInstaller();
            builder.LoadAndInstallMessageBrokerInstaller();
        }

        private void RegisterServices(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<TestService>(Lifetime.Scoped);
            builder.Register<IRemoteConfigService, FirebaseRemoteConfigService>(Lifetime.Scoped);
            builder.Register<IDataProviderService, DataProviderService>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
            builder.Register<RiseProgressionDataController>(Lifetime.Scoped);
        }
    }
}
