using System.Collections.Generic;
using ZLinq;
using DracoRuan.Foundation.DataFlow.Core.Migration;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.Tests
{
    /// <summary>
    /// Covers the migration planner, including data that depends on other data.
    ///
    /// <para>
    /// A planner bug does not crash — it produces a plausible-looking plan that rewrites player
    /// saves in the wrong order or with stale inputs. Nothing downstream can detect that, which is
    /// why the ordering rules are pinned here rather than exercised by hand.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class MigrationPlannerTests
    {
        private MigrationPlanner _planner;

        [SetUp]
        public void SetUp() => this._planner = new MigrationPlanner();

        private static Dictionary<string, int> Versions(params (string Domain, int Version)[] pairs) =>
            pairs.AsValueEnumerable().ToDictionary(p => p.Domain, p => p.Version);

        private MigrationPlan Plan(
            Dictionary<string, int> targets,
            Dictionary<string, int> current,
            params MigrationStep[] steps) =>
            this._planner.CreatePlan(targets, current, steps);

        private static List<string> UnitOrder(MigrationPlan plan) =>
            plan.Units.AsValueEnumerable().Select(u => string.Join("+", u.Domains)).ToList();

        // ---------------------------------------------------------------------
        // Classification
        // ---------------------------------------------------------------------

        [Test]
        public void NoSaveFile_NeedsNoMigration()
        {
            MigrationPlan plan = this.Plan(Versions(("a", 3)), Versions());

            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.NoSaveData));
            Assert.That(plan.HasWork, Is.False);
        }

        [Test]
        public void SameVersion_IsUpToDate()
        {
            MigrationPlan plan = this.Plan(Versions(("a", 3)), Versions(("a", 3)));

            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.UpToDate));
            Assert.That(plan.HasWork, Is.False);
        }

        [Test]
        public void UnreadableSave_IsCorrupt()
        {
            MigrationPlan plan = this.Plan(Versions(("a", 3)), Versions(("a", -1)));

            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.Corrupt));
            Assert.That(plan.HasWork, Is.False);
        }

        /// <summary>
        /// The regression the envelope format would otherwise introduce: a v5 save opened by a v3
        /// build deserializes "fine", drops the newer fields, and the next autosave makes it
        /// permanent. It must never be classified as up to date.
        /// </summary>
        [Test]
        public void SaveFromANewerBuild_IsDowngradeNotUpToDate()
        {
            MigrationPlan plan = this.Plan(Versions(("a", 3)), Versions(("a", 5)));

            DomainPlan domain = plan.GetDomain("a");
            Assert.That(domain.Status, Is.EqualTo(DomainPlanStatus.Downgrade));
            Assert.That(domain.IsHealthy, Is.False);
            Assert.That(plan.HasWork, Is.False, "a downgrade must never be 'fixed' by writing");
        }

        // ---------------------------------------------------------------------
        // Simple chains
        // ---------------------------------------------------------------------

        [Test]
        public void SingleDomain_ChainsStepsInOrder()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 3)), Versions(("a", 1)),
                MigrationStep.Single("a1to2", "a", 1, 2),
                MigrationStep.Single("a2to3", "a", 2, 3));

            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.Migrate));
            Assert.That(plan.Units.AsValueEnumerable().Single().Steps.AsValueEnumerable().Select(s => s.Id).ToArray(),
                Is.EqualTo(new[] { "a1to2", "a2to3" }));
        }

        [Test]
        public void MultiVersionStep_IsAllowed()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 3)), Versions(("a", 1)),
                MigrationStep.Single("a1to3", "a", 1, 3));

            Assert.That(
                plan.Units.AsValueEnumerable().Single().Steps.AsValueEnumerable().Select(s => s.Id).ToArray(),
                Is.EqualTo(new[] { "a1to3" }));
        }

        /// <summary>
        /// The old registry spun forever here instead of reporting anything — on the main thread,
        /// before the first frame.
        /// </summary>
        [Test]
        public void MissingStepInTheMiddle_ReportsAndDoesNotHang()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 3)), Versions(("a", 1)),
                MigrationStep.Single("a1to2", "a", 1, 2));

            DomainPlan domain = plan.GetDomain("a");
            Assert.That(domain.Status, Is.EqualTo(DomainPlanStatus.MissingMigrator));
            Assert.That(domain.Detail, Does.Contain("v2"));
            Assert.That(plan.HasWork, Is.False);
        }

        [Test]
        public void StepOvershootingTheTarget_IsRejected()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 2)), Versions(("a", 1)),
                MigrationStep.Single("a1to5", "a", 1, 5));

            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.MissingMigrator));
            Assert.That(plan.GetDomain("a").Detail, Does.Contain("past the target"));
        }

        [Test]
        public void TwoCompetingStepsFromTheSameVersion_IsRejected()
        {
            // Otherwise which migrator runs depends on registration order - a coin flip over
            // the player's data.
            MigrationPlan plan = this.Plan(
                Versions(("a", 2)), Versions(("a", 1)),
                MigrationStep.Single("first", "a", 1, 2),
                MigrationStep.Single("second", "a", 1, 2));

            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.MissingMigrator));
            Assert.That(plan.GetDomain("a").Detail, Does.Contain("competing"));
        }

        // ---------------------------------------------------------------------
        // One-way dependencies
        // ---------------------------------------------------------------------

        [Test]
        public void OneWayDependency_SchedulesTheDependencyFirst()
        {
            MigrationPlan plan = this.Plan(
                Versions(("inventory", 2), ("player", 2)),
                Versions(("inventory", 1), ("player", 1)),
                MigrationStep.Single("player1to2", "player", 1, 2),
                MigrationStep.Single("inv1to2", "inventory", 1, 2, new[] { "player" }));

            List<string> order = UnitOrder(plan);
            Assert.That(order.IndexOf("player"), Is.LessThan(order.IndexOf("inventory")),
                "inventory reads player, so player must be migrated first");
        }

        [Test]
        public void DependencyChain_IsOrderedTransitively()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("b", 2), ("c", 2)),
                Versions(("a", 1), ("b", 1), ("c", 1)),
                MigrationStep.Single("c", "c", 1, 2),
                MigrationStep.Single("b", "b", 1, 2, new[] { "c" }),
                MigrationStep.Single("a", "a", 1, 2, new[] { "b" }));

            Assert.That(UnitOrder(plan), Is.EqualTo(new[] { "c", "b", "a" }));
        }

        [Test]
        public void DependencyOnADomainThatIsNotMigrating_DoesNotConstrainOrder()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("settings", 1)),
                Versions(("a", 1), ("settings", 1)),
                MigrationStep.Single("a1to2", "a", 1, 2, new[] { "settings" }));

            Assert.That(plan.GetDomain("settings").Status, Is.EqualTo(DomainPlanStatus.UpToDate));
            Assert.That(UnitOrder(plan), Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void DependingOnABrokenDomain_BlocksRatherThanMigratingAgainstStaleData()
        {
            MigrationPlan plan = this.Plan(
                Versions(("broken", 3), ("dependent", 2)),
                Versions(("broken", 1), ("dependent", 1)),
                // no step for `broken`
                MigrationStep.Single("dep1to2", "dependent", 1, 2, new[] { "broken" }));

            Assert.That(plan.GetDomain("broken").Status, Is.EqualTo(DomainPlanStatus.MissingMigrator));
            Assert.That(plan.GetDomain("dependent").Status, Is.EqualTo(DomainPlanStatus.BlockedByDependency));
            Assert.That(plan.HasWork, Is.False);
        }

        [Test]
        public void AnIndependentDomainStillMigratesWhenAnotherIsBroken()
        {
            // One gap must not take the whole boot down - the old orchestrator threw on the first
            // problem while iterating every registered domain.
            MigrationPlan plan = this.Plan(
                Versions(("broken", 3), ("healthy", 2)),
                Versions(("broken", 1), ("healthy", 1)),
                MigrationStep.Single("h1to2", "healthy", 1, 2));

            Assert.That(plan.GetDomain("broken").Status, Is.EqualTo(DomainPlanStatus.MissingMigrator));
            Assert.That(plan.GetDomain("healthy").Status, Is.EqualTo(DomainPlanStatus.Migrate));
            Assert.That(UnitOrder(plan), Is.EqualTo(new[] { "healthy" }));
        }

        // ---------------------------------------------------------------------
        // Two-way dependencies - A changes with B and B changes with A
        // ---------------------------------------------------------------------

        [Test]
        public void MutualDependency_WithGroupStep_MigratesBothTogether()
        {
            MigrationStep group = MigrationStep.Group(
                "ab2to3",
                new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 },
                new Dictionary<string, int> { ["a"] = 2, ["b"] = 2 });

            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("b", 2)),
                Versions(("a", 1), ("b", 1)),
                group);

            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.Migrate));
            Assert.That(plan.GetDomain("b").Status, Is.EqualTo(DomainPlanStatus.Migrate));

            MigrationUnit unit = plan.Units.AsValueEnumerable().Single();
            Assert.That(unit.IsCoupled, Is.True);
            Assert.That(unit.Domains, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(unit.Steps.AsValueEnumerable().Single().Id, Is.EqualTo("ab2to3"));
        }

        [Test]
        public void GroupStep_MembersNeedNotShareAVersionNumber()
        {
            MigrationStep group = MigrationStep.Group(
                "coupled",
                new Dictionary<string, int> { ["a"] = 2, ["b"] = 1 },
                new Dictionary<string, int> { ["a"] = 3, ["b"] = 2 });

            MigrationPlan plan = this.Plan(
                Versions(("a", 3), ("b", 2)),
                Versions(("a", 2), ("b", 1)),
                group);

            Assert.That(plan.Units.AsValueEnumerable().Single().Domains, Is.EqualTo(new[] { "a", "b" }));
        }

        /// <summary>
        /// The error the developer must see at build time rather than on a player's device: two
        /// domains read each other, but every step rewrites only one of them, so whichever order is
        /// chosen, one migrator reads data that has not been migrated yet.
        /// </summary>
        [Test]
        public void MutualDependency_WithoutGroupStep_IsReportedWithActionableDetail()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("b", 2)),
                Versions(("a", 1), ("b", 1)),
                MigrationStep.Single("a1to2", "a", 1, 2, new[] { "b" }),
                MigrationStep.Single("b1to2", "b", 1, 2, new[] { "a" }));

            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.UnresolvableCycle));
            Assert.That(plan.GetDomain("b").Status, Is.EqualTo(DomainPlanStatus.UnresolvableCycle));
            Assert.That(plan.HasWork, Is.False, "nothing may be written while the cycle is unresolved");

            string detail = plan.GetDomain("a").Detail;
            Assert.That(detail, Does.Contain("group migrator"), "the message must say how to fix it");
            Assert.That(detail, Does.Contain("a"));
            Assert.That(detail, Does.Contain("b"));
        }

        [Test]
        public void ThreeWayCycle_WithGroupStep_IsOneUnit()
        {
            MigrationStep group = MigrationStep.Group(
                "abc",
                new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1 },
                new Dictionary<string, int> { ["a"] = 2, ["b"] = 2, ["c"] = 2 });

            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("b", 2), ("c", 2)),
                Versions(("a", 1), ("b", 1), ("c", 1)),
                group);

            MigrationUnit unit = plan.Units.AsValueEnumerable().Single();
            Assert.That(unit.Domains, Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void ThreeWayCycle_WithoutGroupStep_IsReported()
        {
            // a -> b -> c -> a
            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("b", 2), ("c", 2)),
                Versions(("a", 1), ("b", 1), ("c", 1)),
                MigrationStep.Single("a", "a", 1, 2, new[] { "b" }),
                MigrationStep.Single("b", "b", 1, 2, new[] { "c" }),
                MigrationStep.Single("c", "c", 1, 2, new[] { "a" }));

            foreach (string domain in new[] { "a", "b", "c" })
                Assert.That(plan.GetDomain(domain).Status, Is.EqualTo(DomainPlanStatus.UnresolvableCycle),
                    $"{domain} is part of the cycle");
        }

        [Test]
        public void CoupledDomains_CanAlsoHaveIndependentStepsBeforeTheGroupStep()
        {
            // `a` advances alone first, then a and b are rewritten together.
            MigrationStep group = MigrationStep.Group(
                "ab",
                new Dictionary<string, int> { ["a"] = 2, ["b"] = 1 },
                new Dictionary<string, int> { ["a"] = 3, ["b"] = 2 });

            MigrationPlan plan = this.Plan(
                Versions(("a", 3), ("b", 2)),
                Versions(("a", 1), ("b", 1)),
                MigrationStep.Single("a1to2", "a", 1, 2),
                group);

            MigrationUnit unit = plan.Units.AsValueEnumerable().Single();
            Assert.That(unit.Domains, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(unit.Steps.AsValueEnumerable().Select(s => s.Id).ToArray(), Is.EqualTo(new[] { "a1to2", "ab" }),
                "the solo step must run before the coupled one");
        }

        [Test]
        public void CoupledUnit_IsStillOrderedAgainstItsExternalDependencies()
        {
            MigrationStep group = MigrationStep.Group(
                "ab",
                new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 },
                new Dictionary<string, int> { ["a"] = 2, ["b"] = 2 },
                new[] { "player" });

            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("b", 2), ("player", 2)),
                Versions(("a", 1), ("b", 1), ("player", 1)),
                MigrationStep.Single("p1to2", "player", 1, 2),
                group);

            List<string> order = UnitOrder(plan);
            Assert.That(order.IndexOf("player"), Is.LessThan(order.IndexOf("a+b")));
        }

        [Test]
        public void CoupledGroupStepsThatDisagreeOnVersions_AreReported()
        {
            // The group expects b at v1, but b's own chain never puts it there.
            MigrationStep group = MigrationStep.Group(
                "ab",
                new Dictionary<string, int> { ["a"] = 1, ["b"] = 5 },
                new Dictionary<string, int> { ["a"] = 2, ["b"] = 6 });

            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("b", 6)),
                Versions(("a", 1), ("b", 1)),
                group);

            // `b` never reaches v5, so its chain cannot be resolved at all.
            Assert.That(plan.GetDomain("b").Status, Is.EqualTo(DomainPlanStatus.MissingMigrator));

            // And `a` must NOT be migrated on its own: the step rewrites a and b together, so
            // running it with b unmigrated would rewrite one half of a coupled pair.
            Assert.That(plan.GetDomain("a").Status, Is.EqualTo(DomainPlanStatus.BlockedByDependency));
            Assert.That(plan.HasWork, Is.False);
        }

        // ---------------------------------------------------------------------
        // Determinism
        // ---------------------------------------------------------------------

        [Test]
        public void PlanIsDeterministic_RegardlessOfStepRegistrationOrder()
        {
            MigrationStep player = MigrationStep.Single("p", "player", 1, 2);
            MigrationStep inventory = MigrationStep.Single("i", "inventory", 1, 2, new[] { "player" });
            MigrationStep quest = MigrationStep.Single("q", "quest", 1, 2, new[] { "inventory" });

            Dictionary<string, int> targets = Versions(("player", 2), ("inventory", 2), ("quest", 2));
            Dictionary<string, int> current = Versions(("player", 1), ("inventory", 1), ("quest", 1));

            List<string> forward = UnitOrder(this._planner.CreatePlan(targets, current,
                new[] { player, inventory, quest }));
            List<string> reversed = UnitOrder(this._planner.CreatePlan(targets, current,
                new[] { quest, inventory, player }));

            Assert.That(forward, Is.EqualTo(new[] { "player", "inventory", "quest" }));
            Assert.That(reversed, Is.EqualTo(forward));
        }

        [Test]
        public void IndependentDomains_AreAllScheduled()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("b", 2), ("c", 2)),
                Versions(("a", 1), ("b", 1), ("c", 1)),
                MigrationStep.Single("a", "a", 1, 2),
                MigrationStep.Single("b", "b", 1, 2),
                MigrationStep.Single("c", "c", 1, 2));

            Assert.That(plan.Units, Has.Count.EqualTo(3));
            Assert.That(plan.IsFullyHealthy, Is.True);
        }

        [Test]
        public void Describe_MentionsUnitsAndProblems()
        {
            MigrationPlan plan = this.Plan(
                Versions(("a", 2), ("broken", 2)),
                Versions(("a", 1), ("broken", 1)),
                MigrationStep.Single("a1to2", "a", 1, 2));

            string description = plan.Describe();
            Assert.That(description, Does.Contain("a1to2"));
            Assert.That(description, Does.Contain("broken"));
        }

        // ---------------------------------------------------------------------
        // Step construction guards
        // ---------------------------------------------------------------------

        [Test]
        public void Step_MustMoveForward()
        {
            Assert.That(() => MigrationStep.Single("bad", "a", 2, 2), Throws.ArgumentException);
            Assert.That(() => MigrationStep.Single("bad", "a", 3, 2), Throws.ArgumentException);
        }

        [Test]
        public void GroupStep_MustCoverTheSameDomainsOnBothSides()
        {
            Assert.That(() => MigrationStep.Group("bad",
                    new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 },
                    new Dictionary<string, int> { ["a"] = 2 }),
                Throws.ArgumentException);
        }
    }
}