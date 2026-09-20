using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.Core.Serialization;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using Sirenix.OdinInspector.Editor;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// One save domain in the Local Data Manager: which versions exist, which one is loaded, and the
    /// Odin tree used to edit it.
    /// </summary>
    /// <remarks>
    /// <para><b>Goes through the same <see cref="SaveEnvelopeStore"/> as the game.</b> The previous
    /// version reimplemented the path convention and serialization itself, so the tool and the
    /// runtime were two independent implementations of one format — and its Save button would
    /// happily write a file the game could no longer read.</para>
    ///
    /// <para><b>Version discovery is one directory listing.</b> It used to call
    /// <c>File.Exists</c> a hundred times per load and another hundred per delete, for every entry.</para>
    ///
    /// <para><b>The Odin tree is built only while this entry is selected</b> and disposed as soon as
    /// it is not, so exactly one exists at a time.</para>
    /// </remarks>
    public sealed class LocalDataEntry
    {
        /// <summary>Latest plus three previous, per the retention policy.</summary>
        public const int MaxVisibleVersions = 4;

        private static readonly Regex PrettyNamePattern =
            new("(\\B[A-Z])", RegexOptions.Compiled);

        /// <summary>
        /// Display names are cached because they were being recomputed with an uncompiled regex on
        /// every OnGUI event, for every visible row.
        /// </summary>
        private static readonly Dictionary<Type, string> PrettyNameCache = new();

        private readonly SaveEnvelopeStore _store;
        private readonly IPayloadCodec _codec;
        private readonly Dictionary<int, FileStat> _fileStatCache = new();

        private PropertyTree _propertyTree;
        private object _data;

        /// <summary>Cached <see cref="File.GetLastWriteTimeUtc"/>/length pair for one version.</summary>
        private readonly struct FileStat
        {
            public FileStat(long sizeBytes, string modifiedUtc)
            {
                this.SizeBytes = sizeBytes;
                this.ModifiedUtc = modifiedUtc;
            }

            public long SizeBytes { get; }
            public string ModifiedUtc { get; }
        }

        public LocalDataEntry(SaveEnvelopeStore store, IPayloadCodec codec, string domainId, Type dataType)
        {
            this._store = store;
            this._codec = codec;
            this.DomainId = domainId;
            this.DataType = dataType;
            this.DisplayName = GetPrettyName(dataType);

            this.RefreshVersions();
        }

        public string DomainId { get; }

        public Type DataType { get; }

        /// <summary>Human-readable name, e.g. "Rise Progress Data V1".</summary>
        public string DisplayName { get; }

        /// <summary>Versions on disk, newest first, capped at <see cref="MaxVisibleVersions"/>.</summary>
        public IReadOnlyList<int> VisibleVersions { get; private set; } = Array.Empty<int>();

        /// <summary>Highest version on disk, or 0 when there is no save.</summary>
        public int LatestVersion { get; private set; }

        /// <summary>Version currently loaded, or 0 when nothing is loaded.</summary>
        public int LoadedVersion { get; private set; }

        /// <summary>True when data is loaded and editable.</summary>
        public bool HasData => this._data != null;

        /// <summary>True when there is at least one save file.</summary>
        public bool HasFiles => this.LatestVersion > 0;

        /// <summary>Header of the loaded file, for the detail pane.</summary>
        public SaveEnvelopeHeader LoadedHeader { get; private set; }

        /// <summary>Size of the loaded file in bytes, or -1 when unknown.</summary>
        public long LoadedSizeBytes { get; private set; } = -1;

        /// <summary>Last error, or null.</summary>
        public string LastError { get; private set; }

        /// <summary>True when the loaded version is not the newest on disk.</summary>
        public bool IsViewingOldVersion => this.HasData && this.LoadedVersion != this.LatestVersion;

        /// <summary>Re-reads which versions exist. One directory listing.</summary>
        public void RefreshVersions()
        {
            IReadOnlyList<int> all = this._store.ListVersions(this.DomainId);

            this.LatestVersion = all.Count > 0 ? all[0] : 0;

            List<int> visible = new();
            for (int i = 0; i < all.Count && i < MaxVisibleVersions; i++)
                visible.Add(all[i]);

            this.VisibleVersions = visible;

            // Called exactly when the files on disk may have changed (after Load, Save, Delete),
            // so this is the one place that needs to invalidate the per-version stat cache.
            this._fileStatCache.Clear();
        }

        /// <summary>Loads a version, or the newest when <paramref name="version"/> is 0.</summary>
        public bool Load(int version = 0)
        {
            this.RefreshVersions();

            int target = version > 0 ? version : this.LatestVersion;
            if (target <= 0)
            {
                this.LastError = "No save file exists for this domain.";
                return false;
            }

            EnvelopeReadStatus status =
                this._store.Read(this.DomainId, target, out SaveEnvelopeHeader header, out byte[] payload);

            if (status != EnvelopeReadStatus.Success)
            {
                this.LastError = DescribeReadStatus(status);
                this.Unload();
                return false;
            }

            try
            {
                this._data = this._codec.Deserialize(this.DataType, payload);
            }
            catch (Exception exception)
            {
                // The checksum passed, so the bytes are intact - the payload simply does not match
                // this version's class. Leave the file alone.
                this.LastError =
                    $"File is intact but does not match {this.DataType.Name}: {exception.Message}";
                this.Unload();
                return false;
            }

            this.LoadedVersion = target;
            this.LoadedHeader = header;
            this.LoadedSizeBytes = this.GetFileSize(target);
            this.LastError = null;

            this.RebuildPropertyTree();
            return true;
        }

        /// <summary>
        /// Writes back to the version that was loaded.
        /// </summary>
        /// <remarks>
        /// Saving to the newest version instead would turn "open an old save to look at it" into a
        /// silent migration: v1 contents written under a v3 label, which nothing downstream could
        /// detect. The caller warns when the loaded version is not the newest.
        /// </remarks>
        public bool Save()
        {
            if (this._data == null)
            {
                this.LastError = "No data loaded.";
                return false;
            }

            try
            {
                // Flush pending Odin edits before reading the object, or the last field the user
                // touched is not included.
                this._propertyTree?.ApplyChanges();

                byte[] payload = this._codec.Serialize(this.DataType, this._data);
                long revision = this.LoadedHeader.Revision + 1;

                this._store.Write(
                    this.DomainId,
                    this.LoadedVersion,
                    payload,
                    revision,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    this.LoadedHeader.DeviceEpochId,
                    SaveEnvelopeFlags.EditorAuthored);

                this.RefreshVersions();
                this.LoadedSizeBytes = this.GetFileSize(this.LoadedVersion);
                this.LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                this.LastError = exception.Message;
                return false;
            }
        }

        /// <summary>Deletes every version of this domain.</summary>
        public bool Delete(out int deletedFiles)
        {
            deletedFiles = 0;

            try
            {
                deletedFiles = this._store.DeleteAll(this.DomainId);
                this.Unload();
                this.RefreshVersions();
                this.LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                this.LastError = exception.Message;
                return false;
            }
        }

        /// <summary>File names that <see cref="Delete"/> would remove.</summary>
        public IReadOnlyList<string> GetFileNames()
        {
            List<string> names = new();
            foreach (int version in this._store.ListVersions(this.DomainId))
                names.Add(SaveEnvelopeStore.BuildFileName(this.DomainId, version));

            return names;
        }

        public string GetFileName(int version) => SaveEnvelopeStore.BuildFileName(this.DomainId, version);

        public long GetFileSize(int version) => this.GetFileStat(version).SizeBytes;

        public string GetModifiedUtc(int version) => this.GetFileStat(version).ModifiedUtc;

        /// <summary>
        /// Cached <c>File.Exists</c> + <c>GetLastWriteTimeUtc</c> pair for one version.
        /// </summary>
        /// <remarks>
        /// <see cref="LocalDataTool"/>'s detail header calls both of these once per OnGUI event —
        /// at least twice per frame (Layout, then Repaint) — and IMGUI regenerates several frames in
        /// a row while Odin animates a foldout or list resize. Hitting the filesystem synchronously
        /// on every one of those frames is exactly the kind of cost that is invisible on a fast local
        /// SSD but turns into visible stutter on a network share, a cloud-synced folder, or under
        /// antivirus on-access scanning. The cache is invalidated only where the file on disk
        /// actually changes: after <see cref="Load"/>, <see cref="Save"/>, and <see cref="Delete"/>.
        /// </remarks>
        private FileStat GetFileStat(int version)
        {
            if (this._fileStatCache.TryGetValue(version, out FileStat cached))
                return cached;

            FileStat stat = ReadFileStat(this._store.GetPath(this.DomainId, version));
            this._fileStatCache[version] = stat;
            return stat;
        }

        private static FileStat ReadFileStat(string path)
        {
            try
            {
                FileInfo info = new(path);
                if (!info.Exists)
                    return new FileStat(-1, "never");

                string modifiedUtc = info.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                return new FileStat(info.Length, modifiedUtc);
            }
            catch (Exception)
            {
                return new FileStat(-1, "unknown");
            }
        }

        /// <summary>Draws the data. Only called for the selected entry.</summary>
        public void DrawData()
        {
            if (this._propertyTree == null && this._data != null)
                this.RebuildPropertyTree();

            this._propertyTree?.Draw(false);
        }

        /// <summary>Releases the Odin tree. Called as soon as this entry stops being selected.</summary>
        public void ReleaseTree()
        {
            this._propertyTree?.Dispose();
            this._propertyTree = null;
        }

        public void Unload()
        {
            this.ReleaseTree();
            this._data = null;
            this.LoadedVersion = 0;
            this.LoadedHeader = default;
            this.LoadedSizeBytes = -1;
        }

        private void RebuildPropertyTree()
        {
            this.ReleaseTree();

            if (this._data != null)
                this._propertyTree = PropertyTree.Create(this._data);
        }

        private static string DescribeReadStatus(EnvelopeReadStatus status) => status switch
        {
            EnvelopeReadStatus.NotFound => "No save file exists for this version.",
            EnvelopeReadStatus.TooShort => "The file is empty or truncated.",
            EnvelopeReadStatus.BadMagic => "The file is not a DataFlow save.",
            EnvelopeReadStatus.UnsupportedFormatVersion =>
                "The file was written by a newer build using a format this one cannot read.",
            EnvelopeReadStatus.HeaderLengthInvalid => "The file header is malformed.",
            EnvelopeReadStatus.PayloadLengthInvalid => "The file is truncated or its length field is wrong.",
            EnvelopeReadStatus.ChecksumMismatch =>
                "The checksum does not match, so the file is corrupt. It has been left untouched.",
            EnvelopeReadStatus.SchemaDowngrade => "The payload is newer than this build supports.",
            _ => status.ToString()
        };

        private static string GetPrettyName(Type type)
        {
            if (PrettyNameCache.TryGetValue(type, out string cached))
                return cached;

            string pretty = PrettyNamePattern.Replace(type.Name, " $1");
            PrettyNameCache[type] = pretty;
            return pretty;
        }
    }
}