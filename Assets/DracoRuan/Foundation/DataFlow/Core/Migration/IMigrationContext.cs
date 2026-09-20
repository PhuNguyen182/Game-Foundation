using System;
using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// What a migrator is given while it runs: the payloads it is rewriting, and read-only access to
    /// the domains it declared a dependency on.
    /// </summary>
    /// <remarks>
    /// <para><b>Payloads are bytes, not objects.</b> This is the single most important rule in the
    /// migration system. If a migrator deserialized into the game's <i>current</i> data class,
    /// MessagePack would happily accept an older payload — unknown keys skipped, missing keys
    /// defaulted — and return a plausible object with silently wrong contents, which then gets
    /// written back to disk. No exception, no warning, no way to detect it afterwards. Handing over
    /// bytes forces each migrator to name the exact version class it is reading.</para>
    ///
    /// <para><b>Reads are checked against declarations.</b> <see cref="GetDependencyPayload"/>
    /// refuses a domain the step did not declare in <c>DependsOn</c>, so the dependency graph the
    /// planner ordered on always matches what the code actually does. An undeclared read would be
    /// ordered arbitrarily and silently observe un-migrated data.</para>
    /// </remarks>
    public interface IMigrationContext
    {
        /// <summary>Identity the migration is running for. Never a type name.</summary>
        string PlayerId { get; }

        /// <summary>Domains this step is rewriting.</summary>
        IReadOnlyCollection<string> Domains { get; }

        /// <summary>
        /// The payload to migrate, as stored at the step's declared from-version.
        /// </summary>
        ReadOnlySpan<byte> GetPayload(string domainId);

        /// <summary>
        /// Replaces a domain's payload with the migrated bytes. Must be called once for every domain
        /// in <see cref="Domains"/> before the step returns.
        /// </summary>
        void SetPayload(string domainId, byte[] payload);

        /// <summary>
        /// Reads an already-migrated payload belonging to a domain this step declared a dependency
        /// on. Throws when the domain was not declared.
        /// </summary>
        ReadOnlySpan<byte> GetDependencyPayload(string domainId);
    }
}
