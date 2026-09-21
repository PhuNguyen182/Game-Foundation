using System;
using System.Collections.Generic;
using System.IO;
using ZLinq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.Core.Migration;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.Tests
{
    /// <summary>
    /// End-to-end migration against a real directory: plan, execute, roll back.
    ///
    /// <para>
    /// This proves the whole migration story — including the case where the app is killed
    /// mid-migration — before any of it is wired to the game.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class MigrationExecutorTests
    {
        private string _directory;
        private SaveEnvelopeStore _store;
        private MigrationRegistry _registry;
        private MigrationPlanner _planner;

        [SetUp]
        public void SetUp()
        {
            this._directory = Path.Combine(Path.GetTempPath(), "dataflow-exec-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this._directory);
            this._store = new SaveEnvelopeStore(this._directory);
            this._registry = new MigrationRegistry();
            this._planner = new MigrationPlanner();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(this._directory))
                    Directory.Delete(this._directory, recursive: true);
            }
            catch (Exception)
            {
                // Never fail a run over a leaked temp directory.
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private void Seed(string domain, int version, string body) =>
            this._store.Write(domain, version, Encoding.UTF8.GetBytes(body), 1,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Guid.Empty);

        private string ReadBody(string domain, int version)
        {
            EnvelopeReadStatus status = this._store.Read(domain, version, out _, out byte[] payload);
            return status == EnvelopeReadStatus.Success ? Encoding.UTF8.GetString(payload) : null;
        }

        private MigrationOutcome Run(Dictionary<string, int> targets, Dictionary<string, int> current)
        {
            MigrationPlan plan = this._planner.CreatePlan(targets, current, this._registry.Steps);
            MigrationExecutor executor = new(this._store, this._registry);
            return executor.ExecuteAsync(plan, "player-1").GetAwaiter().GetResult();
        }

        private Dictionary<string, int> DiscoverVersions(params string[] domains) =>
            domains.AsValueEnumerable().ToDictionary(d => d, d => this._store.GetLatestVersion(d) ?? 0);

        /// <summary>A migrator that appends a marker, so the transform is observable.</summary>
        private sealed class AppendMigrator : IDataMigrator
        {
            private readonly string _suffix;
            private readonly Action _onRun;

            public AppendMigrator(string domain, int from, int to, string suffix,
                IReadOnlyList<string> dependsOn = null, Action onRun = null)
            {
                this.Domain = domain;
                this.FromVersion = from;
                this.ToVersion = to;
                this._suffix = suffix;
                this.DependsOn = dependsOn ?? Array.Empty<string>();
                this._onRun = onRun;
            }

            public string Domain { get; }
            public int FromVersion { get; }
            public int ToVersion { get; }
            public IReadOnlyList<string> DependsOn { get; }

            public UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken)
            {
                this._onRun?.Invoke();
                string body = Encoding.UTF8.GetString(context.GetPayload(this.Domain));
                context.SetPayload(this.Domain, Encoding.UTF8.GetBytes(body + this._suffix));
                return UniTask.CompletedTask;
            }
        }

        private sealed class ThrowingMigrator : IDataMigrator
        {
            public ThrowingMigrator(string domain, int from, int to)
            {
                this.Domain = domain;
                this.FromVersion = from;
                this.ToVersion = to;
            }

            public string Domain { get; }
            public int FromVersion { get; }
            public int ToVersion { get; }
            public IReadOnlyList<string> DependsOn => Array.Empty<string>();

            public UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("migrator blew up");
        }

        /// <summary>Rewrites two coupled domains, each reading the other's pre-migration payload.</summary>
        private sealed class CoupledMigrator : IGroupDataMigrator
        {
            public CoupledMigrator(string a, string b, int from, int to)
            {
                this.A = a;
                this.B = b;
                this.FromVersions = new Dictionary<string, int> { [a] = from, [b] = from };
                this.ToVersions = new Dictionary<string, int> { [a] = to, [b] = to };
            }

            private string A { get; }
            private string B { get; }

            public IReadOnlyDictionary<string, int> FromVersions { get; }
            public IReadOnlyDictionary<string, int> ToVersions { get; }
            public IReadOnlyList<string> DependsOn => Array.Empty<string>();

            public UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken)
            {
                string a = Encoding.UTF8.GetString(context.GetPayload(this.A));
                string b = Encoding.UTF8.GetString(context.GetPayload(this.B));

                // Each side is derived from the other - only possible because both are rewritten
                // in the same step, from a consistent snapshot.
                context.SetPayload(this.A, Encoding.UTF8.GetBytes($"{a}+from({b})"));
                context.SetPayload(this.B, Encoding.UTF8.GetBytes($"{b}+from({a})"));
                return UniTask.CompletedTask;
            }
        }

        private sealed class IncompleteMigrator : IGroupDataMigrator
        {
            public IncompleteMigrator(string a, string b)
            {
                this.A = a;
                this.FromVersions = new Dictionary<string, int> { [a] = 1, [b] = 1 };
                this.ToVersions = new Dictionary<string, int> { [a] = 2, [b] = 2 };
            }

            private string A { get; }
            public IReadOnlyDictionary<string, int> FromVersions { get; }
            public IReadOnlyDictionary<string, int> ToVersions { get; }
            public IReadOnlyList<string> DependsOn => Array.Empty<string>();

            public UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken)
            {
                // Writes only one of the two domains it claimed.
                context.SetPayload(this.A, Encoding.UTF8.GetBytes("only-a"));
                return UniTask.CompletedTask;
            }
        }

        // ---------------------------------------------------------------------
        // Happy path
        // ---------------------------------------------------------------------

        [Test]
        public void SingleChain_MigratesAndKeepsEveryOlderVersion()
        {
            this.Seed("player", 1, "base");
            this._registry.Register(new AppendMigrator("player", 1, 2, "|v2"));
            this._registry.Register(new AppendMigrator("player", 2, 3, "|v3"));

            MigrationOutcome outcome = this.Run(
                new Dictionary<string, int> { ["player"] = 3 }, this.DiscoverVersions("player"));

            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(this.ReadBody("player", 3), Is.EqualTo("base|v2|v3"));

            // Older versions survive - this is what makes rollback a deletion.
            Assert.That(this._store.ListVersions("player"), Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(this.ReadBody("player", 1), Is.EqualTo("base"));
        }

        [Test]
        public void MigratedFile_CarriesAnIncrementedRevision()
        {
            this.Seed("player", 1, "base");
            this._registry.Register(new AppendMigrator("player", 1, 2, "!"));

            this.Run(new Dictionary<string, int> { ["player"] = 2 }, this.DiscoverVersions("player"));

            this._store.ReadHeader("player", 2, out SaveEnvelopeHeader header);
            Assert.That(header.Revision, Is.EqualTo(2), "revision must advance so sync can order edits");
        }

        [Test]
        public void OneWayDependency_ReadsTheAlreadyMigratedDependency()
        {
            this.Seed("player", 1, "P1");
            this.Seed("inventory", 1, "I1");

            this._registry.Register(new AppendMigrator("player", 1, 2, "|migrated"));

            string observed = null;
            this._registry.Register(new DependencyReadingMigrator("inventory", 1, 2, "player",
                value => observed = value));

            MigrationOutcome outcome = this.Run(
                new Dictionary<string, int> { ["player"] = 2, ["inventory"] = 2 },
                this.DiscoverVersions("player", "inventory"));

            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(observed, Is.EqualTo("P1|migrated"),
                "inventory must observe player AFTER player was migrated");
        }

        private sealed class DependencyReadingMigrator : IDataMigrator
        {
            private readonly string _dependency;
            private readonly Action<string> _observe;

            public DependencyReadingMigrator(string domain, int from, int to, string dependency,
                Action<string> observe)
            {
                this.Domain = domain;
                this.FromVersion = from;
                this.ToVersion = to;
                this._dependency = dependency;
                this._observe = observe;
                this.DependsOn = new[] { dependency };
            }

            public string Domain { get; }
            public int FromVersion { get; }
            public int ToVersion { get; }
            public IReadOnlyList<string> DependsOn { get; }

            public UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken)
            {
                this._observe(Encoding.UTF8.GetString(context.GetDependencyPayload(this._dependency)));
                context.SetPayload(this.Domain, context.GetPayload(this.Domain).ToArray());
                return UniTask.CompletedTask;
            }
        }

        [Test]
        public void UndeclaredDependencyRead_Throws()
        {
            this.Seed("a", 1, "A");
            this.Seed("secret", 1, "S");

            // Declares no dependency, yet reads one.
            this._registry.Register(new DependencyReadingMigratorWithoutDeclaration("a", 1, 2, "secret"));

            MigrationOutcome outcome = this.Run(
                new Dictionary<string, int> { ["a"] = 2 }, this.DiscoverVersions("a"));

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Failures.AsValueEnumerable().Single(), Does.Contain("DependsOn"),
                "the message must point at the missing declaration");
        }

        private sealed class DependencyReadingMigratorWithoutDeclaration : IDataMigrator
        {
            private readonly string _dependency;

            public DependencyReadingMigratorWithoutDeclaration(string domain, int from, int to, string dependency)
            {
                this.Domain = domain;
                this.FromVersion = from;
                this.ToVersion = to;
                this._dependency = dependency;
            }

            public string Domain { get; }
            public int FromVersion { get; }
            public int ToVersion { get; }
            public IReadOnlyList<string> DependsOn => Array.Empty<string>();

            public UniTask MigrateAsync(IMigrationContext context, CancellationToken cancellationToken)
            {
                context.GetDependencyPayload(this._dependency);
                return UniTask.CompletedTask;
            }
        }

        // ---------------------------------------------------------------------
        // Mutually dependent domains
        // ---------------------------------------------------------------------

        [Test]
        public void CoupledDomains_AreRewrittenTogetherFromAConsistentSnapshot()
        {
            this.Seed("a", 1, "A1");
            this.Seed("b", 1, "B1");
            this._registry.Register(new CoupledMigrator("a", "b", 1, 2));

            MigrationOutcome outcome = this.Run(
                new Dictionary<string, int> { ["a"] = 2, ["b"] = 2 },
                this.DiscoverVersions("a", "b"));

            Assert.That(outcome.Succeeded, Is.True);

            // Each side saw the other's PRE-migration value - the defining property of a group step.
            Assert.That(this.ReadBody("a", 2), Is.EqualTo("A1+from(B1)"));
            Assert.That(this.ReadBody("b", 2), Is.EqualTo("B1+from(A1)"));
        }

        [Test]
        public void GroupMigratorThatSkipsAMember_FailsAndRollsBackBoth()
        {
            this.Seed("a", 1, "A1");
            this.Seed("b", 1, "B1");
            this._registry.Register(new IncompleteMigrator("a", "b"));

            MigrationOutcome outcome = this.Run(
                new Dictionary<string, int> { ["a"] = 2, ["b"] = 2 },
                this.DiscoverVersions("a", "b"));

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(this._store.ListVersions("a"), Is.EqualTo(new[] { 1 }),
                "a half-written group must leave nothing behind");
            Assert.That(this._store.ListVersions("b"), Is.EqualTo(new[] { 1 }));
        }

        // ---------------------------------------------------------------------
        // Failure and rollback
        // ---------------------------------------------------------------------

        [Test]
        public void FailingMigrator_RollsBackAndLeavesTheOriginalPlayable()
        {
            this.Seed("player", 1, "original");
            this._registry.Register(new AppendMigrator("player", 1, 2, "|v2"));
            this._registry.Register(new ThrowingMigrator("player", 2, 3));

            MigrationOutcome outcome = this.Run(
                new Dictionary<string, int> { ["player"] = 3 }, this.DiscoverVersions("player"));

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Failures.AsValueEnumerable().Single(), Does.Contain("blew up"));

            // Everything this run wrote is gone, including the successful intermediate step.
            Assert.That(this._store.ListVersions("player"), Is.EqualTo(new[] { 1 }));
            Assert.That(this.ReadBody("player", 1), Is.EqualTo("original"),
                "the player must still be able to play on their pre-migration save");
        }

        [Test]
        public void FailureInOneUnit_DoesNotAffectAnUnrelatedUnit()
        {
            this.Seed("broken", 1, "B");
            this.Seed("healthy", 1, "H");
            this._registry.Register(new ThrowingMigrator("broken", 1, 2));
            this._registry.Register(new AppendMigrator("healthy", 1, 2, "|ok"));

            MigrationOutcome outcome = this.Run(
                new Dictionary<string, int> { ["broken"] = 2, ["healthy"] = 2 },
                this.DiscoverVersions("broken", "healthy"));

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.MigratedDomains, Is.EqualTo(new[] { "healthy" }));
            Assert.That(this.ReadBody("healthy", 2), Is.EqualTo("H|ok"));
            Assert.That(this._store.ListVersions("broken"), Is.EqualTo(new[] { 1 }));
        }

        /// <summary>
        /// Simulates a kill mid-migration: a partial v2 exists but the run never finished. Because
        /// state is derived from the files present, replanning simply redoes the work.
        /// </summary>
        [Test]
        public void InterruptedRun_IsResumedByReplanningFromDisk()
        {
            this.Seed("player", 1, "base");
            this._registry.Register(new AppendMigrator("player", 1, 2, "|v2"));
            this._registry.Register(new AppendMigrator("player", 2, 3, "|v3"));

            // First attempt dies after step one.
            this._store.Write("player", 2, Encoding.UTF8.GetBytes("base|v2"), 2,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Guid.Empty);

            MigrationOutcome outcome = this.Run(
                new Dictionary<string, int> { ["player"] = 3 }, this.DiscoverVersions("player"));

            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(this.ReadBody("player", 3), Is.EqualTo("base|v2|v3"),
                "the completed step must not be run twice");
        }

        [Test]
        public void RunningTwice_IsANoOpTheSecondTime()
        {
            this.Seed("player", 1, "base");
            this._registry.Register(new AppendMigrator("player", 1, 2, "|v2"));

            Dictionary<string, int> targets = new() { ["player"] = 2 };

            this.Run(targets, this.DiscoverVersions("player"));
            MigrationOutcome second = this.Run(targets, this.DiscoverVersions("player"));

            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.MigratedDomains, Is.Empty);
            Assert.That(this.ReadBody("player", 2), Is.EqualTo("base|v2"),
                "a second run must not append twice");
        }

        [Test]
        public void MigratorRunsExactlyOncePerStep()
        {
            int runs = 0;
            this.Seed("player", 1, "base");
            this._registry.Register(new AppendMigrator("player", 1, 2, "!", onRun: () => runs++));

            this.Run(new Dictionary<string, int> { ["player"] = 2 }, this.DiscoverVersions("player"));

            Assert.That(runs, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------------
        // Registry guards
        // ---------------------------------------------------------------------

        [Test]
        public void DuplicateStepId_IsRejected()
        {
            AppendMigrator migrator = new("player", 1, 2, "x");
            this._registry.Register(migrator);

            Assert.That(() => this._registry.Register(migrator), Throws.ArgumentException);
        }

        [Test]
        public void SameMigratorTypeOverDifferentVersions_IsAllowed()
        {
            this._registry.Register(new AppendMigrator("player", 1, 2, "a"));

            Assert.That(() => this._registry.Register(new AppendMigrator("player", 2, 3, "b")),
                Throws.Nothing);
        }
    }
}
