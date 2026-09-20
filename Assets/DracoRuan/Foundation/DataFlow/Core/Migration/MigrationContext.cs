using System;
using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// The payload view handed to a migrator while a step runs.
    /// </summary>
    /// <remarks>
    /// Reading a domain the step did not declare throws rather than returning empty. A silent
    /// failure here is the dangerous one: the planner ordered the run on the assumption that the
    /// declarations are complete, so an undeclared read observes whatever version happens to be on
    /// disk at that moment — possibly not yet migrated — and produces wrong data with no symptom.
    /// </remarks>
    internal sealed class MigrationContext : IMigrationContext
    {
        private readonly Dictionary<string, byte[]> _payloads;
        private readonly Dictionary<string, byte[]> _dependencies;
        private readonly Dictionary<string, byte[]> _results = new(StringComparer.Ordinal);

        public MigrationContext(
            string playerId,
            Dictionary<string, byte[]> payloads,
            Dictionary<string, byte[]> dependencies)
        {
            this.PlayerId = playerId;
            this._payloads = payloads;
            this._dependencies = dependencies;
        }

        public string PlayerId { get; }

        public IReadOnlyCollection<string> Domains => this._payloads.Keys;

        public ReadOnlySpan<byte> GetPayload(string domainId)
        {
            if (!this._payloads.TryGetValue(domainId, out byte[] payload))
            {
                throw new InvalidOperationException(
                    $"'{domainId}' is not part of this migration step. Step domains: " +
                    $"[{string.Join(", ", this._payloads.Keys)}].");
            }

            return payload;
        }

        public void SetPayload(string domainId, byte[] payload)
        {
            if (!this._payloads.ContainsKey(domainId))
            {
                throw new InvalidOperationException(
                    $"'{domainId}' is not part of this migration step, so it cannot be written. " +
                    $"Step domains: [{string.Join(", ", this._payloads.Keys)}].");
            }

            this._results[domainId] = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public ReadOnlySpan<byte> GetDependencyPayload(string domainId)
        {
            if (this._dependencies.TryGetValue(domainId, out byte[] payload))
                return payload;

            if (this._payloads.TryGetValue(domainId, out byte[] own))
                return own;

            throw new InvalidOperationException(
                $"'{domainId}' was not declared as a dependency of this migration step, so the planner " +
                "did not guarantee it is migrated before this step runs. Add it to DependsOn.");
        }

        public bool TryGetResult(string domainId, out byte[] payload) =>
            this._results.TryGetValue(domainId, out payload);
    }
}
