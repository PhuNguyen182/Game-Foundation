using System.Collections.Generic;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>
    /// Deletes vibration entries, and with them their generated identifiers.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>AudioEntryDeletionService</c>: deleting an entry is supposed to remove its constant,
    /// and because generation is a full rebuild from disk, that happens by construction. The compile
    /// errors that follow at every call site are the intended outcome — quietly leaving a dead
    /// constant behind would let the project keep building and fail at runtime instead.
    /// </remarks>
    public static class VibrationEntryDeletionService
    {
        /// <summary>Deletes every entry given, after one confirmation, then regenerates the ids.</summary>
        /// <returns>How many assets were actually deleted.</returns>
        public static int DeleteInteractive(IReadOnlyList<VibrationEntry> entries, out string error)
        {
            error = null;

            if (entries == null || entries.Count == 0)
                return 0;

            List<string> paths = new List<string>(entries.Count);
            List<string> ids = new List<string>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(entries[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                paths.Add(path);

                if (!string.IsNullOrEmpty(entries[i].Id))
                    ids.Add(entries[i].Id);
            }

            if (paths.Count == 0)
                return 0;

            if (!VibrationDialogs.ConfirmDelete(paths, ids))
                return 0;

            VibrationDatabaseLocator.Result database = VibrationDatabaseLocator.Find();
            if (database.Found)
                VibrationDatabaseLocator.Unregister(database.Collection, entries);

            int deleted = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                if (AssetDatabase.DeleteAsset(paths[i]))
                    deleted++;
                else
                    error = $"Could not delete {paths[i]}.";
            }

            VibrationIdIndex.Invalidate();

            VibrationIdGenerationPlan plan = VibrationIdGenerationService.BuildPlan();
            if (plan.CanApply)
                VibrationIdGenerationService.Apply(plan, out _);
            else
                AssetDatabase.Refresh();

            return deleted;
        }
    }
}
