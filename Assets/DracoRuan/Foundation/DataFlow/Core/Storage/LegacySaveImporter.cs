using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DracoRuan.Foundation.DataFlow.Core.Envelope;

namespace DracoRuan.Foundation.DataFlow.Core.Storage
{
    /// <summary>Outcome of importing one domain's legacy files.</summary>
    public readonly struct LegacyImportResult
    {
        public LegacyImportResult(string domainId, int importedVersions, int highestVersion, string error = null)
        {
            this.DomainId = domainId;
            this.ImportedVersions = importedVersions;
            this.HighestVersion = highestVersion;
            this.Error = error;
        }

        public string DomainId { get; }
        public int ImportedVersions { get; }
        public int HighestVersion { get; }
        public string Error { get; }

        public bool Succeeded => this.Error == null;
        public bool DidWork => this.ImportedVersions > 0;
    }

    /// <summary>
    /// Converts saves written in the old layout — <c>{TypeName}_v{N}.data</c>, raw payload bytes with
    /// no header — into the current <c>{domainId}_v{N}.sav</c> envelope format.
    ///
    /// <para>Runs once, at boot, before migration planning.</para>
    /// </summary>
    /// <remarks>
    /// <para><b>Deliberately not an <see cref="Migration.IDataMigrator"/>.</b> This changes the file
    /// <i>container</i>, not the payload's schema. It has no from/to version and must never appear
    /// in a migration chain, or a schema upgrade and a container upgrade would become entangled.</para>
    ///
    /// <para><b>Idempotence comes from the filesystem, not a flag.</b> If a <c>.sav</c> exists for a
    /// version, that version is already imported and the legacy file is ignored. A "hasImported"
    /// boolean in PlayerPrefs would disagree with reality after a cloud restore, a prefs wipe, or a
    /// sideloaded data directory — and disagreeing means either importing twice or never.</para>
    ///
    /// <para><b>Nothing is deleted.</b> Legacy files are moved into a <c>.legacy</c> subdirectory
    /// only after the new file has been written <i>and</i> read back with its checksum verified.
    /// Keep that directory for at least one shipped release: deleting in the same release that
    /// introduces the import makes an import bug unrecoverable for anyone who booted once.</para>
    /// </remarks>
    public sealed class LegacySaveImporter
    {
        /// <summary>Extension of the old format.</summary>
        public const string LegacyExtension = ".data";

        /// <summary>Subdirectory that imported originals are moved into.</summary>
        public const string LegacyArchiveDirectory = ".legacy";

        private readonly SaveEnvelopeStore _store;
        private readonly string _legacyDirectory;

        public LegacySaveImporter(SaveEnvelopeStore store, string legacyDirectory = null)
        {
            this._store = store ?? throw new ArgumentNullException(nameof(store));
            this._legacyDirectory = legacyDirectory ?? store.RootDirectory;
        }

        /// <summary>
        /// Imports every legacy file for a domain.
        /// </summary>
        /// <param name="domainId">Domain id to write under.</param>
        /// <param name="legacyTypeName">
        /// Name the old files used, which was the data class's type name. Usually differs from
        /// <paramref name="domainId"/>, since domain ids are now explicit constants.
        /// </param>
        public LegacyImportResult Import(string domainId, string legacyTypeName)
        {
            DomainId.Validate(domainId);

            if (string.IsNullOrEmpty(legacyTypeName))
                throw new ArgumentException("Legacy type name must not be empty.", nameof(legacyTypeName));

            if (!Directory.Exists(this._legacyDirectory))
                return new LegacyImportResult(domainId, 0, 0);

            List<(int Version, string Path)> legacyFiles;
            try
            {
                legacyFiles = this.FindLegacyFiles(legacyTypeName);
            }
            catch (Exception exception)
            {
                return new LegacyImportResult(domainId, 0, 0, exception.Message);
            }

            if (legacyFiles.Count == 0)
                return new LegacyImportResult(domainId, 0, 0);

            int imported = 0;
            int highest = 0;

            foreach ((int version, string path) in legacyFiles)
            {
                highest = Math.Max(highest, version);

                // Already converted. Never look at the legacy file again.
                if (this._store.ReadHeader(domainId, version, out _) == EnvelopeReadStatus.Success)
                {
                    this.Archive(path);
                    continue;
                }

                try
                {
                    this.ImportOne(domainId, version, path);
                    imported++;
                }
                catch (Exception exception)
                {
                    // Leave the legacy file exactly where it is so the next boot can retry.
                    return new LegacyImportResult(domainId, imported, highest,
                        $"Failed to import '{Path.GetFileName(path)}': {exception.Message}");
                }
            }

            return new LegacyImportResult(domainId, imported, highest);
        }

