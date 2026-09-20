using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Core.Serialization;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// Base class for upgrading one domain from one version class to the next.
    ///
    /// <code>
    /// public sealed class RiseProgressV1ToV2 : DataMigrator&lt;RiseProgressDataV1, RiseProgressDataV2&gt;
    /// {
    ///     public override string Domain =&gt; RiseProgressionDomain.Id;
    ///     public override int FromVersion =&gt; 1;
    ///     public override int ToVersion   =&gt; 2;
    ///
    ///     protected override RiseProgressDataV2 Migrate(RiseProgressDataV1 from, IMigrationContext context) =&gt;
    ///         new() { Number = from.Number, Streaks = from.Streaks };
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para><b>The two type parameters are the point.</b> They force each migrator to name the exact
    /// version class it reads. Deserializing an old payload into the game's <i>current</i> class
    /// would not fail — MessagePack skips unknown keys and defaults missing ones — it would return a
    /// plausible object with quietly wrong contents and write it straight back to disk. Naming
    /// <typeparamref name="TFrom"/> makes that impossible to do by accident.</para>
    ///
    /// <para>Old version classes therefore stay in the codebase permanently. They are small, frozen,
    /// and never edited again; they are the only record of what older saves actually look like.</para>
    /// </remarks>
    public abstract class DataMigrator<TFrom, TTo> : IDataMigrator
    {
        /// <summary>Domain this migrator upgrades. Must be a constant, never a type name.</summary>
        public abstract string Domain { get; }

        public abstract int FromVersion { get; }

        public abstract int ToVersion { get; }

        /// <summary>
        /// Other domains this migrator reads via <see cref="IMigrationContext.GetDependencyPayload"/>.
        /// They are migrated first. Reading one that is not listed throws.
        /// </summary>
        public virtual IReadOnlyList<string> DependsOn => Array.Empty<string>();

        /// <summary>Serializer for this migrator's payloads.</summary>
        protected virtual IPayloadCodec Codec => PayloadCodec.Default;

        /// <summary>
        /// Produces the new shape from the old one. Return a new instance rather than mutating
        /// <paramref name="from"/>.
        /// </summary>
        protected abstract TTo Migrate(TFrom from, IMigrationContext context);

        /// <summary>
        /// Override for a migration that needs to await something, such as fetching a default from
        /// remote config. Prefer the synchronous <see cref="Migrate"/> where possible: this runs on
        /// the boot path, and every await here is time on a loading screen.
        /// </summary>
        protected virtual UniTask<TTo> MigrateAsync(
            TFrom from,
            IMigrationContext context,
            CancellationToken cancellationToken) =>
            UniTask.FromResult(this.Migrate(from, context));

        UniTask IDataMigrator.MigrateAsync(IMigrationContext context, CancellationToken cancellationToken) =>
            this.RunAsync(context, cancellationToken);

        private async UniTask RunAsync(IMigrationContext context, CancellationToken cancellationToken)
        {
            IPayloadCodec codec = this.Codec;

            TFrom from = codec.Deserialize<TFrom>(context.GetPayload(this.Domain));
            TTo to = await this.MigrateAsync(from, context, cancellationToken);

            if (to == null)
            {
                throw new InvalidOperationException(
                    $"{this.GetType().Name} returned null for '{this.Domain}' v{this.FromVersion} -> " +
                    $"v{this.ToVersion}. A migration must produce a payload; returning null would write " +
                    "an empty save over the player's data.");
            }

            context.SetPayload(this.Domain, codec.Serialize(to));
        }
    }

    /// <summary>
    /// Base class for upgrading several mutually dependent domains in one indivisible step.
    ///
    /// <para>
    /// Use this when A's new shape is derived from B and B's from A. No ordering of single-domain
    /// migrators is correct in that case, so the planner refuses to guess and asks for one of these.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Every payload handed to <see cref="Migrate"/> is the pre-migration one, so each side sees a
    /// consistent snapshot of the others. Write all of them: the step commits every domain or none.
    /// </remarks>
    public abstract class GroupDataMigrator : IGroupDataMigrator
    {
        /// <summary>
        /// Version each participating domain must be at. Members need not share a number.
        /// </summary>
        public abstract IReadOnlyDictionary<string, int> FromVersions { get; }

        /// <summary>Version each participating domain reaches.</summary>
        public abstract IReadOnlyDictionary<string, int> ToVersions { get; }

        /// <summary>Domains outside the group that this step reads.</summary>
        public virtual IReadOnlyList<string> DependsOn => Array.Empty<string>();

        /// <summary>Serializer for this migrator's payloads.</summary>
        protected virtual IPayloadCodec Codec => PayloadCodec.Default;

        /// <summary>
        /// Rewrites every participating domain. Use <see cref="Read{T}"/> and <see cref="Write{T}"/>
        /// rather than touching raw bytes.
        /// </summary>
        protected abstract void Migrate(IMigrationContext context);

        /// <summary>Override when the step needs to await something.</summary>
        protected virtual UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken)
        {
            this.Migrate(context);
            return UniTask.CompletedTask;
        }

        UniTask IGroupDataMigrator.MigrateAsync(IMigrationContext context, CancellationToken cancellationToken) =>
            this.MigrateAsync(context, cancellationToken);

        /// <summary>Reads a participating domain's pre-migration payload as <typeparamref name="T"/>.</summary>
        protected T Read<T>(IMigrationContext context, string domainId) =>
            this.Codec.Deserialize<T>(context.GetPayload(domainId));

        /// <summary>Writes a participating domain's migrated payload.</summary>
        protected void Write<T>(IMigrationContext context, string domainId, T value)
        {
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"{this.GetType().Name} produced null for '{domainId}'. A migration must produce a " +
                    "payload; returning null would write an empty save over the player's data.");
            }

            context.SetPayload(domainId, this.Codec.Serialize(value));
        }
    }
}
