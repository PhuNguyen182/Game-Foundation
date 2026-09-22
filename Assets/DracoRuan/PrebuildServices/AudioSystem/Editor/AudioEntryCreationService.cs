using System;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor
{
    /// <summary>What the New Entry form collected.</summary>
    public struct AudioEntryCreationRequest
    {
        public string Id;
        public string ChannelId;
        public AudioClip DirectClip;
    }

    /// <summary>
    /// Creates an <see cref="AudioEntry"/> asset where the user chooses to put it.
    /// </summary>
    public static class AudioEntryCreationService
    {
        /// <summary>
        /// Validates, asks where to save, creates the asset and registers it.
        /// </summary>
        /// <returns>
        /// The new entry, or null. When the user cancelled, <paramref name="error"/> is also null —
        /// a cancel is not a failure and must not raise anything.
        /// </returns>
        public static AudioEntry CreateInteractive(in AudioEntryCreationRequest request, out string error)
        {
            error = null;

            // Validate before opening anything. Opening a file dialog for an id that is about to be
            // rejected is the most annoying possible ordering.
            if (!AudioIdFormat.IsValid(request.Id, out string formatError))
            {
                error = formatError;
                return null;
            }

            AudioIdConflict? conflict = AudioIdCollisionDetector.CheckAgainst(
                request.Id, AudioIdIndex.Entries, ignoreOwnerPath: null);

            if (conflict.HasValue)
            {
                error = conflict.Value.Message;
                return null;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Audio Entry",
                request.Id,
                "asset",
                "Choose where to store this AudioEntry asset.",
                ResolveDefaultDirectory());

            if (string.IsNullOrEmpty(path))
                return null;

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"{path} is outside the Assets folder. An AudioEntry has to live in the project.";
                return null;
            }

            // The panel prompts to replace, but AssetDatabase.CreateAsset over an existing path
            // deletes and recreates, issuing a new GUID and breaking every reference to the old
            // asset. Refuse, and offer a free name instead.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                string unique = AssetDatabase.GenerateUniqueAssetPath(path);

                if (!AudioDialogs.ConfirmSaveAsUnique(path, unique))
                    return null;

                path = unique;
            }

            AudioEntry entry = ScriptableObject.CreateInstance<AudioEntry>();
            AssetDatabase.CreateAsset(entry, path);

            SerializedObject serialized = new SerializedObject(entry);
            serialized.FindProperty("_id").stringValue = request.Id;
            serialized.FindProperty("_channelId").stringValue = request.ChannelId ?? string.Empty;

            if (request.DirectClip != null)
                serialized.FindProperty("_clip").objectReferenceValue = request.DirectClip;

            // Written through SerializedObject rather than a public setter, so the runtime type
            // never exposes a way for gameplay code to change an id.
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(entry);
            AssetDatabase.SaveAssets();

            EditorPrefs.SetString(AudioEditorState.LastSaveDirectoryKey, DirectoryOf(path));
            AudioIdIndex.Invalidate();

            AudioDatabaseLocator.Result database = AudioDatabaseLocator.Find();
            if (database.Found)
                AudioDatabaseLocator.Register(database.Collection, entry, out _);
            else
                error = database.Error;

            return entry;
        }

        /// <summary>Where the save panel opens, first hit wins.</summary>
        public static string ResolveDefaultDirectory()
        {
            string remembered = EditorPrefs.GetString(AudioEditorState.LastSaveDirectoryKey, null);
            if (!string.IsNullOrEmpty(remembered) && AssetDatabase.IsValidFolder(remembered))
                return remembered;

            AudioConfig config = AudioIdIndex.Config;
            if (config != null && !string.IsNullOrEmpty(config.DefaultEntryFolder))
            {
                string configured = config.DefaultEntryFolder.StartsWith("Assets", StringComparison.Ordinal)
                    ? config.DefaultEntryFolder
                    : "Assets/" + config.DefaultEntryFolder.TrimStart('/');

                if (AssetDatabase.IsValidFolder(configured))
                    return configured;
            }

            AudioDatabaseLocator.Result database = AudioDatabaseLocator.Find();
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
