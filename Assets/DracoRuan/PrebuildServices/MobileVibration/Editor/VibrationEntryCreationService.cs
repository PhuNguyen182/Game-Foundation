using System;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>What the New Entry form collected.</summary>
    public struct VibrationEntryCreationRequest
    {
        public string Id;
    }

    /// <summary>
    /// Creates a <see cref="VibrationEntry"/> asset where the user chooses to put it.
    /// </summary>
    public static class VibrationEntryCreationService
    {
        /// <summary>
        /// Validates, asks where to save, creates the asset and registers it.
        /// </summary>
        /// <returns>
        /// The new entry, or null. When the user cancelled, <paramref name="error"/> is also null —
        /// a cancel is not a failure and must not raise anything.
        /// </returns>
        public static VibrationEntry CreateInteractive(in VibrationEntryCreationRequest request, out string error)
        {
            error = null;

            // Validate before opening anything. Opening a file dialog for an id that is about to be
            // rejected is the most annoying possible ordering.
            if (!VibrationIdFormat.IsValid(request.Id, out string formatError))
            {
                error = formatError;
                return null;
            }

            VibrationIdConflict? conflict = VibrationIdCollisionDetector.CheckAgainst(
                request.Id, VibrationIdIndex.Entries, ignoreOwnerPath: null);

            if (conflict.HasValue)
            {
                error = conflict.Value.Message;
                return null;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Vibration Entry",
                request.Id,
                "asset",
                "Choose where to store this VibrationEntry asset.",
                ResolveDefaultDirectory());

            if (string.IsNullOrEmpty(path))
                return null;

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"{path} is outside the Assets folder. A VibrationEntry has to live in the project.";
                return null;
            }

            // The panel prompts to replace, but AssetDatabase.CreateAsset over an existing path
            // deletes and recreates, issuing a new GUID and breaking every reference to the old
            // asset. Refuse, and offer a free name instead.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                string unique = AssetDatabase.GenerateUniqueAssetPath(path);

                if (!VibrationDialogs.ConfirmSaveAsUnique(path, unique))
                    return null;

                path = unique;
            }

            VibrationEntry entry = ScriptableObject.CreateInstance<VibrationEntry>();
            AssetDatabase.CreateAsset(entry, path);

            SerializedObject serialized = new SerializedObject(entry);
            serialized.FindProperty("id").stringValue = request.Id;

            // Written through SerializedObject rather than a public setter, so the runtime type
            // never exposes a way for gameplay code to change an id.
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(entry);
            AssetDatabase.SaveAssets();

            EditorPrefs.SetString(VibrationEditorState.LastSaveDirectoryKey, DirectoryOf(path));
            VibrationIdIndex.Invalidate();

            VibrationDatabaseLocator.Result database = VibrationDatabaseLocator.Find();
            if (database.Found)
                VibrationDatabaseLocator.Register(database.Collection, entry, out _);
            else
                error = database.Error;

            return entry;
        }

        /// <summary>Where the save panel opens, first hit wins.</summary>
        public static string ResolveDefaultDirectory()
        {
            string remembered = EditorPrefs.GetString(VibrationEditorState.LastSaveDirectoryKey, null);
            if (!string.IsNullOrEmpty(remembered) && AssetDatabase.IsValidFolder(remembered))
                return remembered;

            VibrationDatabaseLocator.Result database = VibrationDatabaseLocator.Find();
            if (database.Found)
                return DirectoryOf(database.AssetPath);

            return "Assets";
        }

        private static string DirectoryOf(string assetPath)
        {
            int end = assetPath.LastIndexOf('/');
            return end <= 0 ? "Assets" : assetPath.Substring(0, end);
        }
    }
}
