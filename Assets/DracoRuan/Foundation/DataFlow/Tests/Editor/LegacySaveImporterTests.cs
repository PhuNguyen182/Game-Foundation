using System;
using System.IO;
using System.Text;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using NUnit.Framework;

namespace DracoRuan.Foundation.DataFlow.Tests
{
    [TestFixture]
    public sealed class LegacySaveImporterTests
    {
        private const string Domain = "rise_progression";
        private const string LegacyType = "RiseProgressData";

        private string _directory;
        private SaveEnvelopeStore _store;
        private LegacySaveImporter _importer;

        [SetUp]
        public void SetUp()
        {
            this._directory = Path.Combine(Path.GetTempPath(), "dataflow-legacy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this._directory);
            this._store = new SaveEnvelopeStore(this._directory);
            this._importer = new LegacySaveImporter(this._store);
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

        private void SeedLegacy(int version, string body) =>
            File.WriteAllBytes(
                Path.Combine(this._directory, $"{LegacyType}_v{version}.data"),
                Encoding.UTF8.GetBytes(body));

        /// <summary>Where an imported original ends up: under the domain that imported it.</summary>
        private string ArchivePath(int version) => this.ArchivePath(Domain, version);

        private string ArchivePath(string domainId, int version) =>
            Path.Combine(this._store.GetDomainDirectory(domainId),
                LegacySaveImporter.LegacyArchiveDirectory, $"{LegacyType}_v{version}.data");

        private string ReadBody(int version)
        {
            EnvelopeReadStatus status = this._store.Read(Domain, version, out _, out byte[] payload);
            return status == EnvelopeReadStatus.Success ? Encoding.UTF8.GetString(payload) : null;
        }

        // ---------------------------------------------------------------------

        [Test]
        public void NothingToImport_IsSuccessWithNoWork()
        {
            LegacyImportResult result = this._importer.Import(Domain, LegacyType);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.DidWork, Is.False);
        }

        [Test]
        public void SingleLegacyFile_BecomesAnEnvelope()
        {
            this.SeedLegacy(1, "legacy-payload");

            LegacyImportResult result = this._importer.Import(Domain, LegacyType);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ImportedVersions, Is.EqualTo(1));
            Assert.That(this.ReadBody(1), Is.EqualTo("legacy-payload"),
                "the payload must survive the container change byte for byte");
        }

        [Test]
        public void ImportedFile_IsFlaggedAsLegacyWithRevisionZero()
        {
            this.SeedLegacy(1, "x");
            this._importer.Import(Domain, LegacyType);

            this._store.ReadHeader(Domain, 1, out SaveEnvelopeHeader header);

            Assert.That(header.HasFlag(SaveEnvelopeFlags.LegacyImported), Is.True);
            Assert.That(header.Revision, Is.Zero,
                "revision 0 marks a payload this build has never re-serialized");
        }

        [Test]
        public void EveryLegacyVersion_IsImported()
        {
            this.SeedLegacy(1, "one");
            this.SeedLegacy(2, "two");
            this.SeedLegacy(3, "three");

            LegacyImportResult result = this._importer.Import(Domain, LegacyType);

            Assert.That(result.ImportedVersions, Is.EqualTo(3));
            Assert.That(result.HighestVersion, Is.EqualTo(3));
            Assert.That(this._store.ListVersions(Domain), Is.EqualTo(new[] { 3, 2, 1 }));
        }

        [Test]
        public void OriginalsAreMovedAsideNotDeleted()
        {
            this.SeedLegacy(1, "keep-me");

            this._importer.Import(Domain, LegacyType);

            Assert.That(File.Exists(Path.Combine(this._directory, $"{LegacyType}_v1.data")), Is.False);
            Assert.That(File.Exists(this.ArchivePath(1)), Is.True,
                "an import bug must stay recoverable for at least one release");
            Assert.That(File.ReadAllText(this.ArchivePath(1)), Is.EqualTo("keep-me"));
        }

        [Test]
        public void RunningTwice_DoesNotImportAgain()
        {
            this.SeedLegacy(1, "original");

            this._importer.Import(Domain, LegacyType);
            LegacyImportResult second = this._importer.Import(Domain, LegacyType);

            Assert.That(second.DidWork, Is.False);
            Assert.That(this.ReadBody(1), Is.EqualTo("original"));
        }

        /// <summary>
        /// Idempotence must come from the filesystem. A legacy file reappearing next to an existing
        /// .sav - a cloud restore, a sideloaded data directory - must not overwrite newer data.
        /// </summary>
        [Test]
        public void ExistingEnvelope_IsNeverOverwrittenByALegacyFile()
        {
            this._store.Write(Domain, 1, Encoding.UTF8.GetBytes("current-data"), 5,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Guid.NewGuid());

            this.SeedLegacy(1, "stale-legacy-data");

            LegacyImportResult result = this._importer.Import(Domain, LegacyType);

            Assert.That(result.DidWork, Is.False);
            Assert.That(this.ReadBody(1), Is.EqualTo("current-data"));
            Assert.That(File.Exists(this.ArchivePath(1)), Is.True, "the stale file is archived, not applied");
        }

