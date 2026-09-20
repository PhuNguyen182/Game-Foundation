using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DracoRuan.Foundation.DataFlow.Core.Envelope;

namespace DracoRuan.Foundation.DataFlow.Core.Storage
{
    /// <summary>
    /// Reads and writes versioned save files: <c>{domainId}_v{schemaVersion}.sav</c>, each an
    /// envelope written through <see cref="AtomicFileStore"/>.
    ///
    /// <para>
    /// Versions coexist on disk rather than being collapsed into one file. That is what makes
    /// migration cheap to undo — a migration <i>writes a new version file and never touches the old
    /// one</i>, so rolling back is deleting what was just written, and the pre-migration state is
    /// still sitting there intact. It is also what keeps a downgrade safe: a build that supports
    /// schema v3 looks for <c>_v3</c> and simply does not find a v5 file, instead of opening it and
    /// silently dropping the fields it does not understand.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>Retention is bounded by <see cref="Prune"/> — the latest version plus a few previous
    /// ones. Without it the directory grows once per schema bump, forever.</para>
    ///
    /// <para>Has no Unity dependency: the save root is injected, so tests drive it against a
    /// temporary directory and the Editor tool drives it against the real one. Sharing this type
    /// between the game and the tool is deliberate — the previous Editor window reimplemented the
    /// path and serialization rules, so any change to the format silently desynchronized them.</para>
    /// </remarks>
    public sealed class SaveEnvelopeStore
    {
        /// <summary>Extension for save files.</summary>
        public const string FileExtension = ".sav";

        /// <summary>Separator between the domain id and the version number.</summary>
        public const string VersionSeparator = "_v";

        /// <summary>Latest version plus three previous, matching what the Editor tool shows.</summary>
        public const int DefaultVersionsToKeep = 4;

        private readonly string _rootDirectory;
        private readonly AtomicFileStore _fileStore;

        public SaveEnvelopeStore(string rootDirectory)
        {
            if (string.IsNullOrEmpty(rootDirectory))
                throw new ArgumentException("Save root directory must not be empty.", nameof(rootDirectory));

            this._rootDirectory = rootDirectory;

            // Recovery treats a file as usable only when it decodes AND its checksum verifies, so a
            // corrupt target falls back to the backup instead of being handed to the deserializer.
            this._fileStore = new AtomicFileStore(IsEnvelopeReadable);
        }

        /// <summary>Directory holding every save file.</summary>
        public string RootDirectory => this._rootDirectory;

        /// <summary>Absolute path of one domain's file at one schema version.</summary>
        public string GetPath(string domainId, int schemaVersion)
        {
            DomainId.Validate(domainId);

            if (schemaVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion,
                    "Schema versions start at 1.");

            return Path.Combine(this._rootDirectory, BuildFileName(domainId, schemaVersion));
        }

        public static string BuildFileName(string domainId, int schemaVersion) =>
            domainId + VersionSeparator + schemaVersion.ToString(CultureInfo.InvariantCulture) + FileExtension;

        /// <summary>
        /// Every schema version present for a domain, highest first. One directory enumeration
        /// rather than a probe per version.
        /// </summary>
        /// <remarks>
        /// Enumerating is not merely faster than probing candidate filenames — it is the only
        /// correct approach. A probing scan has to guess an upper bound, and any version above that
        /// bound is invisible, which is precisely how a save from a newer build gets mistaken for
        /// "no save at all" and overwritten.
        /// </remarks>
        public IReadOnlyList<int> ListVersions(string domainId)
        {
            DomainId.Validate(domainId);

            List<int> versions = new();
            if (!Directory.Exists(this._rootDirectory))
                return versions;

            string prefix = domainId + VersionSeparator;
            string[] candidates;
            try
            {
                candidates = Directory.GetFiles(this._rootDirectory, prefix + "*" + FileExtension);
            }
            catch (DirectoryNotFoundException)
            {
                return versions;
            }

            foreach (string path in candidates)
            {
                // Re-check explicitly: Windows search patterns have legacy short-name quirks that
                // can match more than the pattern literally describes.
                if (TryParseFileName(Path.GetFileName(path), out string parsedDomain, out int version)
                    && string.Equals(parsedDomain, domainId, StringComparison.Ordinal))
                {
                    versions.Add(version);
                }
            }

            versions.Sort((a, b) => b.CompareTo(a));
            return versions;
        }

        /// <summary>Highest schema version present, or <c>null</c> when the domain has no save.</summary>
        public int? GetLatestVersion(string domainId)
        {
            IReadOnlyList<int> versions = this.ListVersions(domainId);
            return versions.Count == 0 ? null : versions[0];
        }

