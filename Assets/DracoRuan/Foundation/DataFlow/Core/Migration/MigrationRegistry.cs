using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// Holds the registered migrators, exposes them to the planner as
    /// <see cref="MigrationStep"/>s, and maps a planned step back to the code that runs it.
    /// </summary>
    /// <remarks>
    /// The split matters: the planner reasons about metadata only, so the whole ordering and cycle
    /// analysis stays a pure function. This type is the only place where a plan is tied back to
    /// executable code.
    /// </remarks>
    public sealed class MigrationRegistry
    {
        private readonly List<MigrationStep> _steps = new();

        private readonly Dictionary<string, Func<IMigrationContext, CancellationToken, UniTask>> _executors =
            new(StringComparer.Ordinal);

        /// <summary>Steps for <see cref="MigrationPlanner.CreatePlan"/>.</summary>
        public IReadOnlyList<MigrationStep> Steps => this._steps;

        public void Register(IDataMigrator migrator)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));

            string id = BuildId(migrator.GetType(), migrator.Domain, migrator.FromVersion, migrator.ToVersion);
            this.Add(
                MigrationStep.Single(id, migrator.Domain, migrator.FromVersion, migrator.ToVersion,
                    migrator.DependsOn),
                migrator.MigrateAsync);
        }

        public void Register(IGroupDataMigrator migrator)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));

            string id = migrator.GetType().FullName ?? migrator.GetType().Name;
            this.Add(
                MigrationStep.Group(id, migrator.FromVersions, migrator.ToVersions, migrator.DependsOn),
                migrator.MigrateAsync);
        }

        /// <summary>Returns the code that runs a planned step.</summary>
        public Func<IMigrationContext, CancellationToken, UniTask> GetExecutor(string stepId) =>
            this._executors.TryGetValue(stepId, out Func<IMigrationContext, CancellationToken, UniTask> executor)
                ? executor
                : null;

        private void Add(MigrationStep step, Func<IMigrationContext, CancellationToken, UniTask> executor)
        {
            if (this._executors.ContainsKey(step.Id))
            {
                throw new ArgumentException(
                    $"A migration step with id '{step.Id}' is already registered. Ids must be unique, " +
                    "otherwise a plan cannot be mapped back to the code that runs it.");
            }

            this._steps.Add(step);
            this._executors[step.Id] = executor;
        }

        /// <summary>
        /// Includes the versions so two migrators of the same type over different version ranges —
        /// a generic base reused per step, for instance — stay distinguishable.
        /// </summary>
        private static string BuildId(Type type, string domain, int fromVersion, int toVersion) =>
            $"{type.FullName ?? type.Name}({domain} v{fromVersion}->v{toVersion})";
    }
}
