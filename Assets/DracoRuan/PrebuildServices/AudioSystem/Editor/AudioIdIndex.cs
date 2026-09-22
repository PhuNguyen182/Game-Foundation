using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using UnityEditor;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor
{
    /// <summary>
    /// The one cached answer to "what audio ids and channels exist in this project".
    /// </summary>
    /// <remarks>
    /// <para>The window, both dropdowns, the entry inspector and the generator all read from here,
    /// so there is exactly one duplicate check in the codebase and no way for two surfaces to
    /// disagree about whether a name is free.</para>
    ///
    /// <para><see cref="Version"/> exists so a <c>PropertyDrawer</c> can tell whether its cached
    /// arrays are stale with an integer comparison per repaint instead of a project scan.</para>
    ///
    /// <para>The state is static and therefore dies with the domain, which is the correct lifetime:
    /// after a reload everything is rebuilt from disk anyway.</para>
    /// </remarks>
    public static class AudioIdIndex
    {
        private static readonly List<AudioIdRecord> EntryRecords = new List<AudioIdRecord>();
        private static readonly List<AudioIdRecord> ChannelRecords = new List<AudioIdRecord>();
        private static readonly Dictionary<string, string> OwnerPathById =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> KnownEntryPaths = new HashSet<string>(StringComparer.Ordinal);

        private static string[] _entryIds = Array.Empty<string>();
        private static string[] _entryLabels = Array.Empty<string>();
        private static string[] _channelIds = Array.Empty<string>();
        private static string[] _channelLabels = Array.Empty<string>();
        private static IReadOnlyList<AudioIdConflict> _conflicts = Array.Empty<AudioIdConflict>();
        private static AudioConfig _config;

        private static bool _isDirty = true;

        /// <summary>Bumped on every rebuild. Compare against it instead of rescanning.</summary>
        public static int Version { get; private set; }

        public static IReadOnlyList<AudioIdRecord> Entries
        {
            get
            {
                EnsureBuilt();
                return EntryRecords;
            }
        }

        public static IReadOnlyList<AudioIdRecord> Channels
        {
            get
            {
                EnsureBuilt();
                return ChannelRecords;
            }
        }

        public static string[] EntryIds
        {
            get
            {
                EnsureBuilt();
                return _entryIds;
            }
        }

        /// <summary>Entry ids labelled by their folder, so a long list groups in the dropdown.</summary>
        public static string[] EntryLabels
        {
            get
            {
                EnsureBuilt();
                return _entryLabels;
            }
        }

        public static string[] ChannelIds
        {
            get
            {
                EnsureBuilt();
                return _channelIds;
            }
        }

        public static string[] ChannelLabels
        {
            get
            {
                EnsureBuilt();
                return _channelLabels;
            }
        }

        /// <summary>Every reason the current set of ids cannot be generated.</summary>
        public static IReadOnlyList<AudioIdConflict> Conflicts
        {
            get
            {
                EnsureBuilt();
                return _conflicts;
            }
        }

        /// <summary>The project's audio config, if exactly one could be found.</summary>
        public static AudioConfig Config
        {
            get
            {
                EnsureBuilt();
                return _config;
            }
        }

        public static bool TryGetOwnerPath(string audioId, out string assetPath)
        {
            EnsureBuilt();
            return OwnerPathById.TryGetValue(audioId ?? string.Empty, out assetPath);
        }

        /// <summary>Whether <paramref name="assetPath"/> was an audio entry last time we looked.</summary>
        /// <remarks>
        /// A deleted asset cannot be loaded or type-queried, so matching against the paths we
        /// already know is the only way to notice one going away. Missing this leaves a ghost id in
        /// every dropdown until the next domain reload.
        /// </remarks>
        public static bool WasKnownEntryPath(string assetPath) => KnownEntryPaths.Contains(assetPath);

        /// <summary>Marks the cache stale. The next read rebuilds it.</summary>
        public static void Invalidate() => _isDirty = true;

        private static void EnsureBuilt()
        {
            if (!_isDirty)
                return;

            _isDirty = false;
            Version++;

            EntryRecords.Clear();
            ChannelRecords.Clear();
            OwnerPathById.Clear();
            KnownEntryPaths.Clear();

            CollectEntries();
            CollectChannels();

            _entryIds = ToIdArray(EntryRecords);
            _entryLabels = ToLabelArray(EntryRecords);
            _channelIds = ToIdArray(ChannelRecords);
            _channelLabels = _channelIds;

            _conflicts = AudioIdCollisionDetector.Detect(EntryRecords);
        }

        private static void CollectEntries()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(AudioEntry)}");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioEntry entry = AssetDatabase.LoadAssetAtPath<AudioEntry>(path);

                if (entry == null)
                    continue;

                KnownEntryPaths.Add(path);
                EntryRecords.Add(new AudioIdRecord(entry.Id, path));

                if (!string.IsNullOrEmpty(entry.Id))
                    OwnerPathById[entry.Id] = path;
            }

            EntryRecords.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        }

        private static void CollectChannels()
        {
            _config = null;
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(AudioConfig)}");

            if (guids.Length == 0)
                return;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _config = AssetDatabase.LoadAssetAtPath<AudioConfig>(path);

            if (_config?.Channels == null)
                return;

            for (int i = 0; i < _config.Channels.Count; i++)
            {
                string id = _config.Channels[i]?.Id;
                if (!string.IsNullOrEmpty(id))
                    ChannelRecords.Add(new AudioIdRecord(id, path));
            }
        }

        private static string[] ToIdArray(List<AudioIdRecord> records)
        {
            string[] ids = new string[records.Count];

            for (int i = 0; i < records.Count; i++)
                ids[i] = records[i].Id;

            return ids;
        }

        private static string[] ToLabelArray(List<AudioIdRecord> records)
        {
            string[] labels = new string[records.Count];

            for (int i = 0; i < records.Count; i++)
            {
                string folder = FolderNameOf(records[i].OwnerPath);
                labels[i] = string.IsNullOrEmpty(folder) ? records[i].Id : $"{folder}/{records[i].Id}";
            }

            return labels;
        }

        private static string FolderNameOf(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            int end = assetPath.LastIndexOf('/');
            if (end <= 0)
                return null;

            int start = assetPath.LastIndexOf('/', end - 1);
            return start < 0 ? assetPath.Substring(0, end) : assetPath.Substring(start + 1, end - start - 1);
        }
    }
}
