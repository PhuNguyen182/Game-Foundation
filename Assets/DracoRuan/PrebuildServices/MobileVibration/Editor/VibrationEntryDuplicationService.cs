using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>Copies a <see cref="VibrationEntry"/> asset, keeping its haptic data and a new id.</summary>
    public static class VibrationEntryDuplicationService
    {
        /// <returns>The new entry, or null when validation failed or the copy could not be made.</returns>
        public static VibrationEntry DuplicateInteractive(VibrationEntry source, string newId, out string error)
        {
            error = null;

            if (source == null)
            {
                error = "Nothing to duplicate.";
                return null;
            }

            if (!VibrationIdFormat.IsValid(newId, out string formatError))
            {
                error = formatError;
                return null;
            }

            VibrationIdConflict? conflict = VibrationIdCollisionDetector.CheckAgainst(
                newId, VibrationIdIndex.Entries, ignoreOwnerPath: null);

            if (conflict.HasValue)
            {
                error = conflict.Value.Message;
                return null;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string directory = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            string requestedPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{newId}.asset");

            if (!AssetDatabase.CopyAsset(sourcePath, requestedPath))
            {
                error = $"Could not copy {sourcePath} to {requestedPath}.";
                return null;
            }

            VibrationEntry copy = AssetDatabase.LoadAssetAtPath<VibrationEntry>(requestedPath);

            SerializedObject serialized = new SerializedObject(copy);
            serialized.FindProperty("id").stringValue = newId;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();

            VibrationIdIndex.Invalidate();

            VibrationDatabaseLocator.Result database = VibrationDatabaseLocator.Find();
            if (database.Found)
                VibrationDatabaseLocator.Register(database.Collection, copy, out _);
            else
                error = database.Error;

            VibrationIdGenerationPlan plan = VibrationIdGenerationService.BuildPlan();
            if (plan.CanApply)
                VibrationIdGenerationService.Apply(plan, out _);
            else
                AssetDatabase.Refresh();

            return copy;
        }
    }
}