using System;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>
    /// Creates the project's <see cref="VibrationCollection"/> asset where the user chooses to put it.
    /// </summary>
    /// <remarks>
    /// There is exactly one of these per project (see <see cref="VibrationDatabaseLocator"/>), so this
    /// is only ever offered when <see cref="VibrationDatabaseLocator.Find"/> found none - creating a
    /// second one would immediately make the locator ambiguous again.
    /// </remarks>
    public static class VibrationCollectionCreationService
    {
        /// <returns>
        /// The new collection, or null. When the user cancelled, <paramref name="error"/> is also
        /// null - a cancel is not a failure and must not raise anything.
        /// </returns>
        public static VibrationCollection CreateInteractive(out string error)
        {
            error = null;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Vibration Collection",
                "VibrationCollection",
                "asset",
                "Choose where to store the VibrationCollection asset.",
                "Assets");

            if (string.IsNullOrEmpty(path))
                return null;

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"{path} is outside the Assets folder. A VibrationCollection has to live in the project.";
                return null;
            }

            // AssetDatabase.CreateAsset over an existing path deletes and recreates it, issuing a new
            // GUID and breaking every reference to whatever was there before.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                string unique = AssetDatabase.GenerateUniqueAssetPath(path);

                if (!VibrationDialogs.ConfirmSaveAsUnique(path, unique))
                    return null;

                path = unique;
            }

            VibrationCollection collection = ScriptableObject.CreateInstance<VibrationCollection>();
            AssetDatabase.CreateAsset(collection, path);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            return collection;
        }
    }
}
