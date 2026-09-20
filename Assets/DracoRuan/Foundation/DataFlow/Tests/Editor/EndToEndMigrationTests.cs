using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.Core.Migration;
using DracoRuan.Foundation.DataFlow.Core.Serialization;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.Tests
{
    /// <summary>
    /// The full story a shipped build actually performs: legacy files on disk, imported, then
    /// migrated across schema versions using typed per-version classes — including two domains that
    /// depend on each other.
    ///
    /// <para>
    /// Also serves as the worked example of how to write a migrator, since the engine had never had
    /// a single concrete one before this rework.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class EndToEndMigrationTests
    {
        private string _directory;
        private SaveEnvelopeStore _store;
        private MigrationRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            this._directory = Path.Combine(Path.GetTempPath(), "dataflow-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this._directory);
            this._store = new SaveEnvelopeStore(this._directory);
            this._registry = new MigrationRegistry();

            PayloadCodec.Default = new JsonTestCodec();
        }

        [TearDown]
        public void TearDown()
        {
            PayloadCodec.Reset();

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

        // -----------------------------------------------------------------
        // Per-version data classes. Frozen once shipped; never edited again.
        // -----------------------------------------------------------------

        private sealed class ProfileV1
        {
            public string Name { get; set; }
            public int Score { get; set; }
        }

        private sealed class ProfileV2
        {
            public string Name { get; set; }
            public int Score { get; set; }

            /// <summary>Added in v2, derived from the v1 score.</summary>
            public int Rank { get; set; }
        }

        private sealed class InventoryV1
        {
            public List<string> Items { get; set; } = new();
        }

        private sealed class InventoryV2
        {
            public List<string> Items { get; set; } = new();
            public string OwnerName { get; set; }
        }

        // -----------------------------------------------------------------
        // Migrators, written the way a game would write them
        // -----------------------------------------------------------------

        private sealed class ProfileV1ToV2 : DataMigrator<ProfileV1, ProfileV2>
        {
            public override string Domain => "profile";
            public override int FromVersion => 1;
            public override int ToVersion => 2;

            protected override ProfileV2 Migrate(ProfileV1 from, IMigrationContext context) =>
                new()
                {
                    Name = from.Name,
                    Score = from.Score,
                    Rank = from.Score / 100
                };
        }

        /// <summary>Reads another domain, and declares it so the planner orders them.</summary>
        private sealed class InventoryV1ToV2 : DataMigrator<InventoryV1, InventoryV2>
        {
            public override string Domain => "inventory";
            public override int FromVersion => 1;
            public override int ToVersion => 2;
            public override IReadOnlyList<string> DependsOn => new[] { "profile" };

            protected override InventoryV2 Migrate(InventoryV1 from, IMigrationContext context)
            {
                // "profile" is guaranteed migrated already, so this reads the v2 shape.
                ProfileV2 profile = new JsonTestCodec()
                    .Deserialize<ProfileV2>(context.GetDependencyPayload("profile"));

                return new InventoryV2
                {
                    Items = from.Items,
                    OwnerName = profile.Name
                };
            }
        }

        /// <summary>
        /// Two domains that derive from each other, so neither can go first. Rewritten together.
        /// </summary>
        private sealed class ProfileInventoryCoupledV2ToV3 : GroupDataMigrator
        {
            public override IReadOnlyDictionary<string, int> FromVersions =>
                new Dictionary<string, int> { ["profile"] = 2, ["inventory"] = 2 };

            public override IReadOnlyDictionary<string, int> ToVersions =>
                new Dictionary<string, int> { ["profile"] = 3, ["inventory"] = 3 };

            protected override void Migrate(IMigrationContext context)
            {
                ProfileV2 profile = this.Read<ProfileV2>(context, "profile");
                InventoryV2 inventory = this.Read<InventoryV2>(context, "inventory");

                // Each side derived from the other's pre-migration state.
                profile.Score += inventory.Items.Count;
                inventory.OwnerName = $"{profile.Name}#{profile.Rank}";

                this.Write(context, "profile", profile);
                this.Write(context, "inventory", inventory);
            }
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private void SeedLegacy(string legacyTypeName, int version, object payload) =>
            File.WriteAllBytes(
                Path.Combine(this._directory, $"{legacyTypeName}_v{version}.data"),
                new JsonTestCodec().Serialize(payload.GetType(), payload));

        private T Read<T>(string domain, int version)
        {
            Assert.That(this._store.Read(domain, version, out _, out byte[] payload),
                Is.EqualTo(EnvelopeReadStatus.Success), $"{domain} v{version} should be readable");

            return new JsonTestCodec().Deserialize<T>(payload);
        }

        private MigrationOutcome RunMigration(Dictionary<string, int> targets)
        {
            Dictionary<string, int> current = targets.Keys
                .ToDictionary(d => d, d => this._store.GetLatestVersion(d) ?? 0);

            MigrationPlan plan = new MigrationPlanner().CreatePlan(targets, current, this._registry.Steps);
            MigrationExecutor executor = new(this._store, this._registry);

            return executor.ExecuteAsync(plan, "player-1").GetAwaiter().GetResult();
        }

        // -----------------------------------------------------------------
        // Tests
        // -----------------------------------------------------------------

        [Test]
        public void LegacyFileIsImportedThenMigratedAcrossVersions()
        {
            this.SeedLegacy("ProfileData", 1, new ProfileV1 { Name = "Mai", Score = 250 });

            new LegacySaveImporter(this._store).Import("profile", "ProfileData");
            this._registry.Register(new ProfileV1ToV2());

            MigrationOutcome outcome = this.RunMigration(new Dictionary<string, int> { ["profile"] = 2 });

            Assert.That(outcome.Succeeded, Is.True);

            ProfileV2 migrated = this.Read<ProfileV2>("profile", 2);
            Assert.That(migrated.Name, Is.EqualTo("Mai"));
            Assert.That(migrated.Score, Is.EqualTo(250));
            Assert.That(migrated.Rank, Is.EqualTo(2), "Rank is derived from the v1 score");

            // The v1 file survives, which is what makes the upgrade reversible.
            Assert.That(this._store.ListVersions("profile"), Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void DependentDomainSeesTheMigratedDependency()
        {
            this._store.Write("profile", 1,
                new JsonTestCodec().Serialize(new ProfileV1 { Name = "Mai", Score = 250 }),
                1, 0, Guid.Empty);
            this._store.Write("inventory", 1,
                new JsonTestCodec().Serialize(new InventoryV1 { Items = { "sword", "shield" } }),
                1, 0, Guid.Empty);

            this._registry.Register(new ProfileV1ToV2());
            this._registry.Register(new InventoryV1ToV2());

            MigrationOutcome outcome = this.RunMigration(
                new Dictionary<string, int> { ["profile"] = 2, ["inventory"] = 2 });

            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(this.Read<InventoryV2>("inventory", 2).OwnerName, Is.EqualTo("Mai"));
        }

        [Test]
        public void MutuallyDependentDomainsMigrateAsOneStep()
        {
            this._store.Write("profile", 2,
                new JsonTestCodec().Serialize(new ProfileV2 { Name = "Mai", Score = 250, Rank = 2 }),
                1, 0, Guid.Empty);
            this._store.Write("inventory", 2,
                new JsonTestCodec().Serialize(
                    new InventoryV2 { Items = { "sword", "shield", "potion" }, OwnerName = "Mai" }),
                1, 0, Guid.Empty);

            this._registry.Register(new ProfileInventoryCoupledV2ToV3());

            MigrationOutcome outcome = this.RunMigration(
                new Dictionary<string, int> { ["profile"] = 3, ["inventory"] = 3 });

            Assert.That(outcome.Succeeded, Is.True);

            // Both sides observed the other's PRE-migration values.
            Assert.That(this.Read<ProfileV2>("profile", 3).Score, Is.EqualTo(253), "250 + 3 items");
            Assert.That(this.Read<InventoryV2>("inventory", 3).OwnerName, Is.EqualTo("Mai#2"));
        }

        [Test]
        public void FullChain_LegacyThenSingleThenCoupled()
        {
            this.SeedLegacy("ProfileData", 1, new ProfileV1 { Name = "Mai", Score = 250 });
            this.SeedLegacy("InventoryData", 1, new InventoryV1 { Items = { "sword", "shield" } });

            LegacySaveImporter importer = new(this._store);
            importer.Import("profile", "ProfileData");
            importer.Import("inventory", "InventoryData");

            this._registry.Register(new ProfileV1ToV2());
            this._registry.Register(new InventoryV1ToV2());
            this._registry.Register(new ProfileInventoryCoupledV2ToV3());

            MigrationOutcome outcome = this.RunMigration(
                new Dictionary<string, int> { ["profile"] = 3, ["inventory"] = 3 });

            Assert.That(outcome.Succeeded, Is.True, string.Join("; ", outcome.Failures));

            Assert.That(this.Read<ProfileV2>("profile", 3).Score, Is.EqualTo(252), "250 + 2 items");
            Assert.That(this.Read<InventoryV2>("inventory", 3).OwnerName, Is.EqualTo("Mai#2"));

            // Every intermediate version is still on disk.
            Assert.That(this._store.ListVersions("profile"), Is.EqualTo(new[] { 3, 2, 1 }));
        }

        [Test]
        public void MigratorReturningNull_FailsInsteadOfWritingAnEmptySave()
        {
            this._store.Write("profile", 1,
                new JsonTestCodec().Serialize(new ProfileV1 { Name = "Mai", Score = 1 }), 1, 0, Guid.Empty);

            this._registry.Register(new NullReturningMigrator());

            MigrationOutcome outcome = this.RunMigration(new Dictionary<string, int> { ["profile"] = 2 });

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(this._store.ListVersions("profile"), Is.EqualTo(new[] { 1 }),
                "the original must survive");
        }

        private sealed class NullReturningMigrator : DataMigrator<ProfileV1, ProfileV2>
        {
            public override string Domain => "profile";
            public override int FromVersion => 1;
            public override int ToVersion => 2;

            protected override ProfileV2 Migrate(ProfileV1 from, IMigrationContext context) => null;
        }

        /// <summary>
        /// JSON codec for tests, so migration logic can be exercised without standing up
        /// MessagePack's AOT resolver chain.
        /// </summary>
        private sealed class JsonTestCodec : IPayloadCodec
        {
            public byte[] Serialize<T>(T value) => this.Serialize(typeof(T), value);

            public T Deserialize<T>(ReadOnlySpan<byte> payload) => (T)this.Deserialize(typeof(T), payload);

            public byte[] Serialize(Type type, object value) =>
                Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(value));

            public object Deserialize(Type type, ReadOnlySpan<byte> payload) =>
                Newtonsoft.Json.JsonConvert.DeserializeObject(
                    Encoding.UTF8.GetString(payload.ToArray()), type);
        }
    }
}
