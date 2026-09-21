using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DracoRuan.Foundation.DataFlow.Core.Envelope;

namespace DracoRuan.Foundation.DataFlow.Core.Storage
{
    /// <summary>
    /// Reads and writes versioned save files: <c>{domainId}/{domainId}_v{schemaVersion}.sav</c>,
    /// each an envelope written through <see cref="AtomicFileStore"/>.
    ///
    /// <para>
    /// Every domain owns a directory. One domain's files - every schema version, plus the
    /// <c>.tmp</c> and <c>.bak</c> sidecars each of them can spawn - stay together instead of
    /// being interleaved with every other domain's in one flat listing. With three versions kept
    /// per domain and two sidecars apiece, a dozen domains put over a hundred files in a single
    /// directory, and telling at a glance which belong to what stops being possible.
    /// </para>
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

        /// <summary>Directory holding every domain's directory.</summary>
        public string RootDirectory => this._rootDirectory;

        /// <summary>
        /// Directory holding one domain's save files. The directory name is the domain id, which
        /// <see cref="DomainId"/> already restricts to characters legal in a path segment.
        /// </summary>
        /// <remarks>
        /// Returns a path whether or not the directory exists. Creating it is
        /// <see cref="AtomicFileStore.Write"/>'s job, so a domain that has never been saved leaves
        /// no empty folder behind.
        /// </remarks>
        public string GetDomainDirectory(string domainId)
        {
            DomainId.Validate(domainId);
            return Path.Combine(this._rootDirectory, domainId);
        }

