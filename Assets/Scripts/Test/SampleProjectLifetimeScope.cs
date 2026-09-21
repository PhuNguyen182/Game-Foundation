using DracoRuan.Foundation.DataFlow.Runtime;
using DracoRuan.Foundation.DataFlow.StaticData;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using DracoRuan.RemoteConfig;
using DracoRuan.VContainerInstallerSupport.Generated;
using Temps.Scripts;
using Temps.Scripts.StaticDataSample;
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

        /// <summary>
        /// All registration happens synchronously here.
        /// </summary>
        /// <remarks>
        /// <para>This used to call an <c>async</c> method and <c>Forget()</c> it, so registrations
        /// raced <c>Build()</c>. It happened to work only because both generated installer
        /// extensions completed inline (<c>await UniTask.CompletedTask</c>); the generator's
        /// MonoBehaviour and ScriptableObject paths await Addressables for real, so the first
        /// installer converted to either shape would have had its registrations land <i>after</i>
        /// the container was built and vanish with no error.</para>
        ///
        /// <para>Anything that genuinely needs to load before it can register belongs in an
        /// installer that the boot pipeline awaits, not in <c>Configure</c>.</para>
        /// </remarks>
        protected override void Configure(IContainerBuilder builder)
        {
            LifetimeScopeInstallerRoot = this.transform;

            this.RegisterInstallers(builder);
            this.RegisterServices(builder);
            this.RegisterStaticData(builder);
            this.RegisterDataFlow(builder);

            // Registered last so every dependency it resolves already exists.
            builder.RegisterEntryPoint<AppInitializationPipelineEntryPoint>(Lifetime.Singleton);
        }

        private void RegisterInstallers(IContainerBuilder builder)
        {
            builder.LoadAndInstallMessageBrokerInstaller();
        }

        private void RegisterServices(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<TestService>(Lifetime.Singleton);
            builder.Register<IRemoteConfigService, FirebaseRemoteConfigService>(Lifetime.Singleton);
        }

        /// <summary>
        /// Registers the config system, then one line per table.
        /// </summary>
        /// <remarks>
        /// <para>The reader adapter is registered before <c>AddStaticData</c> so the source registry
        /// can pick it up; a project with no remote config simply leaves it out, and any chain naming
        /// that source skips the link instead of failing to build.</para>
        ///
        /// <para>Each controller declares its own fallback chain, so there is nothing to configure
        /// here beyond which tables exist.</para>
        /// </remarks>
        private void RegisterStaticData(IContainerBuilder builder)
        {
            builder.Register<IStaticRemoteConfigReader, RemoteConfigStaticDataReader>(Lifetime.Singleton);

            StaticDataScope staticData = builder.AddStaticData();

            builder.RegisterStaticDataController<GachaRateController>(staticData);
#if USE_CSV_HELPER
            builder.RegisterStaticDataController<LevelConfigController>(staticData);
#endif
        }

        /// <summary>
        /// Registers the save system, then one line per repository.
        /// </summary>
        /// <remarks>
        /// Each repository declares a permanent domain id and the schema version this build expects.
        /// Migrators are registered alongside via <c>dataFlow.RegisterMigrator(...)</c>, so a
        /// domain's version history lives next to the domain rather than in a central list.
        /// </remarks>
        private void RegisterDataFlow(IContainerBuilder builder)
        {
            DataFlowScope dataFlow = builder.AddDataFlow();

            builder.RegisterDataController<RiseProgressionDataController, RiseProgressDataV1>(
                dataFlow,
                domainId: RiseProgressionDataController.Id,
                targetSchemaVersion: 1,
                legacyTypeName: "RiseProgressData");
        }
    }
}