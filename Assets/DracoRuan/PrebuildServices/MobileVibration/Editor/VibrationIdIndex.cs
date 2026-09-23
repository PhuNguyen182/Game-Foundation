using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>
    /// The one cached answer to "what vibration ids exist in this project".
    /// </summary>
    /// <remarks>
    /// <para>The window, the dropdown and the generator all read from here, so there is exactly one
    /// duplicate check in the codebase and no way for two surfaces to disagree about whether a name
    /// is free — the same role <c>AudioIdIndex</c> plays for audio.</para>
    ///
    /// <para><see cref="Version"/> exists so a <c>PropertyDrawer</c> can tell whether its cached
    /// arrays are stale with an integer comparison per repaint instead of a project scan.</para>
    ///
    /// <para>The state is static and therefore dies with the domain, which is the correct lifetime:
    /// after a reload everything is rebuilt from disk anyway.</para>
    /// </remarks>
    public static class VibrationIdIndex
    {
        private static readonly List<VibrationIdRecord> EntryRecords = new List<VibrationIdRecord>();
        private static readonly Dictionary<string, string> OwnerPathById =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> KnownEntryPaths = new HashSet<string>(StringComparer.Ordinal);

        private static string[] _entryIds = Array.Empty<string>();
        private static string[] _entryLabels = Array.Empty<string>();
        private static IReadOnlyList<VibrationIdConflict> _conflicts = Array.Empty<VibrationIdConflict>();

        private static bool _isDirty = true;

        /// <summary>Bumped on every rebuild. Compare against it instead of rescanning.</summary>
        public static int Version { get; private set; }

        public static IReadOnlyList<VibrationIdRecord> Entries
        {
            get
            {
                EnsureBuilt();
                return EntryRecords;
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

        /// <summary>Every reason the current set of ids cannot be generated.</summary>
        public static IReadOnlyList<VibrationIdConflict> Conflicts
        {
            get
            {
                EnsureBuilt();
                return _conflicts;
            }
        }

        public static bool TryGetOwnerPath(string vibrationId, out string assetPath)
        {
            EnsureBuilt();
            return OwnerPathById.TryGetValue(vibrationId ?? string.Empty, out assetPath);
        }

        /// <summary>Whether <paramref name="assetPath"/> was a vibration entry last time we looked.</summary>
        /// <remarks>
        /// A deleted asset cannot be loaded or type-queried, so matching against the paths we already
        /// know is the only way to notice one going away. Missing this leaves a ghost id in every
        /// dropdown until the next domain reload.
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
            OwnerPathById.Clear();
            KnownEntryPaths.Clear();

            CollectEntries();

            _entryIds = ToIdArray(EntryRecords);
            _entryLabels = ToLabelArray(EntryRecords);

            _conflicts = VibrationIdCollisionDetector.Detect(EntryRecords);
        }

        private static void CollectEntries()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(VibrationEntry)}");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                VibrationEntry entry = AssetDatabase.LoadAssetAtPath<VibrationEntry>(path);

                if (entry == null)
                    continue;

                KnownEntryPaths.Add(path);
                EntryRecords.Add(new VibrationIdRecord(entry.Id, path));

                if (!string.IsNullOrEmpty(entry.Id))
                    OwnerPathById[entry.Id] = path;
            }

            EntryRecords.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        }

        private static string[] ToIdArray(List<VibrationIdRecord> records)
        {
            string[] ids = new string[records.Count];

            for (int i = 0; i < records.Count; i++)
                ids[i] = records[i].Id;

            return ids;
        }

        private static string[] ToLabelArray(List<VibrationIdRecord> records)
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
