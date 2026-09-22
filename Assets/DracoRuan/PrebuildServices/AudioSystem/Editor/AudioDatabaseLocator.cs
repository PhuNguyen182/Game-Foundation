using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using UnityEditor;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor
{
    /// <summary>
    /// Finds the project's single <see cref="AudioCollection"/>.
    /// </summary>
    /// <remarks>
    /// Ambiguity is an error rather than a coin toss. Registering new entries into whichever
    /// collection <c>FindAssets</c> happened to return first is how a game ends up with half its
    /// sounds filed in an asset that nothing loads, and the symptom — some sounds silently missing
    /// — looks nothing like the cause.
    /// </remarks>
    public static class AudioDatabaseLocator
    {
        public readonly struct Result
        {
            public Result(AudioCollection collection, string assetPath, string error, IReadOnlyList<string> candidates)
            {
                this.Collection = collection;
                this.AssetPath = assetPath;
                this.Error = error;
                this.Candidates = candidates;
            }

            public AudioCollection Collection { get; }
            public string AssetPath { get; }

            /// <summary>Null when exactly one collection was found.</summary>
            public string Error { get; }

            /// <summary>Every path found, for a message that names them.</summary>
            public IReadOnlyList<string> Candidates { get; }

            public bool Found => this.Collection != null;
        }

        public static Result Find()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(AudioCollection)}");
            List<string> paths = new List<string>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

            paths.Sort(System.StringComparer.Ordinal);

            if (paths.Count == 0)
                return new Result(null, null,
                    "No AudioCollection asset exists yet. Create one so new entries have somewhere to be "
                    + "registered.", paths);

            if (paths.Count > 1)
                return new Result(null, null,
                    "More than one AudioCollection exists: " + string.Join(", ", paths)
                    + ". Delete or merge all but one, so it is unambiguous which the game loads.", paths);

            AudioCollection collection = AssetDatabase.LoadAssetAtPath<AudioCollection>(paths[0]);
            return new Result(collection, paths[0], collection == null ? "The AudioCollection could not be loaded." : null, paths);
        }

        /// <summary>
        /// Adds <paramref name="entry"/> to <paramref name="collection"/>, into the category whose
        /// channel matches, or a new category when none does.
        /// </summary>
        /// <remarks>
        /// Written through <c>SerializedObject</c> rather than the public list so the change is
        /// recorded like any inspector edit and survives undo.
        /// </remarks>
        public static bool Register(AudioCollection collection, AudioEntry entry, out string error)
        {
            error = null;

            if (collection == null || entry == null)
            {
                error = "Nothing to register.";
                return false;
            }

            if (collection.Contains(entry))
                return true;

            SerializedObject serialized = new SerializedObject(collection);
            SerializedProperty categories = serialized.FindProperty("_categories");

            if (categories == null)
            {
                error = "The AudioCollection has no categories field. Was it renamed?";
                return false;
            }

            SerializedProperty target = FindCategoryForChannel(categories, entry.ChannelId)
                                        ?? AppendCategory(categories, entry.ChannelId);

            SerializedProperty entries = target.FindPropertyRelative("_entries");
            entries.InsertArrayElementAtIndex(entries.arraySize);
            entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = entry;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            return true;
        }

        private static SerializedProperty FindCategoryForChannel(SerializedProperty categories, string channelId)
        {
            if (string.IsNullOrEmpty(channelId))
                return categories.arraySize > 0 ? categories.GetArrayElementAtIndex(0) : null;

            for (int i = 0; i < categories.arraySize; i++)
            {
                SerializedProperty category = categories.GetArrayElementAtIndex(i);
                if (category.FindPropertyRelative("_channelId")?.stringValue == channelId)
                    return category;
            }

            return null;
        }

        private static SerializedProperty AppendCategory(SerializedProperty categories, string channelId)
        {
            categories.InsertArrayElementAtIndex(categories.arraySize);
            SerializedProperty created = categories.GetArrayElementAtIndex(categories.arraySize - 1);

            created.FindPropertyRelative("_name").stringValue =
                string.IsNullOrEmpty(channelId) ? "Uncategorised" : channelId;
            created.FindPropertyRelative("_channelId").stringValue = channelId;
            created.FindPropertyRelative("_entries").ClearArray();

            return created;
        }
    }
}
