using System;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor
{
    /// <summary>
    /// Creates the project's <see cref="AudioCollection"/> asset where the user chooses to put it.
    /// </summary>
    /// <remarks>
    /// There is exactly one of these per project (see <see cref="AudioDatabaseLocator"/>), so this is
    /// only ever offered when <see cref="AudioDatabaseLocator.Find"/> found none - creating a second
    /// one would immediately make the locator ambiguous again.
    /// </remarks>
    public static class AudioCollectionCreationService
    {
        /// <returns>
        /// The new collection, or null. When the user cancelled, <paramref name="error"/> is also
        /// null - a cancel is not a failure and must not raise anything.
        /// </returns>
        public static AudioCollection CreateInteractive(out string error)
        {
            error = null;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Audio Collection",
                "AudioCollection",
                "asset",
                "Choose where to store the AudioCollection asset.",
                "Assets");

            if (string.IsNullOrEmpty(path))
                return null;

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"{path} is outside the Assets folder. An AudioCollection has to live in the project.";
                return null;
            }

            // AssetDatabase.CreateAsset over an existing path deletes and recreates it, issuing a new
            // GUID and breaking every reference to whatever was there before.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                string unique = AssetDatabase.GenerateUniqueAssetPath(path);

                if (!AudioDialogs.ConfirmSaveAsUnique(path, unique))
                    return null;

                path = unique;
            }

            AudioCollection collection = ScriptableObject.CreateInstance<AudioCollection>();
            AssetDatabase.CreateAsset(collection, path);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            return collection;
        }
    }
}