        /// <summary>Absolute path of one domain's file at one schema version.</summary>
        /// <remarks>
        /// Adopts any of the domain's files still lying flat in the root before answering — see
        /// <see cref="AdoptFlatLayout"/>. Every read, write and delete resolves its path here, so
        /// hanging the move off this one method is what makes it impossible for a call site to
        /// look for a file in the new location while the file is still in the old one.
        /// </remarks>
        public string GetPath(string domainId, int schemaVersion)
        {
            DomainId.Validate(domainId);

            if (schemaVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion,
                    "Schema versions start at 1.");

            this.AdoptFlatLayout(domainId);

            return Path.Combine(this._rootDirectory, domainId, BuildFileName(domainId, schemaVersion));
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

            this.AdoptFlatLayout(domainId);

            List<int> versions = new();
            string domainDirectory = this.GetDomainDirectory(domainId);
            if (!Directory.Exists(domainDirectory))
                return versions;

            string prefix = domainId + VersionSeparator;
            string[] candidates;
            try
            {
                candidates = Directory.GetFiles(domainDirectory, prefix + "*" + FileExtension);
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
        /// <remarks>
        /// Removes the domain's directory too, but only when nothing is left in it. "Delete my save
        /// data" that leaves a tree of empty folders behind looks like it failed. Anything
        /// unexpected in there - a file this store did not write - keeps the directory, since
        /// deleting a stranger's file was never asked for.
        /// </remarks>
        public int DeleteAll(string domainId)
        {
            int deleted = 0;
            foreach (int version in this.ListVersions(domainId))
                deleted += this.Delete(domainId, version);

            string domainDirectory = this.GetDomainDirectory(domainId);
            try
            {
                if (Directory.Exists(domainDirectory) &&
                    Directory.GetFileSystemEntries(domainDirectory).Length == 0)
                {
                    Directory.Delete(domainDirectory);
                }
            }
            catch (Exception)
            {
                // Housekeeping only. The files are gone, which is what was asked for.
            }

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
        /// <remarks>
        /// Scans both layouts. A directory only counts when it actually holds a parseable save, so
        /// an empty folder - left by a delete, or created by hand - is not reported as a domain
        /// with data. Files still lying flat in the root are reported too, so a build that has not
        /// yet opened them does not present a player's saves as missing.
        /// </remarks>
        public IReadOnlyList<string> ListDomains()
        {
            List<string> domains = new();
            if (!Directory.Exists(this._rootDirectory))
                return domains;

            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach (string directory in Directory.GetDirectories(this._rootDirectory))
            {
                string domainId = Path.GetFileName(directory);
                if (!DomainId.IsValid(domainId, out _) || seen.Contains(domainId))
                    continue;

                foreach (string path in Directory.GetFiles(directory, "*" + FileExtension))
                {
                    if (TryParseFileName(Path.GetFileName(path), out string parsed, out _) &&
                        string.Equals(parsed, domainId, StringComparison.Ordinal))
                    {
                        seen.Add(domainId);
                        domains.Add(domainId);
                        break;
                    }
                }
            }

            // Pre-move saves. Reading one adopts it, but listing must not require that to have
            // happened yet - the Editor tool lists before it loads anything.
            foreach (string path in Directory.GetFiles(this._rootDirectory, "*" + FileExtension))
            {
                if (TryParseFileName(Path.GetFileName(path), out string domainId, out _) && seen.Add(domainId))
                    domains.Add(domainId);
            }

            domains.Sort(StringComparer.Ordinal);
            return domains;
        }

        /// <summary>
        /// Moves any of one domain's files still sitting flat in the root into its own directory.
        /// </summary>
        /// <remarks>
        /// <para>Saves written before this layout existed are in the root. Rather than a boot-time
        /// migration pass - which would need its own ordering, its own failure mode, and a way to
        /// know it had run - each domain adopts its own files the first time anything touches it.
        /// The two entry points that resolve a location, <see cref="GetPath"/> and
        /// <see cref="ListVersions"/>, both call this first, so the move happens before the first
        /// byte is read. It is idempotent: once the root holds no matching file, it costs one
        /// directory enumeration.</para>
        ///
        /// <para><b>Sidecars move with the target.</b> A <c>.tmp</c> or <c>.bak</c> left behind
        /// would strand the only good copy of an interrupted write in a directory nothing looks at
        /// any more, so <see cref="AtomicFileStore.Recover"/> could never find it.</para>
        ///
        /// <para>A failure here is not fatal and is not reported. The file stays in the root, where
        /// the next call tries again and where the flat-layout branch of
        /// <see cref="ListDomains"/> still finds it.</para>
        /// </remarks>
        private void AdoptFlatLayout(string domainId)
        {
            if (!Directory.Exists(this._rootDirectory))
                return;

            string[] candidates;
            try
            {
                candidates = Directory.GetFiles(this._rootDirectory, domainId + VersionSeparator + "*");
            }
            catch (Exception)
            {
                return;
            }

            if (candidates.Length == 0)
                return;

            string domainDirectory = this.GetDomainDirectory(domainId);

            foreach (string path in candidates)
            {
                string fileName = Path.GetFileName(path);
                if (!BelongsToDomain(fileName, domainId))
                    continue;

                try
                {
                    if (!Directory.Exists(domainDirectory))
                        Directory.CreateDirectory(domainDirectory);

                    string destination = Path.Combine(domainDirectory, fileName);

                    // The destination wins. It was written by a build that already used this
                    // layout, so it is the newer of the two; overwriting it with the flat file
                    // would roll the player back to their pre-move save.
                    if (File.Exists(destination))
                        File.Delete(path);
                    else
                        File.Move(path, destination);
                }
                catch (Exception)
                {
                    // Housekeeping. Leave the file where it is and try again next time.
                }
            }
        }

        /// <summary>
        /// Whether <paramref name="fileName"/> is one of <paramref name="domainId"/>'s files -
        /// a save at some version, or one of that save's <c>.tmp</c> / <c>.bak</c> sidecars.
        /// </summary>
        private static bool BelongsToDomain(string fileName, string domainId)
        {
            string stem = fileName;

            if (stem.EndsWith(AtomicFileStore.TempSuffix, StringComparison.OrdinalIgnoreCase))
                stem = stem.Substring(0, stem.Length - AtomicFileStore.TempSuffix.Length);
            else if (stem.EndsWith(AtomicFileStore.BackupSuffix, StringComparison.OrdinalIgnoreCase))
                stem = stem.Substring(0, stem.Length - AtomicFileStore.BackupSuffix.Length);

            return TryParseFileName(stem, out string parsed, out _) &&
                   string.Equals(parsed, domainId, StringComparison.Ordinal);
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
