using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.UISystem.Data;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor.Registry
{
    /// <summary>Asset-level operations behind UIRegistryWindow: find/create/delete definitions
    /// and collections, and keep collection membership in sync. GUI-free so the window stays a
    /// thin layer. Collection edits go through SerializedObject (the list is a private
    /// serialized field) so they are undoable and mark the collection dirty.</summary>
    public static class UIRegistryAssetOps
    {
        private const string DefinitionsField = "definitions";

        public static List<UIViewCollection> FindCollections() => FindAll<UIViewCollection>();

        public static List<UIViewDefinition> FindDefinitions() => FindAll<UIViewDefinition>();

        private static List<T> FindAll<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();

        public static bool CollectionContains(UIViewCollection collection, UIViewDefinition definition) =>
            collection.Definitions.Contains(definition);

        public static UIViewCollection CreateCollection(string assetPath)
        {
            var collection = ScriptableObject.CreateInstance<UIViewCollection>();
            AssetDatabase.CreateAsset(collection, assetPath);
            AssetDatabase.SaveAssets();
            return collection;
        }

        /// <summary>Persists the draft instance itself as the asset, so every reference it
        /// already holds (prefab, layer, type refs) is kept exactly as authored.</summary>
        public static UIViewDefinition CreateDefinition(UIViewDefinition draft, string assetPath)
        {
            draft.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(draft, assetPath);
            AssetDatabase.SaveAssets();
            return draft;
        }

        public static void AddToCollection(UIViewCollection collection, UIViewDefinition definition)
        {
            if (collection == null || definition == null || CollectionContains(collection, definition))
                return;

            var serialized = new SerializedObject(collection);
            SerializedProperty list = serialized.FindProperty(DefinitionsField);
            int index = list.arraySize;
            list.arraySize++;
            list.GetArrayElementAtIndex(index).objectReferenceValue = definition;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssetIfDirty(collection);
        }

        public static void RemoveFromCollection(UIViewCollection collection, UIViewDefinition definition)
        {
            var serialized = new SerializedObject(collection);
            SerializedProperty list = serialized.FindProperty(DefinitionsField);
            bool changed = false;

            // Backwards so removing shifts nothing we still have to visit. Null slots (a
            // previously deleted definition) are dropped along the way.
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                Object entry = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (entry != null && entry != definition)
                    continue;

                // A non-null element must be nulled first, otherwise DeleteArrayElementAtIndex
                // only clears the reference instead of removing the slot.
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
                changed = true;
            }

            if (!changed)
                return;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssetIfDirty(collection);
        }

        /// <summary>Unregisters the definition from every collection, then deletes its asset.</summary>
        public static void Delete(UIViewDefinition definition, IEnumerable<UIViewCollection> collections)
        {
            foreach (UIViewCollection collection in collections)
                RemoveFromCollection(collection, definition);

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(definition));
        }
    }
}
