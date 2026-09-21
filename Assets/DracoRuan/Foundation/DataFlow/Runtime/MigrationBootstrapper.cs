using System;
using System.Collections.Generic;
using ZLinq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.Core.Migration;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using DracoRuan.Foundation.DataFlow.Sync;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// The single boot-time pass that brings every save domain up to the version this build expects.
    ///
    /// <para>Runs before any repository loads, in four stages:</para>
    /// <list type="number">
    ///   <item>import any saves still in the pre-envelope layout;</item>
    ///   <item>read each domain's header to discover the version on disk — cheap, no payload is decoded;</item>
    ///   <item>plan, condensing mutually dependent domains into units;</item>
    ///   <item>execute, writing new version files and rolling back on failure.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>Deliberately <b>not</b> an entry point. VContainer would then start it concurrently with
    /// every other <c>IAsyncStartable</c> in the same frame, with no way to make anything wait. It is
    /// a plain service that the boot pipeline awaits explicitly.</para>
    ///
    /// <para>A domain that cannot be migrated does not stop the others, and never has its save
    /// overwritten. Whether that is fatal for the session is the caller's decision, which is why the
    /// result is returned rather than thrown.</para>
    /// </remarks>
    public sealed class MigrationBootstrapper
    {
        private const string LogTag = "Migration";

        private readonly SaveEnvelopeStore _store;
        private readonly DataDomainRegistry _domains;
        private readonly MigrationRegistry _migrators;
        private readonly IPlayerIdentityProvider _identity;
        private readonly LegacySaveImporter _legacyImporter;

        public MigrationBootstrapper(
            SaveEnvelopeStore store,
            DataDomainRegistry domains,
            MigrationRegistry migrators,
            IPlayerIdentityProvider identity)
        {
            this._store = store ?? throw new ArgumentNullException(nameof(store));
            this._domains = domains ?? throw new ArgumentNullException(nameof(domains));
            this._migrators = migrators ?? throw new ArgumentNullException(nameof(migrators));
            this._identity = identity ?? throw new ArgumentNullException(nameof(identity));
            this._legacyImporter = new LegacySaveImporter(store);
        }

        /// <summary>The plan from the last run, for diagnostics screens.</summary>
        public MigrationPlan LastPlan { get; private set; }

        public async UniTask<MigrationOutcome> RunAsync(CancellationToken cancellationToken = default)
        {
            this.ImportLegacySaves();

            Dictionary<string, int> targets = this._domains.GetTargetVersions();
            Dictionary<string, int> current = this.DiscoverCurrentVersions();

            MigrationPlan plan = new MigrationPlanner().CreatePlan(targets, current, this._migrators.Steps);
            this.LastPlan = plan;

            this.LogPlan(plan);

            if (!plan.HasWork)
                return new MigrationOutcome(true, Array.Empty<string>(), Array.Empty<string>());

            MigrationExecutor executor = new(this._store, this._migrators, () => this._identity.DeviceEpochId);
            executor.OnUnitStarted += unit => Debug.Log($"[{LogTag}] Migrating {unit}");
            executor.OnUnitFailed += (_, message) => Debug.LogError($"[{LogTag}] {message}");

            MigrationOutcome outcome = await executor.ExecuteAsync(plan, this._identity.PlayerId, cancellationToken);

            // Keep only the latest few versions once the upgrade has landed, so the save directory
            // does not grow by one file per schema bump forever.
            foreach (string domainId in outcome.MigratedDomains)
                this._store.Prune(domainId);

            return outcome;
        }

        private void ImportLegacySaves()
        {
            foreach (DataDomainDescriptor descriptor in this._domains.Descriptors)
            {
                LegacyImportResult result =
                    this._legacyImporter.Import(descriptor.DomainId, descriptor.LegacyTypeName);

                if (!result.Succeeded)
                {
                    Debug.LogError($"[{LogTag}] Legacy import failed for '{descriptor.DomainId}': {result.Error}");
                    continue;
                }

                if (result.DidWork)
                {
                    Debug.Log(
                        $"[{LogTag}] Imported {result.ImportedVersions} legacy file(s) for " +
                        $"'{descriptor.DomainId}'.");
                }
            }
        }

        /// <summary>
        /// Reads what is actually on disk for each domain.
        /// </summary>
        /// <remarks>
        /// Reports the <i>highest</i> version present, including versions above what this build
        /// supports, so a save written by a newer build is recognised as a downgrade rather than
        /// mistaken for "no save" and overwritten. A domain whose header will not parse is reported
        /// as -1 so the planner can mark it corrupt instead of treating it as new.
        /// </remarks>
        private Dictionary<string, int> DiscoverCurrentVersions()
        {
            Dictionary<string, int> current = new(StringComparer.Ordinal);

            foreach (DataDomainDescriptor descriptor in this._domains.Descriptors)
            {
                IReadOnlyList<int> versions = this._store.ListVersions(descriptor.DomainId);
                if (versions.Count == 0)
                {
                    current[descriptor.DomainId] = 0;
                    continue;
                }

                int highest = versions[0];
                EnvelopeReadStatus status = this._store.ReadHeader(descriptor.DomainId, highest, out _);

                current[descriptor.DomainId] = status == EnvelopeReadStatus.Success ? highest : -1;
            }

            return current;
        }

        private void LogPlan(MigrationPlan plan)
        {
            if (plan.HasWork)
                Debug.Log($"[{LogTag}] {plan.Describe()}");

            foreach (DomainPlan problem in plan.ProblemDomains)
            {
                switch (problem.Status)
                {
                    case DomainPlanStatus.Downgrade:
                        Debug.LogError(
                            $"[{LogTag}] '{problem.DomainId}' was saved by a newer build " +
                            $"(v{problem.CurrentVersion} > v{problem.TargetVersion}). It will NOT be loaded: " +
                            "reading it here would drop the newer fields and the next save would make that " +
                            "permanent.");
                        break;

                    case DomainPlanStatus.UnresolvableCycle:
                        Debug.LogError($"[{LogTag}] {problem.Detail}");
                        break;

                    default:
                        Debug.LogError($"[{LogTag}] {problem}");
                        break;
                }
            }
        }

        /// <summary>
        /// Domains this build must not load, because doing so would destroy data.
        /// </summary>
        public IReadOnlyList<string> GetUnloadableDomains() =>
            this.LastPlan?.ProblemDomains
                .AsValueEnumerable()
                .Where(d => d.Status is DomainPlanStatus.Downgrade or DomainPlanStatus.Corrupt)
                .Select(d => d.DomainId)
                .ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }
}