        [Test]
        public void PartialImport_LeavesUnimportedOriginalsInPlaceForTheNextBoot()
        {
            this.SeedLegacy(1, "one");
            this.SeedLegacy(2, "two");

            this._importer.Import(Domain, LegacyType);

            // Simulate the process dying before v2 was handled by restoring only that original.
            File.Move(this.ArchivePath(2), Path.Combine(this._directory, $"{LegacyType}_v2.data"));
            this._store.Delete(Domain, 2);

            LegacyImportResult retry = this._importer.Import(Domain, LegacyType);

            Assert.That(retry.Succeeded, Is.True);
            Assert.That(this.ReadBody(2), Is.EqualTo("two"));
        }

        /// <summary>
        /// The old runtime scanned downward from the build's current version, so a save from a newer
        /// build was invisible. The importer enumerates instead, so it is seen and preserved.
        /// </summary>
        [Test]
        public void VersionsAboveTheCurrentBuild_AreStillImported()
        {
            this.SeedLegacy(9, "from-a-newer-build");

            LegacyImportResult result = this._importer.Import(Domain, LegacyType);

            Assert.That(result.HighestVersion, Is.EqualTo(9));
            Assert.That(this.ReadBody(9), Is.EqualTo("from-a-newer-build"));
        }

        [Test]
        public void DomainIdMayDifferFromTheLegacyTypeName()
        {
            this.SeedLegacy(1, "payload");

            this._importer.Import("a_completely_different_id", LegacyType);

            // Read from the flat root, write under the domain id - not under the legacy type name.
            Assert.That(
                File.Exists(Path.Combine(this._directory, "a_completely_different_id",
                    "a_completely_different_id_v1.sav")),
                Is.True);
        }

        // ---------------------------------------------------------------------
        // Where the archive lives
        // ---------------------------------------------------------------------

        [Test]
        public void TheArchiveLivesUnderTheImportingDomain()
        {
            this.SeedLegacy(1, "payload");

            this._importer.Import(Domain, LegacyType);

            Assert.That(File.Exists(this.ArchivePath(1)), Is.True);
            Assert.That(Directory.Exists(
                    Path.Combine(this._directory, LegacySaveImporter.LegacyArchiveDirectory)),
                Is.False,
                "a shared archive at the root is the one place every domain stays piled together");
        }

        /// <summary>
        /// Two domains may legitimately import the same legacy type name. Under a shared archive
        /// the second would overwrite the first, destroying an original that exists nowhere else.
        /// </summary>
        [Test]
        public void TwoDomainsImportingTheSameTypeNameKeepBothOriginals()
        {
            this.SeedLegacy(1, "first");
            this._importer.Import("domain_one", LegacyType);

            this.SeedLegacy(1, "second");
            this._importer.Import("domain_two", LegacyType);

            Assert.That(File.Exists(this.ArchivePath("domain_one", 1)), Is.True);
            Assert.That(File.Exists(this.ArchivePath("domain_two", 1)), Is.True);

            Assert.That(File.ReadAllText(this.ArchivePath("domain_one", 1)), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(this.ArchivePath("domain_two", 1)), Is.EqualTo("second"),
                "one domain's original must never overwrite another's");
        }

        [Test]
        public void ArchivingCreatesNoStrayFilesInTheRoot()
        {
            this.SeedLegacy(1, "payload");
            this.SeedLegacy(2, "payload");

            this._importer.Import(Domain, LegacyType);

            Assert.That(Directory.GetFiles(this._directory), Is.Empty,
                "every imported original belongs to some domain");
        }

        [Test]
        public void UnrelatedLegacyFiles_AreLeftAlone()
        {
            this.SeedLegacy(1, "mine");
            File.WriteAllText(Path.Combine(this._directory, "SomeOtherType_v1.data"), "not mine");

            this._importer.Import(Domain, LegacyType);

            Assert.That(File.Exists(Path.Combine(this._directory, "SomeOtherType_v1.data")), Is.True);
        }

        [Test]
        public void EmptyLegacyFile_ImportsAsAnEmptyPayload()
        {
            File.WriteAllBytes(Path.Combine(this._directory, $"{LegacyType}_v1.data"), Array.Empty<byte>());

            LegacyImportResult result = this._importer.Import(Domain, LegacyType);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(this.ReadBody(1), Is.Empty);
        }

        [TestCase("NoVersion.data")]
        [TestCase("Type_v.data")]
        [TestCase("Type_v0.data")]
        [TestCase("Type_vX.data")]
        [TestCase("Type_v1.sav")]
        public void MalformedLegacyNames_AreNotParsed(string fileName)
        {
            Assert.That(LegacySaveImporter.TryParseLegacyFileName(fileName, out _, out _), Is.False);
        }

        [Test]
        public void LegacyNameWithUnderscores_ParsesFromTheRight()
        {
            Assert.That(LegacySaveImporter.TryParseLegacyFileName("My_Nested_Type_v12.data",
                out string typeName, out int version), Is.True);
            Assert.That(typeName, Is.EqualTo("My_Nested_Type"));
            Assert.That(version, Is.EqualTo(12));
        }
    }
}
