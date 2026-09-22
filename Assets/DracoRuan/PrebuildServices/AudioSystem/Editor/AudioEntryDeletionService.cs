using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEditor;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor
{
    /// <summary>
    /// Deletes audio entries, and with them their generated identifiers.
    /// </summary>
    /// <remarks>
    /// <para>Deleting an entry is supposed to remove its constant. Because generation is a full
    /// rebuild from disk, that happens by construction: the asset is gone, so nothing emits a
    /// constant for it.</para>
    ///
    /// <para>The compile errors that follow at every call site are the intended outcome, not a side
    /// effect. Quietly leaving a dead constant behind would let the project keep building and fail
    /// at runtime instead, which is the failure the generated class exists to prevent.</para>
    /// </remarks>
    public static class AudioEntryDeletionService
    {
        /// <summary>
        /// Deletes every entry given, after one confirmation, then regenerates the ids.
        /// </summary>
        /// <returns>How many assets were actually deleted.</returns>
        public static int DeleteInteractive(IReadOnlyList<AudioEntry> entries, out string error)
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

            if (!AudioDialogs.ConfirmDelete(paths, ids))
                return 0;

            AudioDatabaseLocator.Result database = AudioDatabaseLocator.Find();
            if (database.Found)
                Unregister(database.Collection, entries);

            int deleted = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                if (AssetDatabase.DeleteAsset(paths[i]))
                    deleted++;
                else
                    error = $"Could not delete {paths[i]}.";
            }

            AudioIdIndex.Invalidate();

            // One regeneration and one refresh for the whole batch. Recompiling per asset would
            // reload the domain out from under the loop still running it.
            AudioIdGenerationPlan plan = AudioIdGenerationService.BuildPlan();
            if (plan.CanApply)
                AudioIdGenerationService.Apply(plan, out _);
            else
                AssetDatabase.Refresh();

            return deleted;
        }

        /// <remarks>
        /// Removed through <c>SerializedObject</c>, and backwards, so earlier indices stay valid
        /// while the array shrinks.
        /// </remarks>
        private static void Unregister(AudioCollection collection, IReadOnlyList<AudioEntry> entries)
        {
            SerializedObject serialized = new SerializedObject(collection);
            SerializedProperty categories = serialized.FindProperty("_categories");

            if (categories == null)
                return;

            for (int categoryIndex = 0; categoryIndex < categories.arraySize; categoryIndex++)
            {
                SerializedProperty list = categories.GetArrayElementAtIndex(categoryIndex)
                    .FindPropertyRelative("_entries");

                if (list == null)
                    continue;

                for (int entryIndex = list.arraySize - 1; entryIndex >= 0; entryIndex--)
                {
                    UnityEngine.Object referenced = list.GetArrayElementAtIndex(entryIndex).objectReferenceValue;

                    if (Contains(entries, referenced))
                        list.DeleteArrayElementAtIndex(entryIndex);
                }
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
        }

        private static bool Contains(IReadOnlyList<AudioEntry> entries, UnityEngine.Object candidate)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i], candidate))
                    return true;
            }

            return false;
        }
    }
}
