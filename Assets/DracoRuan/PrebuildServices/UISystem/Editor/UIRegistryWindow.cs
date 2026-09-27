using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor
{
    /// <summary>REWRITE_PLAN.md mục 5 bước 8: browse every UIViewDefinition across every
    /// UIViewCollection in the project, with UIRegistryValidator's findings inline.</summary>
    public sealed class UIRegistryWindow : EditorWindow
    {
        private Vector2 _scroll;
        private List<UIViewCollection> _collections = new List<UIViewCollection>();
        private List<UIValidationFinding> _findings = new List<UIValidationFinding>();

        [MenuItem("Tools/DracoRuan/UISystem/Registry Window")]
        private static void Open() => GetWindow<UIRegistryWindow>("UISystem Registry");

        private void OnEnable() => this.Refresh();

        private void Refresh()
        {
            this._collections = AssetDatabase.FindAssets($"t:{nameof(UIViewCollection)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UIViewCollection>)
                .Where(c => c != null)
                .ToList();

            this._findings = this._collections
                .SelectMany(c => UIRegistryValidator.Validate(c.Definitions))
                .ToList();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Refresh"))
                this.Refresh();

            EditorGUILayout.Space();

            this._scroll = EditorGUILayout.BeginScrollView(this._scroll);

            foreach (UIViewCollection collection in this._collections)
            {
                EditorGUILayout.LabelField(collection.name, EditorStyles.boldLabel);
                foreach (UIViewDefinition definition in collection.Definitions)
                {
                    if (definition == null)
                        continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(definition, typeof(UIViewDefinition), false);
                    EditorGUILayout.LabelField(definition.ViewModelType?.Name ?? "<unresolved>", GUILayout.Width(220));
                    EditorGUILayout.LabelField(definition.Layer != null ? definition.Layer.LayerName : "<no layer>", GUILayout.Width(100));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();
            }

            if (this._findings.Count > 0)
            {
                EditorGUILayout.LabelField("Findings", EditorStyles.boldLabel);
                foreach (UIValidationFinding finding in this._findings)
                {
                    MessageType messageType = finding.Severity == UIValidationSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning;

                    EditorGUILayout.HelpBox(finding.Message, messageType);
                    if (finding.Context != null && GUILayout.Button("Select", GUILayout.Width(80)))
                        Selection.activeObject = finding.Context;
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
