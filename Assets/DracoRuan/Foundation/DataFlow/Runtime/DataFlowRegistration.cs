using System;
using System.IO;
using DracoRuan.Foundation.DataFlow.Core.Migration;
using DracoRuan.Foundation.DataFlow.Core.Serialization;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.Sync;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// Container registration for the save system.
    ///
    /// <code>
    /// protected override void Configure(IContainerBuilder builder)
    /// {
    ///     DataFlowScope dataFlow = builder.AddDataFlow();
    ///
    ///     builder.RegisterDataController&lt;RiseProgressionDataController, RiseProgressDataV1&gt;(
    ///         dataFlow, domainId: "rise_progression", targetSchemaVersion: 1);
    ///
    ///     dataFlow.RegisterMigrator(new RiseProgressV1ToV2());
    /// }
    /// </code>
    /// </summary>
    public static class DataFlowRegistration
    {
        /// <summary>Directory under <see cref="Application.persistentDataPath"/> holding save files.</summary>
        public const string SaveDirectoryName = "GameData";

        /// <summary>
        /// Registers the save system and returns a handle for declaring domains and migrators.
        /// </summary>
        /// <param name="builder">Container being configured.</param>
        /// <param name="autoSaveIntervalSeconds">
        /// Seconds between periodic flushes. Zero or less leaves only the suspend and quit flushes.
        /// </param>
        public static DataFlowScope AddDataFlow(this IContainerBuilder builder, float autoSaveIntervalSeconds = 30f)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // Application.persistentDataPath must be read on the main thread, and Configure runs
            // there. Resolving it now means no background write ever has to ask for it.
            string saveRoot = Path.Combine(Application.persistentDataPath, SaveDirectoryName);

            SaveEnvelopeStore store = new(saveRoot);
            DataDomainRegistry domains = new();
            MigrationRegistry migrators = new();
            DataFlowGate gate = new();
            IPayloadCodec codec = new MessagePackPayloadCodec();

            // Migrators pick this up without every one of them needing it injected.
            PayloadCodec.Default = codec;

            builder.RegisterInstance(store);
            builder.RegisterInstance(domains);
            builder.RegisterInstance(migrators);
            builder.RegisterInstance(gate);
            builder.RegisterInstance(codec).As<IPayloadCodec>();

            builder.Register<IPlayerIdentityProvider, LocalPlayerIdentityProvider>(Lifetime.Singleton);
            builder.Register<MigrationBootstrapper>(Lifetime.Singleton);

            // Registered as an entry point so the container starts it. Autosave and the
            // flush-on-suspend hook would otherwise never be armed, and the only way a save would
            // ever reach disk is a repository calling Save() by hand - which is the data loss this
            // scheduler exists to prevent.
            EntryPointsBuilder.EnsureDispatcherRegistered(builder);
            builder.Register(_ => new SaveScheduler(gate, autoSaveIntervalSeconds), Lifetime.Singleton)
                .AsSelf()
                .As<IStartable>();
            builder.Register<DataControllerContext>(Lifetime.Singleton);

            return new DataFlowScope(builder, domains, migrators);
        }

        /// <summary>
        /// Registers one repository: its controller and the descriptor that lets boot-time migration
        /// know its target version without constructing it.
        /// </summary>
        /// <param name="domainId">
        /// Permanent identifier for the save file. Use a constant, never <c>nameof</c> of a type:
        /// renaming the class would orphan every player's save.
        /// </param>
        /// <param name="targetSchemaVersion">Schema version this build expects.</param>
        /// <param name="legacyTypeName">
        /// Filename stem used before the envelope format, if different from
        /// <typeparamref name="TData"/>'s name. Only needed for domains that shipped previously.
        /// </param>
        public static IContainerBuilder RegisterDataController<TController, TData>(
            this IContainerBuilder builder,
            DataFlowScope scope,
            string domainId,
            int targetSchemaVersion,
            string legacyTypeName = null)
            where TController : DynamicGameDataController<TData>
            where TData : class, IGameData, new()
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (scope == null) throw new ArgumentNullException(nameof(scope));

            scope.Domains.Add(new DataDomainDescriptor(
                domainId, targetSchemaVersion, typeof(TData), typeof(TController), legacyTypeName));

            builder.Register<TController>(Lifetime.Singleton)
                .As<IDataController>()
                .AsSelf();

            return builder;
        }
    }

    /// <summary>
    /// Handle returned by <see cref="DataFlowRegistration.AddDataFlow"/> for declaring domains and
    /// migrators.
    /// </summary>
    public sealed class DataFlowScope
    {
        internal DataFlowScope(IContainerBuilder builder, DataDomainRegistry domains, MigrationRegistry migrators)
        {
            this.Builder = builder;
            this.Domains = domains;
            this.Migrators = migrators;
        }

        internal IContainerBuilder Builder { get; }

        public DataDomainRegistry Domains { get; }

        public MigrationRegistry Migrators { get; }

        /// <summary>Registers a single-domain migrator.</summary>
        public DataFlowScope RegisterMigrator(IDataMigrator migrator)
        {
            this.Migrators.Register(migrator);
            return this;
        }

        /// <summary>
        /// Registers a migrator that rewrites several mutually dependent domains together.
        /// </summary>
        public DataFlowScope RegisterMigrator(IGroupDataMigrator migrator)
        {
            this.Migrators.Register(migrator);
            return this;
        }
    }
}