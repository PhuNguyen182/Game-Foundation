using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Manifest;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Exceptions;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies.Resolver;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Snapshot;
using DracoRuan.Foundation.DataFlow.DataMigration.Migrator;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Orchestrator
{
    public class DataMigrationOrchestrator
    {
        private readonly MigrationRegistry _registry;
        private readonly MigrationResolver _resolver;
        private readonly SnapshotManager _snapshots;
        private readonly MigrationManifestStorage _manifestStorage;

        public RollbackStrategy Strategy { get; set; }

        public event Action<string, int, int> OnMigrationStepStarted;
        public event Action<string, int, int> OnMigrationStepCompleted;
        public event Action<string, int, int, string> OnMigrationStepFailed;

        public DataMigrationOrchestrator(MigrationRegistry registry, MigrationResolver resolver, 
            SnapshotManager snapshots, MigrationManifestStorage manifestStorage)
        {
            this._registry = registry;
            this._resolver = resolver;
            this._snapshots = snapshots;
            this._manifestStorage = manifestStorage;
        }

        public async UniTask<MigrationResult> MigrateData(MigrationContext context)
        {
            if (context.CurrentVersion >= context.TargetVersion)
            {
                Debug.Log($"[Orchestrator] Data has been updated to v{context.CurrentVersion}, no need to migrate!");
                return MigrationResult.Succeeded();
            }

            Debug.Log($"[Orchestrator] Migration start {context.PlayerId}: v{context.CurrentVersion} → v{context.TargetVersion}");
            MigrationManifest manifest = this._manifestStorage.Load(context.PlayerId) ?? this.CreateManifest(context);
            string fullSnapshotKey = this._snapshots.TakeFullSnapshot(context);

            try
            {
                List<MigrationUnit> units = this.BuildMigrationUnits(context);
                if (units.Count == 0)
                {
                    Debug.Log("[Orchestrator] No domain need migration.");
                    return MigrationResult.Succeeded();
                }

                this._resolver.ValidateDependenciesExist(units, this._registry);
                List<MigrationUnit> ordered = this._resolver.BuildTopologicalSortedMigrations(units);
                Debug.Log($"[Orchestrator] Migration order: {string.Join(" → ", GetDomainNames(ordered))}");

                foreach (MigrationUnit unit in ordered)
                {
                    MigrationResult unitResult = await this.RunUnitAsync(unit, context, manifest, fullSnapshotKey);
                    if (unitResult.IsSuccess) 
                        continue;
                    
                    if (this.Strategy == RollbackStrategy.Fully)
                    {
                        Debug.LogError($"[Orchestrator] Domain '{unit.Domain}' failed! Rollback-ing ...");
                        return MigrationResult.Failed(
                            $"Migration failed at domain '{unit.Domain}': {unitResult.ErrorMessage}",
                            unitResult.Exception);
                    }

                    Debug.LogWarning($"[Orchestrator] Domain '{unit.Domain}' failed, continue ...");
                }

                manifest.isMigrationCompleted = true;
                manifest.completedAt = DateTime.UtcNow.ToString("O");
                this._manifestStorage.Save(manifest);
                this._snapshots.ClearAll();

                Debug.Log($"[Orchestrator] Migration complete: {context.PlayerId} → v{context.TargetVersion}");
                return MigrationResult.Succeeded();
            }
            catch (MigrationException ex)
            {
                Debug.LogError($"[Orchestrator] Migration error: {ex.Message}");
                return MigrationResult.Failed(ex.Message, ex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Orchestrator] Unexpected error: {ex}");
                return MigrationResult.Failed("Unexpected error while migration.", ex);
            }
        }

        private async UniTask<MigrationResult> RunUnitAsync(MigrationUnit unit, MigrationContext context,
            MigrationManifest manifest, string fullSnapshotKey)
        {
            foreach (IDataMigrator migrator in unit.MigratorChain)
            {
                if (manifest.IsStepCompleted(migrator.Domain, migrator.FromVersion, migrator.ToVersion))
                {
                    Debug.Log($"[Orchestrator] Skip (completed): {migrator.Domain} " +
                              $"v{migrator.FromVersion} → v{migrator.ToVersion}");
                    continue;
                }

                string snapshotKey = this._snapshots.TakeSnapshot(context, migrator.Domain, migrator.FromVersion);
                manifest.GetOrCreateStep(migrator.Domain, migrator.FromVersion, migrator.ToVersion, snapshotKey);
                this._manifestStorage.Save(manifest);

                this.OnMigrationStepStarted?.Invoke(migrator.Domain, migrator.FromVersion, migrator.ToVersion);
                Debug.Log($"[Orchestrator] Run: {migrator.Domain} v{migrator.FromVersion} → v{migrator.ToVersion}");

                MigrationResult result;
                try
                {
                    result = await migrator.MigrateData(context);
                }
                catch (Exception ex)
                {
                    result = MigrationResult.Failed($"Exception: {ex.Message}", ex);
                }

                if (!result.IsSuccess)
                {
                    OnMigrationStepFailed?.Invoke(
                        migrator.Domain, migrator.FromVersion, migrator.ToVersion, result.ErrorMessage);
                    Debug.LogError($"[Orchestrator] Failed: {migrator.Domain} v{migrator.FromVersion} → v{migrator.ToVersion} – {result.ErrorMessage}");
                    return result;
                }

                bool valid;
                try
                {
                    valid = await migrator.ValidateData(context);
                }
                catch (Exception ex)
                {
                    valid = false;
                    Debug.LogError($"[Orchestrator] Validate exception: {ex.Message}");
                }

                if (!valid)
                {
                    this.OnMigrationStepFailed?.Invoke(migrator.Domain, migrator.FromVersion, migrator.ToVersion, "Validation failed");
                    return MigrationResult.Failed($"Validation failed after: {migrator.Domain} v{migrator.FromVersion} → v{migrator.ToVersion}");
                }

                manifest.MarkStepCompleted(migrator.Domain, migrator.FromVersion, migrator.ToVersion);
                this._manifestStorage.Save(manifest);
                this.OnMigrationStepCompleted?.Invoke(migrator.Domain, migrator.FromVersion, migrator.ToVersion);
                Debug.Log($"[Orchestrator] Completed: {migrator.Domain} v{migrator.FromVersion} → v{migrator.ToVersion}");
            }

            return MigrationResult.Succeeded();
        }

        private List<MigrationUnit> BuildMigrationUnits(MigrationContext context)
        {
            List<MigrationUnit> units = new();
            foreach (string domain in this._registry.GetAllDomains())
            {
                int domainVersion = this.GetDomainVersion(context, domain);
                int latestVersion = this._registry.GetLatestVersionOfDomain(domain);

                if (domainVersion >= latestVersion)
                {
                    Debug.Log($"[Orchestrator] Domain '{domain}' is updated to v{domainVersion}, skip.");
                    continue;
                }

                List<IDataMigrator> chain = this._registry.GetDataMigratorChain(domain, domainVersion, latestVersion);
                units.Add(new MigrationUnit(domain, chain));
            }

            return units;
        }

        private int GetDomainVersion(MigrationContext context, string domain)
        {
            string key = $"version_{domain}";
            object version = context.GetSharedData<object>(key);
            if (version is int versionNumber)
            {
                return versionNumber;
            }

            return context.CurrentVersion; // fallback: dùng global version
        }

        private MigrationManifest CreateManifest(MigrationContext context)
        {
            MigrationManifest manifest = new()
            {
                playerId = context.PlayerId,
                startVersion = context.CurrentVersion,
                targetVersion = context.TargetVersion,
                startedAt = DateTime.UtcNow.ToString("O")
            };

            this._manifestStorage.Save(manifest);
            return manifest;
        }

        private static IEnumerable<string> GetDomainNames(IEnumerable<MigrationUnit> units)
        {
            foreach (MigrationUnit unit in units)
                yield return unit.Domain;
        }
    }
}