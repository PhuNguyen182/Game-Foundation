using System;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>Changes a <see cref="VibrationEntry"/>'s id, after validating and confirming.</summary>
    /// <remarks>
    /// The asset's file name is kept in sync only when it currently matches the old id — a deliberate
    /// convention, not every entry's file name follows its id, and renaming an unrelated file name
    /// would be a surprise the author did not ask for.
    /// </remarks>
    public static class VibrationEntryRenameService
    {
        /// <returns>True when the id was changed. False on validation failure or user cancel.</returns>
        public static bool RenameInteractive(VibrationEntry entry, string newId, out string error)
        {
            error = null;

            if (entry == null)
            {
                error = "Nothing to rename.";
                return false;
            }

            string oldId = entry.Id;
            string ownPath = AssetDatabase.GetAssetPath(entry);

            if (!VibrationIdFormat.IsValid(newId, out string formatError))
            {
                error = formatError;
                return false;
            }

            VibrationIdConflict? conflict = VibrationIdCollisionDetector.CheckAgainst(
                newId, VibrationIdIndex.Entries, ignoreOwnerPath: ownPath);

            if (conflict.HasValue)
            {
                error = conflict.Value.Message;
                return false;
            }

            if (string.Equals(oldId, newId, StringComparison.Ordinal))
                return false;

            if (!VibrationDialogs.ConfirmRename(oldId, newId))
                return false;

            SerializedObject serialized = new SerializedObject(entry);
            serialized.FindProperty("id").stringValue = newId;

            // Same convention as creation: written directly, not through a public setter, so gameplay
            // code never gets a way to change an id at runtime.
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(entry);

            string fileName = System.IO.Path.GetFileNameWithoutExtension(ownPath);
            if (string.Equals(fileName, oldId, StringComparison.Ordinal))
            {
                string renameError = AssetDatabase.RenameAsset(ownPath, newId);
                if (!string.IsNullOrEmpty(renameError))
                    error = $"Id changed, but the asset file could not be renamed: {renameError}";
            }

            AssetDatabase.SaveAssets();
            VibrationIdIndex.Invalidate();

            VibrationIdGenerationPlan plan = VibrationIdGenerationService.BuildPlan();
            if (plan.CanApply)
                VibrationIdGenerationService.Apply(plan, out _);
            else
                AssetDatabase.Refresh();

            return true;
        }
    }
}