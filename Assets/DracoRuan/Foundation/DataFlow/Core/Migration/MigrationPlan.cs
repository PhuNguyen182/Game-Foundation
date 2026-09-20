using System;
using System.Collections.Generic;
using System.Linq;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>The planner's verdict for one domain.</summary>
    public sealed class DomainPlan
    {
        public DomainPlan(
            string domainId,
            DomainPlanStatus status,
            int currentVersion,
            int targetVersion,
            string detail = null)
        {
            this.DomainId = domainId;
            this.Status = status;
            this.CurrentVersion = currentVersion;
            this.TargetVersion = targetVersion;
            this.Detail = detail;
        }

        public string DomainId { get; }
        public DomainPlanStatus Status { get; }

        /// <summary>Version found on disk, or 0 when there is no save.</summary>
        public int CurrentVersion { get; }

        /// <summary>Version this build expects.</summary>
        public int TargetVersion { get; }

        /// <summary>Human-readable explanation for the problem statuses.</summary>
        public string Detail { get; }

        /// <summary>True when the domain needs no work and blocks nothing.</summary>
        public bool IsHealthy =>
            this.Status is DomainPlanStatus.UpToDate
                or DomainPlanStatus.NoSaveData
                or DomainPlanStatus.Migrate;

        public override string ToString() =>
            $"{this.DomainId}: {this.Status} (v{this.CurrentVersion} -> v{this.TargetVersion})" +
            (string.IsNullOrEmpty(this.Detail) ? string.Empty : $" - {this.Detail}");
    }

    /// <summary>
    /// A group of domains that must be migrated as one unit, in the given step order.
    /// </summary>
    /// <remarks>
    /// A unit holds more than one domain exactly when those domains depend on each other in a cycle
    /// — the case no ordering can satisfy. The planner resolves it by not ordering them at all.
    /// </remarks>
    public sealed class MigrationUnit
    {
        public MigrationUnit(IReadOnlyList<string> domains, IReadOnlyList<MigrationStep> steps)
        {
            this.Domains = domains;
            this.Steps = steps;
        }

        /// <summary>Domains rewritten by this unit.</summary>
        public IReadOnlyList<string> Domains { get; }

        /// <summary>Steps to run, in order.</summary>
        public IReadOnlyList<MigrationStep> Steps { get; }

        /// <summary>True when the unit covers mutually dependent domains.</summary>
        public bool IsCoupled => this.Domains.Count > 1;

        public override string ToString() =>
            $"[{string.Join(" + ", this.Domains)}] {this.Steps.Count} step(s)";
    }

    /// <summary>
    /// The complete migration plan: what each domain needs, and the order to do it in.
    /// </summary>
    public sealed class MigrationPlan
    {
        public MigrationPlan(IReadOnlyList<DomainPlan> domains, IReadOnlyList<MigrationUnit> units)
        {
            this.Domains = domains;
            this.Units = units;
        }

        /// <summary>Verdict for every domain the planner was given.</summary>
        public IReadOnlyList<DomainPlan> Domains { get; }

        /// <summary>
        /// Units to execute, already ordered so every unit's dependencies run before it.
        /// </summary>
        public IReadOnlyList<MigrationUnit> Units { get; }

        /// <summary>True when at least one domain needs work.</summary>
        public bool HasWork => this.Units.Count > 0;

        /// <summary>Domains the planner could not produce a safe plan for.</summary>
        public IReadOnlyList<DomainPlan> ProblemDomains =>
            this.Domains.Where(d => !d.IsHealthy).ToList();

        /// <summary>True when every domain is healthy.</summary>
        public bool IsFullyHealthy => this.Domains.All(d => d.IsHealthy);

        public DomainPlan GetDomain(string domainId) =>
            this.Domains.FirstOrDefault(d =>
                string.Equals(d.DomainId, domainId, StringComparison.Ordinal));

        /// <summary>Multi-line summary for the boot log.</summary>
        public string Describe()
        {
            List<string> lines = new() { $"Migration plan: {this.Units.Count} unit(s)" };

            foreach (MigrationUnit unit in this.Units)
            {
                lines.Add($"  {unit}");
                foreach (MigrationStep step in unit.Steps)
                    lines.Add($"    - {step}");
            }

            foreach (DomainPlan problem in this.ProblemDomains)
                lines.Add($"  ! {problem}");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
