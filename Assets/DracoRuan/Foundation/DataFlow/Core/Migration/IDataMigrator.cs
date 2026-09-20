using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// Upgrades one save domain from one schema version to the next.
    ///
    /// <para>
    /// Declared next to the repository it belongs to, not in a central registry — a domain owns its
    /// own version history the same way it owns its model and its controller.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Prefer deriving from <c>DataMigrator&lt;TFrom, TTo&gt;</c>, which handles the
    /// deserialize/serialize boundary and makes the version classes explicit in the type signature.
    /// Implement this interface directly only when a step needs to work on raw bytes.
    /// </remarks>
    public interface IDataMigrator
    {
        /// <summary>
        /// Domain this migrator upgrades. Must be a constant that never changes: it names a file on
        /// every player's device.
        /// </summary>
        string Domain { get; }

        /// <summary>Schema version this migrator reads.</summary>
        int FromVersion { get; }

        /// <summary>Schema version this migrator produces.</summary>
        int ToVersion { get; }

        /// <summary>
        /// Other domains this migrator reads. They are migrated first, and reading one that is not
        /// listed here is an error.
        /// </summary>
        IReadOnlyList<string> DependsOn { get; }

        /// <summary>
        /// Rewrites the payload. Must call <see cref="IMigrationContext.SetPayload"/> for
        /// <see cref="Domain"/> before returning.
        /// </summary>
        UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Upgrades several mutually dependent domains in one indivisible step.
    ///
    /// <para>
    /// For data that cannot be upgraded in any order: where A's new shape is derived from B and B's
    /// from A. No sequence of single-domain migrators is correct there, because whichever runs first
    /// reads data the other has not rewritten yet. The planner detects such cycles and requires one
    /// of these; without it, it reports the cycle rather than guessing.
    /// </para>
    /// </summary>
    public interface IGroupDataMigrator
    {
        /// <summary>
        /// Version each participating domain must be at. Members need not share a number — a group
        /// may take A from v2 to v3 while taking B from v1 to v2 — so no filler migrators are needed
        /// just to line versions up.
        /// </summary>
        IReadOnlyDictionary<string, int> FromVersions { get; }

        /// <summary>Version each participating domain reaches.</summary>
        IReadOnlyDictionary<string, int> ToVersions { get; }

        /// <summary>Domains outside the group that this step reads.</summary>
        IReadOnlyList<string> DependsOn { get; }

        /// <summary>
        /// Rewrites every participating payload. Must call
        /// <see cref="IMigrationContext.SetPayload"/> for each domain in
        /// <see cref="FromVersions"/> before returning; the step commits all of them or none.
        /// </summary>
        UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken);
    }
}
