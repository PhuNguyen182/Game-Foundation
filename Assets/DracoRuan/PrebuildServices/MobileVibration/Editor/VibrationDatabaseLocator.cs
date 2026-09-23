using System.Collections.Generic;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using UnityEditor;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>
    /// Finds the project's single <see cref="VibrationCollection"/>.
    /// </summary>
    /// <remarks>
    /// Ambiguity is an error rather than a coin toss, the same way <c>AudioDatabaseLocator</c> treats
    /// it: registering new entries into whichever collection <c>FindAssets</c> happened to return
    /// first is how a game ends up with half its haptics filed in an asset that nothing loads, and
    /// the symptom — some vibrations silently missing — looks nothing like the cause.
    /// </remarks>
    public static class VibrationDatabaseLocator
    {
        public readonly struct Result
        {
            public Result(VibrationCollection collection, string assetPath, string error, IReadOnlyList<string> candidates)
            {
                this.Collection = collection;
                this.AssetPath = assetPath;
                this.Error = error;
                this.Candidates = candidates;
            }

            public VibrationCollection Collection { get; }
            public string AssetPath { get; }

            /// <summary>Null when exactly one collection was found.</summary>
            public string Error { get; }

            /// <summary>Every path found, for a message that names them.</summary>
            public IReadOnlyList<string> Candidates { get; }

            public bool Found => this.Collection != null;
        }

        public static Result Find()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(VibrationCollection)}");
            List<string> paths = new List<string>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

            paths.Sort(System.StringComparer.Ordinal);

            if (paths.Count == 0)
                return new Result(null, null,
                    "No VibrationCollection asset exists yet. Create one so new entries have somewhere "
                    + "to be registered.", paths);

            if (paths.Count > 1)
                return new Result(null, null,
                    "More than one VibrationCollection exists: " + string.Join(", ", paths)
                    + ". Delete or merge all but one, so it is unambiguous which the game loads.", paths);

            VibrationCollection collection = AssetDatabase.LoadAssetAtPath<VibrationCollection>(paths[0]);
            return new Result(collection, paths[0],
                collection == null ? "The VibrationCollection could not be loaded." : null, paths);
        }

        /// <summary>Adds <paramref name="entry"/> to <paramref name="collection"/>'s flat entry list.</summary>
        /// <remarks>
        /// Written through <c>SerializedObject</c> rather than the public list so the change is
        /// recorded like any inspector edit and survives undo. There is no category to place it in —
        /// <c>VibrationCollection</c> is a flat list, unlike <c>AudioCollection</c>.
        /// </remarks>
        public static bool Register(VibrationCollection collection, VibrationEntry entry, out string error)
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
            SerializedProperty entries = serialized.FindProperty("_entries");

            if (entries == null)
            {
                error = "The VibrationCollection has no entries field. Was it renamed?";
                return false;
            }

            entries.InsertArrayElementAtIndex(entries.arraySize);
            entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = entry;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            return true;
        }
    }
}
