using System;
using System.Collections.Generic;
using System.Linq;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// One indivisible advance of the save schema: it moves one or more domains from a known version
    /// to a higher one.
    ///
    /// <para>
    /// A step over a single domain is the ordinary case. A step over several domains exists for data
    /// that cannot be upgraded independently — where A's new shape is derived from B <i>and</i> B's
    /// from A. Such domains have no valid ordering, so they are not ordered: they are rewritten
    /// together, in one step, all or nothing.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>This is the planner's view of a migrator — metadata only, no execution. Keeping the two
    /// apart is what lets the whole planner, including the cycle analysis, be tested as a pure
    /// function with no Unity, no async and no files.</para>
    ///
    /// <para><see cref="DependsOn"/> lists domains this step <i>reads</i> and therefore needs
    /// migrated first. It is a promise the planner enforces: reading a domain you did not declare is
    /// an error, so the dependency graph always matches what the code actually does.</para>
    /// </remarks>
    public sealed class MigrationStep
    {
        private MigrationStep(
            string id,
            IReadOnlyDictionary<string, int> fromVersions,
            IReadOnlyDictionary<string, int> toVersions,
            IReadOnlyList<string> dependsOn)
        {
            this.Id = id;
            this.FromVersions = fromVersions;
            this.ToVersions = toVersions;
            this.DependsOn = dependsOn;
        }

        /// <summary>Stable identifier, usually the migrator's type name. Used in diagnostics.</summary>
        public string Id { get; }

        /// <summary>Version each participating domain must be at for this step to apply.</summary>
        public IReadOnlyDictionary<string, int> FromVersions { get; }

        /// <summary>Version each participating domain reaches when this step completes.</summary>
        public IReadOnlyDictionary<string, int> ToVersions { get; }

        /// <summary>
        /// Domains outside this step that it reads, and which must therefore be migrated first.
        /// </summary>
        public IReadOnlyList<string> DependsOn { get; }

        /// <summary>Domains this step rewrites.</summary>
        public IEnumerable<string> Domains => this.FromVersions.Keys;

        /// <summary>True when the step rewrites more than one domain as a unit.</summary>
        public bool IsGroup => this.FromVersions.Count > 1;

        /// <summary>Creates a step over a single domain.</summary>
        public static MigrationStep Single(
            string id,
            string domain,
            int fromVersion,
            int toVersion,
            IReadOnlyList<string> dependsOn = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Migration step id must not be empty.", nameof(id));
            if (string.IsNullOrEmpty(domain))
                throw new ArgumentException("Migration step domain must not be empty.", nameof(domain));
            if (toVersion <= fromVersion)
                throw new ArgumentException(
                    $"Migration step '{id}' must move '{domain}' forward, but goes v{fromVersion} -> v{toVersion}. " +
                    "Schema versions only ever increase; a downgrade is handled by refusing to load, not by a migrator.",
                    nameof(toVersion));

            return new MigrationStep(
                id,
                new Dictionary<string, int> { [domain] = fromVersion },
                new Dictionary<string, int> { [domain] = toVersion },
                dependsOn ?? Array.Empty<string>());
        }

        /// <summary>
        /// Creates a step that rewrites several mutually dependent domains together.
        /// </summary>
        /// <remarks>
        /// Members do not have to share a version number: a group may take A from v2 to v3 while
        /// taking B from v1 to v2. Each domain's own from/to is declared explicitly, because forcing
        /// members onto a shared version would push developers into inventing filler migrators.
        /// </remarks>
        public static MigrationStep Group(
            string id,
            IReadOnlyDictionary<string, int> fromVersions,
            IReadOnlyDictionary<string, int> toVersions,
            IReadOnlyList<string> dependsOn = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Migration step id must not be empty.", nameof(id));
            if (fromVersions == null || fromVersions.Count == 0)
                throw new ArgumentException("A group migration step must name at least one domain.",
                    nameof(fromVersions));
            if (toVersions == null)
                throw new ArgumentNullException(nameof(toVersions));

            if (fromVersions.Count != toVersions.Count ||
                fromVersions.Keys.Any(domain => !toVersions.ContainsKey(domain)))
            {
                throw new ArgumentException(
                    $"Group migration step '{id}' must declare a target version for exactly the domains it " +
                    $"declares a source version for. From: [{string.Join(", ", fromVersions.Keys)}], " +
                    $"To: [{string.Join(", ", toVersions.Keys)}].",
                    nameof(toVersions));
            }

            foreach (KeyValuePair<string, int> pair in fromVersions)
            {
                if (toVersions[pair.Key] <= pair.Value)
                {
                    throw new ArgumentException(
                        $"Group migration step '{id}' must move '{pair.Key}' forward, but goes " +
                        $"v{pair.Value} -> v{toVersions[pair.Key]}.",
                        nameof(toVersions));
                }
            }

            return new MigrationStep(
                id,
                new Dictionary<string, int>(fromVersions.ToDictionary(p => p.Key, p => p.Value)),
                new Dictionary<string, int>(toVersions.ToDictionary(p => p.Key, p => p.Value)),
                dependsOn ?? Array.Empty<string>());
        }

        public override string ToString()
        {
            string moves = string.Join(", ",
                this.FromVersions.Select(p => $"{p.Key} v{p.Value}->v{this.ToVersions[p.Key]}"));

            return this.IsGroup ? $"{this.Id} [group: {moves}]" : $"{this.Id} [{moves}]";
        }
    }
}
