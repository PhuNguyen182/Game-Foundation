using System;
using System.Reflection;
using DracoRuan.Foundation.DataFlow.StaticData.Controllers;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using VContainer;

namespace DracoRuan.Foundation.DataFlow.StaticData
{
    /// <summary>
    /// Container registration for static data, shaped like <c>DataFlowRegistration</c> on the save
    /// side so both halves are declared the same way.
    ///
    /// <code>
    /// protected override void Configure(IContainerBuilder builder)
    /// {
    ///     builder.Register&lt;IStaticRemoteConfigReader, FirebaseStaticRemoteConfigReader&gt;(Lifetime.Singleton);
    ///
    ///     StaticDataScope staticData = builder.AddStaticData();
    ///     builder.RegisterStaticDataController&lt;GachaRateController&gt;(staticData);
    ///     builder.RegisterStaticDataController&lt;LevelConfigController&gt;(staticData);
    /// }
    /// </code>
    /// </summary>
    public static class StaticDataRegistration
    {
        /// <summary>
        /// Registers the static data infrastructure and returns a handle for declaring tables.
        /// </summary>
        /// <param name="remoteConfigReadyTimeoutSeconds">
        /// How long a table waits for remote config before falling through. Bounded on purpose: an
        /// unbounded wait on the first link of a chain is a hang, not a retry.
        /// </param>
        /// <param name="urlTimeoutSeconds">Per-attempt budget for a CDN download.</param>
        public static StaticDataScope AddStaticData(
            this IContainerBuilder builder,
            float remoteConfigReadyTimeoutSeconds = 10f,
            int urlTimeoutSeconds = 10)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            StaticDataRegistry registry = new();
            builder.RegisterInstance(registry);

            // Application.persistentDataPath must be read on the main thread, and Configure runs
            // there. Resolving the cache now means no download ever has to ask for it.
            StaticDataDiskCache diskCache = StaticDataDiskCache.CreateDefault();

            builder.Register<IStaticDataSourceRegistry>(resolver => new StaticDataSourceRegistry(
                    // Optional: a project with no remote config registers no reader, and chains that
                    // name RemoteConfig simply skip that link instead of failing to build.
                    resolver.ResolveOrDefault<IStaticRemoteConfigReader>(),
                    diskCache,
                    remoteConfigReadyTimeoutSeconds,
                    urlTimeoutSeconds),
                Lifetime.Singleton);

            builder.Register<StaticDataControllerContext>(Lifetime.Singleton);

            return new StaticDataScope(registry);
        }

        /// <summary>
        /// Registers one table's controller, as a singleton and as an
        /// <see cref="IStaticDataController"/> so the boot pipeline can initialize it.
        /// </summary>
        /// <remarks>
        /// The id comes from <see cref="StaticDataIdAttribute"/> rather than a parameter, so
        /// there is exactly one place it is written down and no way for a registration to disagree
        /// with the controller about what its table is called.
        /// </remarks>
        public static IContainerBuilder RegisterStaticDataController<TController>(
            this IContainerBuilder builder, StaticDataScope scope)
            where TController : class, IStaticDataController
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (scope == null) throw new ArgumentNullException(nameof(scope));

            Type controllerType = typeof(TController);
            StaticDataIdAttribute attribute =
                controllerType.GetCustomAttribute<StaticDataIdAttribute>();

            if (attribute == null)
                throw new ArgumentException(
                    $"{controllerType.Name} is missing [{nameof(StaticDataIdAttribute)}]. " +
                    "Add it with the controller's own Id constant so tooling can find the table " +
                    "without constructing the controller.");

            scope.Registry.Add(new StaticDataDescriptor(attribute.DataId, controllerType));

            builder.Register<TController>(Lifetime.Singleton)
                .As<IStaticDataController>()
                .AsSelf();

            return builder;
        }
    }

    /// <summary>Handle returned by <see cref="StaticDataRegistration.AddStaticData"/>.</summary>
    public sealed class StaticDataScope
    {
        internal StaticDataScope(StaticDataRegistry registry)
        {
            this.Registry = registry;
        }

        public StaticDataRegistry Registry { get; }
    }
}