        /// <summary>
        /// Splits <c>{domainId}_v{N}.sav</c>. Parses from the right because domain ids may
        /// themselves contain underscores (<c>rise_progression_v3.sav</c>).
        /// </summary>
        public static bool TryParseFileName(string fileName, out string domainId, out int schemaVersion)
        {
            domainId = null;
            schemaVersion = 0;

            if (string.IsNullOrEmpty(fileName) ||
                !fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
                return false;

            string stem = fileName.Substring(0, fileName.Length - FileExtension.Length);

            int separator = stem.LastIndexOf(VersionSeparator, StringComparison.Ordinal);
            if (separator <= 0)
                return false;

            string versionText = stem.Substring(separator + VersionSeparator.Length);
            if (versionText.Length == 0 ||
                !int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out schemaVersion) ||
                schemaVersion < 1)
                return false;

            domainId = stem.Substring(0, separator);
            return true;
        }

        /// <summary>
        /// Reads a header without decoding the payload — the cheap boot scan.
        /// </summary>
        public EnvelopeReadStatus ReadHeader(string domainId, int schemaVersion, out SaveEnvelopeHeader header)
        {
            header = default;

            string path = this.GetPath(domainId, schemaVersion);
            this._fileStore.Recover(path);

            if (!File.Exists(path))
                return EnvelopeReadStatus.NotFound;

            byte[] raw;
            try
            {
                raw = File.ReadAllBytes(path);
            }
            catch (Exception)
            {
                return EnvelopeReadStatus.NotFound;
            }

            return SaveEnvelopeCodec.TryReadHeader(raw, out header);
        }

        /// <summary>
        /// Reads and verifies a complete save file, recovering from an interrupted write first.
        /// </summary>
        public EnvelopeReadStatus Read(
            string domainId,
            int schemaVersion,
            out SaveEnvelopeHeader header,
            out byte[] payload)
        {
            header = default;
            payload = null;

            string path = this.GetPath(domainId, schemaVersion);

            byte[] raw = this._fileStore.Read(path, out RecoveryOutcome _);
            if (raw == null)
                return File.Exists(path) ? EnvelopeReadStatus.ChecksumMismatch : EnvelopeReadStatus.NotFound;

            return SaveEnvelopeCodec.TryRead(raw, out header, out payload);
        }

        /// <summary>
        /// Writes a payload as the given schema version, atomically.
        /// </summary>
        /// <remarks>
        /// <paramref name="payload"/> must already be serialized. Serialization belongs to the
        /// caller and must happen on the main thread while the game object is not being mutated —
        /// serializing a live object off-thread produces a torn payload whose checksum is computed
        /// over the torn bytes, so it verifies perfectly and the corruption is invisible.
        /// </remarks>
        public void Write(
            string domainId,
            int schemaVersion,
            ReadOnlySpan<byte> payload,
            long revision,
            long lastModifiedUtcMs,
            Guid deviceEpochId,
            SaveEnvelopeFlags flags = SaveEnvelopeFlags.None)
        {
            string path = this.GetPath(domainId, schemaVersion);

            byte[] raw = SaveEnvelopeCodec.Write(
                schemaVersion, revision, lastModifiedUtcMs, deviceEpochId, flags, payload);

            this._fileStore.Write(path, raw);
        }

        /// <summary>Deletes one version and its sidecars. Returns the number of files removed.</summary>
        public int Delete(string domainId, int schemaVersion) =>
            this._fileStore.Delete(this.GetPath(domainId, schemaVersion));

        /// <summary>Deletes every version of a domain. Returns the number of files removed.</summary>
        public int DeleteAll(string domainId)
        {
            int deleted = 0;
            foreach (int version in this.ListVersions(domainId))
                deleted += this.Delete(domainId, version);

            return deleted;
        }

        /// <summary>
        /// Keeps the <paramref name="keep"/> highest versions and deletes the rest. Returns the
        /// number of files removed.
        /// </summary>
        public int Prune(string domainId, int keep = DefaultVersionsToKeep)
        {
            if (keep < 1)
                throw new ArgumentOutOfRangeException(nameof(keep), keep, "At least the latest version must be kept.");

            IReadOnlyList<int> versions = this.ListVersions(domainId);
            int deleted = 0;

            for (int i = keep; i < versions.Count; i++)
                deleted += this.Delete(domainId, versions[i]);

            return deleted;
        }

        /// <summary>Every domain id with at least one save file on disk.</summary>
        public IReadOnlyList<string> ListDomains()
        {
            List<string> domains = new();
            if (!Directory.Exists(this._rootDirectory))
                return domains;

            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(this._rootDirectory, "*" + FileExtension))
            {
                if (TryParseFileName(Path.GetFileName(path), out string domainId, out _) && seen.Add(domainId))
                    domains.Add(domainId);
            }

            domains.Sort(StringComparer.Ordinal);
            return domains;
        }

        /// <summary>
        /// Integrity predicate handed to <see cref="AtomicFileStore"/>: a file counts as usable only
        /// when it decodes <i>and</i> its checksum verifies.
        /// </summary>
        private static bool IsEnvelopeReadable(string path)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(path);
                return SaveEnvelopeCodec.TryRead(raw, out _, out _) == EnvelopeReadStatus.Success;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
