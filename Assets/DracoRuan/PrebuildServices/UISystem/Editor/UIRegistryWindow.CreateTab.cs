using System;
using System.Collections.Generic;
using System.IO;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using DracoRuan.PrebuildServices.UISystem.Editor.Registry;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DracoRuan.PrebuildServices.UISystem.Editor.DracoRuan.PrebuildServices.UISystem.Editor
{
    /// <summary>Create tab: author a new UIViewDefinition around an existing UIView prefab.</summary>
    public sealed partial class UIRegistryWindow
    {
        private UIViewDefinition _draft;
        private SerializedObject _draftSerialized;
        private bool _addToCollection = true;
        private readonly List<string> _draftMissing = new();
        private Vector2 _createScroll;

        private void EnsureDraft()
        {
            if (this._draft != null)
                return;

            this._draft = ScriptableObject.CreateInstance<UIViewDefinition>();
            this._draft.hideFlags = HideFlags.DontSave;
            this._draftSerialized = new SerializedObject(this._draft);
            this.RecomputeDraftMissing();
        }

        private void DestroyDraft()
        {
            if (this._draft != null)
                Object.DestroyImmediate(this._draft);

            this._draft = null;
            this._draftSerialized = null;
        }

        /// <summary>Resolves the view model type, so it runs only when the draft is edited
        /// rather than on every GUI event.</summary>
        private void RecomputeDraftMissing()
        {
            this._draftMissing.Clear();

            if (this._draftSerialized.FindProperty("viewPrefab").objectReferenceValue == null)
                this._draftMissing.Add("prefab");

            string vmName = this._draftSerialized.FindProperty("viewModelType")
                .FindPropertyRelative("assemblyQualifiedName").stringValue;
            if (string.IsNullOrEmpty(vmName) || Type.GetType(vmName) == null)
                this._draftMissing.Add("view model type");

            if (this._draftSerialized.FindProperty("layer").objectReferenceValue == null)
                this._draftMissing.Add("layer");
        }

        private void DrawCreateTab()
        {
            this._createScroll = EditorGUILayout.BeginScrollView(this._createScroll);

            EditorGUILayout.LabelField("Register prefab", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (this.DrawFields(this._draftSerialized))
                    this.RecomputeDraftMissing();

                EditorGUILayout.Space(2);

                this._addToCollection = EditorGUILayout.ToggleLeft(
                    this._targetCollection != null
                        ? $"Add to collection '{this._targetCollection.name}'"
                        : "Add to collection",
                    this._addToCollection);

                bool needsCollection = this._addToCollection && this._targetCollection == null;
                if (needsCollection)
                {
                    EditorGUILayout.HelpBox(
                        "No collection selected. Pick or create one in the 'Register to' bar, or untick 'Add to collection'.",
                        MessageType.Warning);
                }

                if (this._draftMissing.Count > 0)
                    EditorGUILayout.HelpBox($"Missing: {string.Join(", ", this._draftMissing)}", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply preset defaults"))
                        global::DracoRuan.PrebuildServices.UISystem.Editor.DracoRuan.PrebuildServices.UISystem.Editor.UIRegistryWindow.ApplyPresetDefaults(this._draftSerialized);

                    using (new EditorGUI.DisabledScope(this._draftMissing.Count > 0 || needsCollection))
                    {
                        if (GUILayout.Button("Create"))
                            this.CreateFromDraft();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void CreateFromDraft()
        {
            var view = this._draftSerialized.FindProperty("viewPrefab").objectReferenceValue as UIViewBase;
            string folder = this._addToCollection && this._targetCollection != null
                ? Path.GetDirectoryName(AssetDatabase.GetAssetPath((Object)this._targetCollection))
                : "Assets";

            string path = EditorUtility.SaveFilePanelInProject("Create UIViewDefinition",
                $"{view.name}Definition", "asset", "Choose where to save the new definition.", folder);
            if (string.IsNullOrEmpty(path))
                return;

            // The buffer holds the edits; commit them to the draft before it becomes the asset,
            // so every reference authored in the form is kept.
            this._draftSerialized.ApplyModifiedProperties();
            UIViewDefinition created = UIRegistryAssetOps.CreateDefinition(this._draft, path);

            if (this._addToCollection)
                UIRegistryAssetOps.AddToCollection(this._targetCollection, created);

            // The draft instance is now the asset; author the next one on a fresh draft.
            this._draft = null;
            this._draftSerialized = null;
            this.EnsureDraft();

            this.RefreshData();
            this._filterIndex = 0;
            this._search = string.Empty;
            this.RebuildFiltered();

            if (this.SelectDefinition(created))
            {
                this._tab = global::DracoRuan.PrebuildServices.UISystem.Editor.DracoRuan.PrebuildServices.UISystem.Editor.UIRegistryWindow.Tab.Registry;
                this.GoToPageOf(created);
            }

            GUIUtility.ExitGUI();
        }
    }
}
