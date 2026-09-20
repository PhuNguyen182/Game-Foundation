using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.Tests
{
    [TestFixture]
    public sealed class SaveEnvelopeStoreTests
    {
        private const string Domain = "rise_progression";

        private string _directory;
        private SaveEnvelopeStore _store;

        private static readonly Guid Epoch = new("11111111222233334444555566667777");

        [SetUp]
        public void SetUp()
        {
            this._directory = Path.Combine(Path.GetTempPath(), "dataflow-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this._directory);
            this._store = new SaveEnvelopeStore(this._directory);
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

        private void Write(string domain, int version, string body, long revision = 1) =>
            this._store.Write(domain, version, Encoding.UTF8.GetBytes(body), revision,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Epoch);

        // ---------------------------------------------------------------------
        // Round trip
        // ---------------------------------------------------------------------

        [Test]
        public void WriteThenRead_RoundTrips()
        {
            this.Write(Domain, 3, "hello", revision: 7);

            EnvelopeReadStatus status = this._store.Read(Domain, 3, out SaveEnvelopeHeader header,
                out byte[] payload);

            Assert.That(status, Is.EqualTo(EnvelopeReadStatus.Success));
            Assert.That(header.SchemaVersion, Is.EqualTo(3));
            Assert.That(header.Revision, Is.EqualTo(7));
            Assert.That(Encoding.UTF8.GetString(payload), Is.EqualTo("hello"));
        }

        [Test]
        public void Read_MissingDomain_IsNotFound()
        {
            Assert.That(this._store.Read("absent", 1, out _, out _),
                Is.EqualTo(EnvelopeReadStatus.NotFound));
        }

        [Test]
        public void ReadHeader_DoesNotNeedThePayload()
        {
            this.Write(Domain, 2, "body");

            Assert.That(this._store.ReadHeader(Domain, 2, out SaveEnvelopeHeader header),
                Is.EqualTo(EnvelopeReadStatus.Success));
            Assert.That(header.SchemaVersion, Is.EqualTo(2));
        }

        [Test]
        public void FileNameFollowsTheDocumentedConvention()
        {
            this.Write(Domain, 3, "x");

            Assert.That(File.Exists(Path.Combine(this._directory, "rise_progression_v3.sav")), Is.True);
        }

        // ---------------------------------------------------------------------
        // Versions coexist - the property migration rollback depends on
        // ---------------------------------------------------------------------

        [Test]
        public void WritingANewVersion_LeavesOlderVersionsUntouched()
        {
            this.Write(Domain, 1, "v1-content");
            this.Write(Domain, 2, "v2-content");
            this.Write(Domain, 3, "v3-content");

            Assert.That(this._store.ListVersions(Domain), Is.EqualTo(new[] { 3, 2, 1 }));

            // This is what makes migration rollback a deletion rather than a restore.
            this._store.Read(Domain, 1, out _, out byte[] v1);
            Assert.That(Encoding.UTF8.GetString(v1), Is.EqualTo("v1-content"));
        }

        [Test]
        public void ListVersions_IsDescending()
        {
            foreach (int v in new[] { 2, 10, 1, 7 })
                this.Write(Domain, v, "x");

            Assert.That(this._store.ListVersions(Domain), Is.EqualTo(new[] { 10, 7, 2, 1 }));
        }

        [Test]
        public void ListVersions_EmptyForUnknownDomain()
        {
            Assert.That(this._store.ListVersions("nothing_here"), Is.Empty);
        }

        [Test]
        public void ListVersions_IgnoresSidecarFiles()
        {
            this.Write(Domain, 1, "a");
            this.Write(Domain, 1, "b"); // produces a .bak

            Assert.That(File.Exists(Path.Combine(this._directory, "rise_progression_v1.sav.bak")), Is.True);
            Assert.That(this._store.ListVersions(Domain), Is.EqualTo(new[] { 1 }),
                "a .bak must not be mistaken for a version");
        }

        [Test]
        public void ListVersions_DoesNotLeakAcrossDomainsWithASharedPrefix()
        {
            this.Write("rise", 1, "a");
            this.Write("rise_progression", 2, "b");

            Assert.That(this._store.ListVersions("rise"), Is.EqualTo(new[] { 1 }));
            Assert.That(this._store.ListVersions("rise_progression"), Is.EqualTo(new[] { 2 }));
        }

        /// <summary>
        /// The old runtime scanned downward from the build's current version, so a save written by a
        /// newer build was invisible and got treated as "no save". Enumeration must see it.
        /// </summary>
        [Test]
        public void ListVersions_SeesVersionsAboveWhatThisBuildSupports()
        {
            this.Write(Domain, 9, "from-a-newer-build");

            Assert.That(this._store.GetLatestVersion(Domain), Is.EqualTo(9));
        }

        [Test]
        public void GetLatestVersion_NullWhenNoSaveExists()
        {
            Assert.That(this._store.GetLatestVersion(Domain), Is.Null);
        }

        // ---------------------------------------------------------------------
        // File name parsing
        // ---------------------------------------------------------------------

        [Test]
        public void TryParseFileName_HandlesUnderscoresInTheDomainId()
        {
            Assert.That(SaveEnvelopeStore.TryParseFileName("rise_progression_v12.sav",
                out string domain, out int version), Is.True);
            Assert.That(domain, Is.EqualTo("rise_progression"));
            Assert.That(version, Is.EqualTo(12));
        }

        [TestCase("no-version.sav")]
        [TestCase("domain_v.sav")]
        [TestCase("domain_vX.sav")]
        [TestCase("domain_v0.sav")]
        [TestCase("domain_v-1.sav")]
        [TestCase("domain_v1.sav.bak")]
        [TestCase("domain_v1.sav.tmp")]
        [TestCase("_v1.sav")]
        [TestCase("")]
        public void TryParseFileName_RejectsMalformedNames(string fileName)
        {
            Assert.That(SaveEnvelopeStore.TryParseFileName(fileName, out _, out _), Is.False);
        }

        // ---------------------------------------------------------------------
        // Domain id validation
        // ---------------------------------------------------------------------

        [TestCase("../escape")]
        [TestCase("with/slash")]
        [TestCase("with\\backslash")]
        [TestCase("wild*card")]
        [TestCase("question?mark")]
        [TestCase("")]
        public void GetPath_RejectsUnsafeDomainIds(string domainId)
        {
            Assert.That(() => this._store.GetPath(domainId, 1), Throws.ArgumentException);
        }

        [Test]
        public void GetPath_RejectsVersionBelowOne()
        {
            Assert.That(() => this._store.GetPath(Domain, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        // ---------------------------------------------------------------------
        // Corruption handling
        // ---------------------------------------------------------------------

        [Test]
        public void CorruptPayload_FallsBackToBackup()
        {
            this.Write(Domain, 1, "good-old");
            this.Write(Domain, 1, "good-new"); // demotes "good-old" to .bak

            string path = Path.Combine(this._directory, "rise_progression_v1.sav");
            byte[] raw = File.ReadAllBytes(path);
            raw[^1] ^= 0xFF;
            File.WriteAllBytes(path, raw);

            EnvelopeReadStatus status = this._store.Read(Domain, 1, out _, out byte[] payload);

            Assert.That(status, Is.EqualTo(EnvelopeReadStatus.Success));
            Assert.That(Encoding.UTF8.GetString(payload), Is.EqualTo("good-old"),
                "a corrupt save must cost the last write, not the whole file");
        }

        [Test]
        public void CorruptPayloadWithNoBackup_ReportsFailureWithoutDeletingAnything()
        {
            this.Write(Domain, 1, "only-copy");

            string path = Path.Combine(this._directory, "rise_progression_v1.sav");
            byte[] raw = File.ReadAllBytes(path);
            raw[^1] ^= 0xFF;
            File.WriteAllBytes(path, raw);

            Assert.That(this._store.Read(Domain, 1, out _, out _),
                Is.Not.EqualTo(EnvelopeReadStatus.Success));
            Assert.That(File.Exists(path), Is.True, "the caller decides whether to quarantine");
        }

        [Test]
        public void InterruptedWrite_IsRecoveredOnNextRead()
        {
            this.Write(Domain, 1, "committed");

            // Simulate a kill after the target was moved aside but before the temp was promoted.
            string target = Path.Combine(this._directory, "rise_progression_v1.sav");
            File.Move(target, target + AtomicFileStore.BackupSuffix);
            this.Write(Domain, 2, "unrelated");
            File.Copy(Path.Combine(this._directory, "rise_progression_v2.sav"),
                target + AtomicFileStore.TempSuffix);

            EnvelopeReadStatus status = this._store.Read(Domain, 1, out _, out byte[] payload);

            Assert.That(status, Is.EqualTo(EnvelopeReadStatus.Success));
            Assert.That(Encoding.UTF8.GetString(payload), Is.EqualTo("unrelated"),
                "the newer completed content wins over the demoted backup");
        }

        // ---------------------------------------------------------------------
        // Deleting and pruning
        // ---------------------------------------------------------------------

        [Test]
        public void Delete_RemovesOneVersionOnly()
        {
            this.Write(Domain, 1, "a");
            this.Write(Domain, 2, "b");

            this._store.Delete(Domain, 1);

            Assert.That(this._store.ListVersions(Domain), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void DeleteAll_RemovesEveryVersionAndLeavesNothingRecoverable()
        {
            for (int v = 1; v <= 4; v++)
            {
                this.Write(Domain, v, "a");
                this.Write(Domain, v, "b"); // create a .bak for each
            }

            this._store.DeleteAll(Domain);

            Assert.That(this._store.ListVersions(Domain), Is.Empty);
            Assert.That(Directory.GetFiles(this._directory, "rise_progression*"), Is.Empty,
                "stray sidecars would resurrect data the player asked to delete");
        }

        [Test]
        public void Prune_KeepsLatestPlusThreePrevious()
        {
            for (int v = 1; v <= 9; v++)
                this.Write(Domain, v, "x");

            this._store.Prune(Domain);

            Assert.That(this._store.ListVersions(Domain), Is.EqualTo(new[] { 9, 8, 7, 6 }));
        }

        [Test]
        public void Prune_IsANoOpBelowTheLimit()
        {
            this.Write(Domain, 1, "x");
            this.Write(Domain, 2, "x");

            Assert.That(this._store.Prune(Domain), Is.Zero);
            Assert.That(this._store.ListVersions(Domain), Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void Prune_RejectsKeepingNothing()
        {
            Assert.That(() => this._store.Prune(Domain, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ListDomains_ReturnsEachDomainOnce()
        {
            this.Write("alpha", 1, "x");
            this.Write("alpha", 2, "x");
            this.Write("beta", 1, "x");

            Assert.That(this._store.ListDomains(), Is.EqualTo(new List<string> { "alpha", "beta" }));
        }

        [Test]
        public void ListDomains_EmptyWhenDirectoryDoesNotExist()
        {
            SaveEnvelopeStore missing = new(Path.Combine(this._directory, "does-not-exist"));

            Assert.That(missing.ListDomains(), Is.Empty);
            Assert.That(missing.ListVersions("anything"), Is.Empty);
        }
    }
}