        private void ImportOne(string domainId, int version, string legacyPath)
        {
            byte[] payload = File.ReadAllBytes(legacyPath);

            this._store.Write(
                domainId,
                version,
                payload,
                revision: 0,
                lastModifiedUtcMs: ToUnixMilliseconds(File.GetLastWriteTimeUtc(legacyPath)),
                deviceEpochId: Guid.Empty,
                flags: SaveEnvelopeFlags.LegacyImported);

            // Read back and verify before touching the original. Writing then archiving without a
            // check would move the only good copy aside on the strength of an unverified write.
            EnvelopeReadStatus status = this._store.Read(domainId, version, out _, out byte[] roundTripped);
            if (status != EnvelopeReadStatus.Success)
                throw new IOException($"Imported file failed verification ({status}).");

            if (!SequenceEquals(payload, roundTripped))
                throw new IOException("Imported payload does not match the legacy file.");

            this.Archive(legacyPath);
        }

        /// <summary>
        /// Enumerates legacy files rather than probing candidate names, so a save written by a
        /// build newer than this one is still seen instead of being mistaken for "no save".
        /// </summary>
        private List<(int Version, string Path)> FindLegacyFiles(string legacyTypeName)
        {
            List<(int Version, string Path)> found = new();

            string[] candidates = Directory.GetFiles(
                this._legacyDirectory, legacyTypeName + "_v*" + LegacyExtension);

            foreach (string path in candidates)
            {
                if (TryParseLegacyFileName(Path.GetFileName(path), out string typeName, out int version) &&
                    string.Equals(typeName, legacyTypeName, StringComparison.Ordinal))
                {
                    found.Add((version, path));
                }
            }

            found.Sort((a, b) => a.Version.CompareTo(b.Version));
            return found;
        }

        /// <summary>Splits <c>{TypeName}_v{N}.data</c>, parsing from the right.</summary>
        public static bool TryParseLegacyFileName(string fileName, out string typeName, out int version)
        {
            typeName = null;
            version = 0;

            if (string.IsNullOrEmpty(fileName) ||
                !fileName.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase))
                return false;

            string stem = fileName.Substring(0, fileName.Length - LegacyExtension.Length);

            int separator = stem.LastIndexOf("_v", StringComparison.Ordinal);
            if (separator <= 0)
                return false;

            string versionText = stem.Substring(separator + 2);
            if (versionText.Length == 0 ||
                !int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out version) ||
                version < 1)
                return false;

            typeName = stem.Substring(0, separator);
            return true;
        }

        /// <summary>Moves an imported original aside. Never deletes it.</summary>
        private void Archive(string legacyPath)
        {
            if (!File.Exists(legacyPath))
                return;

            string archiveDirectory = Path.Combine(this._legacyDirectory, LegacyArchiveDirectory);
            if (!Directory.Exists(archiveDirectory))
                Directory.CreateDirectory(archiveDirectory);

            string destination = Path.Combine(archiveDirectory, Path.GetFileName(legacyPath));

            try
            {
                if (File.Exists(destination))
                    File.Delete(destination);

                File.Move(legacyPath, destination);
            }
            catch (Exception)
            {
                // Archiving is housekeeping. A failure here must not fail an import that already
                // wrote and verified the new file; the next boot will simply skip and retry.
            }
        }

        private static long ToUnixMilliseconds(DateTime utc) =>
            new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        private static bool SequenceEquals(IReadOnlyList<byte> left, IReadOnlyList<byte> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }
    }
}
