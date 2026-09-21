using System;
using System.Collections.Generic;
using ZLinq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.Core.Storage;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>Result of running a migration plan.</summary>
    public sealed class MigrationOutcome
    {
        public MigrationOutcome(
            bool succeeded,
            IReadOnlyList<string> migratedDomains,
            IReadOnlyList<string> failures,
            Exception exception = null)
        {
            this.Succeeded = succeeded;
            this.MigratedDomains = migratedDomains;
            this.Failures = failures;
            this.Exception = exception;
        }

        /// <summary>True when every scheduled unit completed.</summary>
        public bool Succeeded { get; }

        /// <summary>Domains brought up to their target version.</summary>
        public IReadOnlyList<string> MigratedDomains { get; }

        /// <summary>One message per unit that failed or was rolled back.</summary>
        public IReadOnlyList<string> Failures { get; }

        public Exception Exception { get; }
    }

    /// <summary>
    /// Runs a <see cref="MigrationPlan"/> against the save store.
    ///
    /// <para>
    /// Each step reads the payloads at its from-versions, hands them to the migrator, and writes the
    /// results as <i>new</i> version files. The source files are never touched.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para><b>Rollback is a deletion.</b> Because a migration only ever adds files, undoing one
    /// means deleting what this run wrote — the pre-migration state is still on disk, untouched and
    /// checksum-verified. That removes the entire snapshot subsystem the previous design needed, and
    /// with it the class of bug where a rollback itself is interrupted: re-deleting a file that is
    /// already gone is a no-op, so recovery is idempotent by construction.</para>
    ///
    /// <para><b>Truth lives on disk, not in a journal.</b> Nothing here consults a record of "steps
    /// already done" to decide what to skip. Version files are the state, so an interrupted run is
    /// resumed simply by planning again from what is present. A journal can disagree with the
    /// filesystem — a step marked done whose write never landed would be skipped forever, leaving a
    /// payload permanently mislabelled with a version it does not match.</para>
    ///
    /// <para><b>Units are independent.</b> One failing unit rolls back only its own writes; unrelated
    /// domains keep their successful migrations.</para>
    /// </remarks>
    public sealed class MigrationExecutor
    {
        private readonly SaveEnvelopeStore _store;
        private readonly MigrationRegistry _registry;
        private readonly Func<Guid> _deviceEpochProvider;

        public MigrationExecutor(
            SaveEnvelopeStore store,
            MigrationRegistry registry,
            Func<Guid> deviceEpochProvider = null)
        {
            this._store = store ?? throw new ArgumentNullException(nameof(store));
            this._registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this._deviceEpochProvider = deviceEpochProvider ?? (() => Guid.Empty);
        }

        /// <summary>Raised before each unit starts. Useful for a loading-screen progress readout.</summary>
        public event Action<MigrationUnit> OnUnitStarted;

        /// <summary>Raised after each unit commits.</summary>
        public event Action<MigrationUnit> OnUnitCompleted;

        /// <summary>Raised when a unit fails and its writes have been rolled back.</summary>
        public event Action<MigrationUnit, string> OnUnitFailed;

        public async UniTask<MigrationOutcome> ExecuteAsync(
            MigrationPlan plan,
            string playerId,
            CancellationToken cancellationToken = default)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            List<string> migrated = new();
            List<string> failures = new();
            Exception firstException = null;

            foreach (MigrationUnit unit in plan.Units)
            {
                cancellationToken.ThrowIfCancellationRequested();

                this.OnUnitStarted?.Invoke(unit);

                // Everything this unit writes, so a failure can undo exactly its own work.
                List<(string DomainId, int Version)> written = new();

                try
                {
                    await this.ExecuteUnitAsync(unit, playerId, written, cancellationToken);

                    migrated.AddRange(unit.Domains);
                    this.OnUnitCompleted?.Invoke(unit);
                }
                catch (OperationCanceledException)
                {
                    this.RollBack(written);
                    throw;
                }
                catch (Exception exception)
                {
                    this.RollBack(written);

                    string message = $"[{string.Join(" + ", unit.Domains)}] {exception.Message}";
                    failures.Add(message);
                    firstException ??= exception;

                    this.OnUnitFailed?.Invoke(unit, message);
                }
            }

            return new MigrationOutcome(failures.Count == 0, migrated, failures, firstException);
        }

        private async UniTask ExecuteUnitAsync(
            MigrationUnit unit,
            string playerId,
            ICollection<(string DomainId, int Version)> written,
            CancellationToken cancellationToken)
        {
            foreach (MigrationStep step in unit.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Func<IMigrationContext, CancellationToken, UniTask> executor =
                    this._registry.GetExecutor(step.Id);

                if (executor == null)
                {
                    throw new InvalidOperationException(
                        $"No executor registered for migration step '{step.Id}'. The plan and the registry " +
                        "disagree, which means they were built from different migrator sets.");
                }

                MigrationContext context = this.BuildContext(step, playerId);

                await executor(context, cancellationToken);

                // Every participating domain must have been rewritten. Silently keeping the old
                // payload under a new version number would mislabel it forever.
                foreach (string domainId in step.Domains)
                {
                    if (!context.TryGetResult(domainId, out byte[] payload))
                    {
                        throw new InvalidOperationException(
                            $"Migration step '{step.Id}' did not write a payload for '{domainId}'. " +
                            "Every domain a step declares must be given a migrated payload.");
                    }

                    int toVersion = step.ToVersions[domainId];
                    long revision = this.ReadRevision(domainId, step.FromVersions[domainId]);

                    this._store.Write(
                        domainId,
                        toVersion,
                        payload,
                        revision + 1,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        this._deviceEpochProvider(),
                        SaveEnvelopeFlags.None);

                    written.Add((domainId, toVersion));
                }
            }
        }

        private MigrationContext BuildContext(MigrationStep step, string playerId)
        {
            Dictionary<string, byte[]> payloads = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in step.FromVersions)
            {
                EnvelopeReadStatus status = this._store.Read(pair.Key, pair.Value, out _, out byte[] payload);
                if (status != EnvelopeReadStatus.Success)
                {
                    throw new InvalidOperationException(
                        $"Cannot read '{pair.Key}' v{pair.Value} to migrate it: {status}.");
                }

                payloads[pair.Key] = payload;
            }

            // Dependencies are read at their CURRENT latest version, which - because the planner
            // ordered them first - is their migrated one.
            Dictionary<string, byte[]> dependencies = new(StringComparer.Ordinal);
            foreach (string domainId in step.DependsOn)
            {
                if (payloads.ContainsKey(domainId))
                    continue;

                int? latest = this._store.GetLatestVersion(domainId);
                if (latest == null)
                    continue;

                if (this._store.Read(domainId, latest.Value, out _, out byte[] payload) ==
                    EnvelopeReadStatus.Success)
                {
                    dependencies[domainId] = payload;
                }
            }

            return new MigrationContext(playerId, payloads, dependencies);
        }

        private long ReadRevision(string domainId, int fromVersion) =>
            this._store.ReadHeader(domainId, fromVersion, out SaveEnvelopeHeader header) ==
            EnvelopeReadStatus.Success
                ? header.Revision
                : 0;

        /// <summary>
        /// Undoes a unit by deleting the version files it wrote. Idempotent: deleting a file that is
        /// already gone is a no-op, so an interrupted rollback is safe to re-run.
        /// </summary>
        private void RollBack(IEnumerable<(string DomainId, int Version)> written)
        {
            foreach ((string domainId, int version) in written.AsValueEnumerable().Reverse())
                this._store.Delete(domainId, version);
        }
    }
}
