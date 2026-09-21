using System;
using System.Collections.Generic;
using ZLinq;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// Works out what has to happen to bring every save domain up to the version this build expects,
    /// and in what order.
    ///
    /// <para>The pipeline is four passes:</para>
    /// <list type="number">
    ///   <item>classify each domain from its on-disk version versus its target;</item>
    ///   <item>resolve a chain of <see cref="MigrationStep"/>s per domain that needs one;</item>
    ///   <item>condense the dependency graph into strongly connected components, so mutually
    ///         dependent domains become one unit instead of an unsolvable ordering problem;</item>
    ///   <item>order the units so every dependency runs first.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>Pure: on-disk versions in, plan out. No files, no async, no Unity. That is deliberate —
    /// the failure mode here is a <i>wrong plan</i>, not a crash, and a wrong plan silently rewrites
    /// player data. It has to be exhaustively testable.</para>
    ///
    /// <para>The planner never throws for bad <i>data</i>. Every problem becomes a
    /// <see cref="DomainPlanStatus"/> against the affected domain, so one broken domain cannot take
    /// the boot down with it. It does throw for bad <i>configuration</i> — a null argument — because
    /// that is a programming error, not a device condition.</para>
    /// </remarks>
    public sealed class MigrationPlanner
    {
        /// <summary>
        /// Builds a plan.
        /// </summary>
        /// <param name="targetVersions">Schema version this build expects, per domain.</param>
        /// <param name="currentVersions">
        /// Version found on disk, per domain. Omit a domain, or pass 0, when it has no save file.
        /// Pass a negative value to mark it unreadable.
        /// </param>
        /// <param name="steps">Every registered migration step.</param>
        public MigrationPlan CreatePlan(
            IReadOnlyDictionary<string, int> targetVersions,
            IReadOnlyDictionary<string, int> currentVersions,
            IReadOnlyCollection<MigrationStep> steps)
        {
            if (targetVersions == null) throw new ArgumentNullException(nameof(targetVersions));
            if (currentVersions == null) throw new ArgumentNullException(nameof(currentVersions));
            if (steps == null) throw new ArgumentNullException(nameof(steps));

            Dictionary<string, DomainPlan> plans = new(StringComparer.Ordinal);
            Dictionary<string, List<MigrationStep>> chains = new(StringComparer.Ordinal);

            // ---- Pass 1 & 2: classify, then resolve a chain for whatever needs migrating ----
            foreach (KeyValuePair<string, int> target in targetVersions)
            {
                string domain = target.Key;
                int targetVersion = target.Value;
                int current = currentVersions.TryGetValue(domain, out int found) ? found : 0;

                if (current < 0)
                {
                    plans[domain] = new DomainPlan(domain, DomainPlanStatus.Corrupt, 0, targetVersion,
                        "Save file exists but could not be decoded.");
                    continue;
                }

                if (current == 0)
                {
                    plans[domain] = new DomainPlan(domain, DomainPlanStatus.NoSaveData, 0, targetVersion);
                    continue;
                }

                if (current == targetVersion)
                {
                    plans[domain] = new DomainPlan(domain, DomainPlanStatus.UpToDate, current, targetVersion);
                    continue;
                }

                if (current > targetVersion)
                {
                    plans[domain] = new DomainPlan(domain, DomainPlanStatus.Downgrade, current, targetVersion,
                        $"Save is at v{current} but this build supports v{targetVersion}. " +
                        "Loading it would drop every field added since, and the next save would make that permanent.");
                    continue;
                }

                if (TryResolveChain(domain, current, targetVersion, steps, out List<MigrationStep> chain,
                        out string failure))
                {
                    chains[domain] = chain;
                    plans[domain] = new DomainPlan(domain, DomainPlanStatus.Migrate, current, targetVersion);
                }
                else
                {
                    plans[domain] = new DomainPlan(domain, DomainPlanStatus.MissingMigrator, current,
                        targetVersion, failure);
                }
            }

            // ---- Pass 3: condense the dependency graph ----
            List<string> migrating = chains.Keys.AsValueEnumerable().ToList();
            migrating.Sort(StringComparer.Ordinal);

            Dictionary<string, HashSet<string>> edges = BuildDependencyGraph(migrating, chains);

            List<List<string>> components = StronglyConnectedComponents.Find(
                migrating,
                node => edges.TryGetValue(node, out HashSet<string> targets)
                    ? targets
                    : Array.Empty<string>());

            // ---- Pass 4: validate each component and emit units in dependency order ----
            List<MigrationUnit> units = new();
            HashSet<string> blocked = new(StringComparer.Ordinal);

            foreach (List<string> component in components)
            {
                // A component depending on an unhealthy domain cannot run: it would read data that
                // was never upgraded. Cascade rather than silently migrate against stale input.
                string blocker = FindBlocker(component, chains, plans, blocked);
                if (blocker != null)
                {
                    foreach (string domain in component)
                    {
                        blocked.Add(domain);
                        plans[domain] = new DomainPlan(domain, DomainPlanStatus.BlockedByDependency,
                            plans[domain].CurrentVersion, plans[domain].TargetVersion,
                            $"Depends on '{blocker}', which cannot be migrated.");
                    }

                    continue;
                }

                if (component.Count == 1)
                {
                    string domain = component[0];
                    units.Add(new MigrationUnit(component, chains[domain]));
                    continue;
                }

                // More than one domain in a component means they depend on each other, so no order
                // between them exists. The only valid resolution is steps that rewrite them together.
                if (TryOrderCoupledComponent(component, chains, currentVersions, out List<MigrationStep> ordered,
                        out string cycleFailure))
                {
                    units.Add(new MigrationUnit(component, ordered));
                }
                else
                {
                    foreach (string domain in component)
                    {
                        blocked.Add(domain);
                        plans[domain] = new DomainPlan(domain, DomainPlanStatus.UnresolvableCycle,
                            plans[domain].CurrentVersion, plans[domain].TargetVersion, cycleFailure);
                    }
                }
            }

            List<DomainPlan> ordered2 = plans.Values.AsValueEnumerable()
                .OrderBy(p => p.DomainId, StringComparer.Ordinal)
                .ToList();
            return new MigrationPlan(ordered2, units);
        }

        /// <summary>
        /// Walks a domain from its current version to its target, one step at a time.
        /// </summary>
        /// <remarks>
        /// The loop advances on every iteration or returns. The old registry had a
        /// <c>continue</c> that skipped a step without advancing the version, which spun forever on
        /// the main thread before the first frame — a black screen with no diagnostics.
        /// </remarks>
        private static bool TryResolveChain(
            string domain,
            int currentVersion,
            int targetVersion,
            IReadOnlyCollection<MigrationStep> steps,
            out List<MigrationStep> chain,
            out string failure)
        {
            chain = new List<MigrationStep>();
            int version = currentVersion;

            while (version < targetVersion)
            {
                List<MigrationStep> applicable = steps.AsValueEnumerable()
                    .Where(s => s.FromVersions.TryGetValue(domain, out int from) && from == version)
                    .Where(s => s.ToVersions[domain] <= targetVersion)
                    .ToList();

                if (applicable.Count == 0)
                {
                    bool overshoots = steps.AsValueEnumerable().Any(s =>
                        s.FromVersions.TryGetValue(domain, out int from) && from == version);

                    failure = overshoots
                        ? $"The only migrator from '{domain}' v{version} goes past the target v{targetVersion}."
                        : $"No migrator advances '{domain}' from v{version}. " +
                          $"Need an unbroken chain v{currentVersion} -> v{targetVersion}.";

                    chain = null;
                    return false;
                }

                if (applicable.Count > 1)
                {
                    failure = $"'{domain}' v{version} has {applicable.Count} competing migrators " +
                              $"({string.Join(", ", applicable.AsValueEnumerable().Select(s => s.Id).ToArray())}). " +
                              "Exactly one must apply, " +
                              "otherwise which one runs depends on registration order.";
                    chain = null;
                    return false;
                }

                MigrationStep step = applicable[0];
                chain.Add(step);
                version = step.ToVersions[domain];
            }

            failure = null;
            return true;
        }

        /// <summary>
        /// Builds "domain depends on domain" edges from declared dependencies and from group
        /// membership.
        /// </summary>
        /// <remarks>
        /// Group membership is recorded as edges in <i>both</i> directions. That deliberately
        /// creates a cycle, so the SCC pass pulls the members into one component and they can only
        /// ever be scheduled together.
        /// </remarks>
        private static Dictionary<string, HashSet<string>> BuildDependencyGraph(
            IReadOnlyCollection<string> migrating,
            IReadOnlyDictionary<string, List<MigrationStep>> chains)
        {
            HashSet<string> migratingSet = new(migrating, StringComparer.Ordinal);
            Dictionary<string, HashSet<string>> edges = new(StringComparer.Ordinal);

            HashSet<string> EdgesFor(string node)
            {
                if (!edges.TryGetValue(node, out HashSet<string> set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    edges[node] = set;
                }

                return set;
            }

            foreach (string domain in migrating)
            {
                EdgesFor(domain);

                foreach (MigrationStep step in chains[domain])
                {
                    foreach (string dependency in step.DependsOn)
                    {
                        // Only dependencies that are themselves migrating constrain the order.
                        if (migratingSet.Contains(dependency) &&
                            !string.Equals(dependency, domain, StringComparison.Ordinal))
                        {
                            EdgesFor(domain).Add(dependency);
                        }
                    }

                    if (!step.IsGroup)
                        continue;

                    foreach (string member in step.Domains)
                    {
                        if (string.Equals(member, domain, StringComparison.Ordinal) ||
                            !migratingSet.Contains(member))
                            continue;

                        EdgesFor(domain).Add(member);
                        EdgesFor(member).Add(domain);
                    }
                }
            }

            return edges;
        }

        /// <summary>
        /// Returns the first unhealthy domain a component depends on, or <c>null</c>.
        /// </summary>
        private static string FindBlocker(
            IReadOnlyCollection<string> component,
            IReadOnlyDictionary<string, List<MigrationStep>> chains,
            IReadOnlyDictionary<string, DomainPlan> plans,
            IReadOnlyCollection<string> blocked)
        {
            HashSet<string> inComponent = new(component, StringComparer.Ordinal);

            foreach (string domain in component)
            {
                foreach (MigrationStep step in chains[domain])
                {
                    // Declared reads, plus the other members of a group step. A group rewrites its
                    // members together, so a member that is not being migrated - because its own
                    // chain could not be resolved - makes the whole step unrunnable. Checking only
                    // DependsOn would schedule it anyway and rewrite one side of a coupled pair.
                    foreach (string dependency in
                             step.DependsOn.AsValueEnumerable().Concat(step.Domains.AsValueEnumerable()))
                    {
                        if (inComponent.Contains(dependency))
                            continue;

                        if (blocked.AsValueEnumerable().Contains(dependency))
                            return dependency;

                        // A dependency this build knows nothing about is a configuration error, but
                        // an unhealthy known one is the common case.
                        if (plans.TryGetValue(dependency, out DomainPlan plan) && !plan.IsHealthy)
                            return dependency;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Produces an execution order for mutually dependent domains.
        /// </summary>
        /// <remarks>
        /// <para>Replays the component forward one step at a time. A step is <i>applicable</i> when
        /// every member it touches sits at its declared from-version, and <i>runnable</i> when it is
        /// applicable and none of its in-component dependencies still has an applicable step of its
        /// own — because running it then would read data that is about to change.</para>
        ///
        /// <para>That distinction is what separates a resolvable cycle from an unresolvable one.
        /// Domains being coupled at <i>some</i> version step does not by itself make an earlier
        /// single-domain step ambiguous: if A can advance on its own first, B may then read it
        /// safely. Only when every applicable step is waiting on another applicable step is there no
        /// correct order at all, and that is reported rather than guessed at.</para>
        ///
        /// <para>Each applied step strictly advances at least one domain, so this terminates.</para>
        /// </remarks>
        private static bool TryOrderCoupledComponent(
            IReadOnlyList<string> component,
            IReadOnlyDictionary<string, List<MigrationStep>> chains,
            IReadOnlyDictionary<string, int> currentVersions,
            out List<MigrationStep> ordered,
            out string failure)
        {
            HashSet<string> members = new(component, StringComparer.Ordinal);

            Dictionary<string, int> state = new(StringComparer.Ordinal);
            foreach (string domain in component)
                state[domain] = currentVersions.TryGetValue(domain, out int v) ? v : 0;

            List<MigrationStep> remaining = component.AsValueEnumerable()
                .SelectMany(d => chains[d].AsValueEnumerable())
                .Distinct()
                .ToList();

            ordered = new List<MigrationStep>();

            while (remaining.Count > 0)
            {
                List<MigrationStep> applicable = remaining.AsValueEnumerable()
                    .Where(step => step.FromVersions.AsValueEnumerable().All(pair =>
                        !members.Contains(pair.Key) ||
                        (state.TryGetValue(pair.Key, out int at) && at == pair.Value)))
                    .ToList();

                if (applicable.Count == 0)
                {
                    ordered = null;
                    failure =
                        $"'{string.Join("' and '", component)}' depend on each other, but their migration steps " +
                        "never line up: no remaining step has all of its domains at the required version " +
                        $"(currently {string.Join(", ", state.AsValueEnumerable().Select(p => $"{p.Key} v{p.Value}").ToArray())}). " +
                        "Group migrators across these domains must agree on each other's from-versions.";
                    return false;
                }

                MigrationStep next = applicable.AsValueEnumerable().FirstOrDefault(step =>
                    !HasPendingInComponentDependency(step, applicable, members));

                if (next == null)
                {
                    // Every applicable step is waiting on another applicable step, so whichever runs
                    // first reads data the other is about to rewrite. Only a step covering them
                    // together can be correct.
                    ordered = null;
                    failure =
                        $"'{string.Join("' and '", component)}' depend on each other, so no migration order " +
                        "is correct for all of them: " +
                        string.Join("; ", applicable.AsValueEnumerable().Select(s =>
                            $"'{s.Id}' migrates [{string.Join(", ", s.Domains)}] but reads " +
                            $"[{string.Join(", ", s.DependsOn.AsValueEnumerable().Where(members.Contains).ToArray())}]")
                            .ToArray()) +
                        $". Replace these with a single group migrator covering exactly " +
                        $"[{string.Join(", ", component)}].";
                    return false;
                }

                foreach (KeyValuePair<string, int> pair in next.ToVersions)
                {
                    if (members.Contains(pair.Key))
                        state[pair.Key] = pair.Value;
                }

                ordered.Add(next);
                remaining.Remove(next);
            }

            failure = null;
            return true;
        }

        /// <summary>
        /// True when a step reads an in-component domain that still has work ready to run, so the
        /// step would observe a value that is about to change.
        /// </summary>
        private static bool HasPendingInComponentDependency(
            MigrationStep step,
            IEnumerable<MigrationStep> applicable,
            ICollection<string> members)
        {
            HashSet<string> dependencies = new(
                step.DependsOn.AsValueEnumerable()
                    .Where(d => members.Contains(d) && !step.FromVersions.ContainsKey(d))
                    .ToArray(),
                StringComparer.Ordinal);

            if (dependencies.Count == 0)
                return false;

            foreach (MigrationStep other in applicable)
            {
                if (ReferenceEquals(other, step))
                    continue;

                if (other.Domains.AsValueEnumerable().Any(dependencies.Contains))
                    return true;
            }

            return false;
        }
    }
}