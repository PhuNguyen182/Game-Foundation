using System;
using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Editor.Registry;
using DracoRuan.PrebuildServices.UISystem.Editor.Validation;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor
{
    /// <summary>
    /// Registry tool, two tabs: Create (register an existing UIView prefab as a new
    /// UIViewDefinition) and Registry (paged master/detail browser with buffered editing and
    /// an Apply button). Everything the GUI needs per event is cached at Refresh time - OnGUI
    /// only draws, never resolves types or scans assets - so scrolling stays cheap however many
    /// definitions exist.
    /// </summary>
    public sealed partial class UIRegistryWindow : EditorWindow
    {
        private enum Tab
        {
            Create,
            Registry,
        }

        /// <summary>Everything a list row needs, resolved once (ViewModelType is a
        /// Type.GetType string parse, far too slow to call per row per event).</summary>
        private sealed class RowInfo
        {
            public UIViewDefinition Definition;
            public string Name;
            public string VmName;
            public string LayerName;
            public string SearchKey;
            public List<UIViewCollection> Owners;
            public List<UIValidationFinding> Errors;
        }

        private static readonly string[] TabNames = { "Create", "Registry" };

        private Tab _tab;
        private bool _showAllFields;

        private List<UIViewCollection> _collections = new();
        private string[] _collectionNames = Array.Empty<string>();
        private UIViewCollection _targetCollection;

        private readonly List<RowInfo> _rows = new();
        private readonly Dictionary<UIViewDefinition, RowInfo> _rowByDefinition = new();
        private string _summary = string.Empty;

        [MenuItem("Tools/DracoRuan/UISystem/Registry Window")]
        private static void Open() => GetWindow<UIRegistryWindow>("UISystem Registry");

        private void OnEnable()
        {
            this.RefreshData();
            this.EnsureDraft();
        }

        private void OnDisable() => this.DestroyDraft();

        private void OnDestroy()
        {
            // Closing the window would silently drop buffered edits; last chance to keep them.
            if (this._hasPending && this._selected != null &&
                EditorUtility.DisplayDialog("Unapplied changes",
                    $"'{this._selected.name}' has changes that have not been applied.", "Apply", "Discard"))
            {
                this.ApplyPending();
            }
        }

        // ---------------------------------------------------------------
        // Data (rebuilt on Refresh / after Apply, never inside OnGUI)
        // ---------------------------------------------------------------

        /// <summary>Full reload from the AssetDatabase. Keeps the selection (and any buffered
        /// edits) when the selected definition still exists.</summary>
        private void RefreshData()
        {
            this._collections = UIRegistryAssetOps.FindCollections();
            this._collectionNames = this._collections.Select(c => c.name).ToArray();
            if (this._targetCollection == null || !this._collections.Contains(this._targetCollection))
                this._targetCollection = this._collections.FirstOrDefault();

            this.RebuildRows();
            this.RebuildFilterOptions();
            this.RebuildFiltered();

            if (this._selected == null)
                this.ClearSelection();
            else if (!this._hasPending)
                this._selectedSerialized = new SerializedObject(this._selected);
        }

        private void RebuildRows()
        {
            List<UIViewDefinition> allDefinitions = UIRegistryAssetOps.FindDefinitions();

            var owners = new Dictionary<UIViewDefinition, List<UIViewCollection>>();
            foreach (UIViewCollection collection in this._collections)
            {
                foreach (UIViewDefinition definition in collection.Definitions)
                {
                    if (definition == null)
                        continue;

                    if (!owners.TryGetValue(definition, out List<UIViewCollection> list))
                        owners[definition] = list = new List<UIViewCollection>();

                    list.Add(collection);
                }
            }

            List<UIValidationFinding> findings = this._collections
                .SelectMany(c => UIRegistryValidator.Validate(c.Definitions))
                .Concat(UIRegistryCompletenessChecker.Check(this._collections, allDefinitions))
                .ToList();

            var errorsByDefinition = new Dictionary<UIViewDefinition, List<UIValidationFinding>>();
            foreach (UIValidationFinding finding in findings)
            {
                if (finding.Context is not UIViewDefinition definition)
                    continue;

                if (!errorsByDefinition.TryGetValue(definition, out List<UIValidationFinding> list))
                    errorsByDefinition[definition] = list = new List<UIValidationFinding>();

                list.Add(finding);
            }

            this._rows.Clear();
            this._rowByDefinition.Clear();

            foreach (UIViewDefinition definition in allDefinitions.OrderBy(d => d.name,
                         StringComparer.OrdinalIgnoreCase))
            {
                string vmName = definition.ViewModelType?.Name ?? "<unresolved>";
                var row = new RowInfo
                {
                    Definition = definition,
                    Name = definition.name,
                    VmName = vmName,
                    LayerName = definition.Layer != null ? definition.Layer.LayerName : "<no layer>",
                    SearchKey = $"{definition.name} {vmName}".ToLowerInvariant(),
                    Owners = owners.TryGetValue(definition, out List<UIViewCollection> owned)
                        ? owned
                        : new List<UIViewCollection>(),
                    Errors = errorsByDefinition.TryGetValue(definition, out List<UIValidationFinding> errors)
                        ? errors
                        : new List<UIValidationFinding>(),
                };

                this._rows.Add(row);
                this._rowByDefinition[definition] = row;
            }

            this._summary = $"{this._rows.Count} definitions · {findings.Count} error(s)";
        }

        // ---------------------------------------------------------------
        // GUI frame
        // ---------------------------------------------------------------

        private void OnGUI()
        {
            EnsureStyles();
            this.EnsureDraft();

            this.DrawToolbar();
            this.DrawTargetCollectionBar();
            this._tab = (Tab)GUILayout.Toolbar((int)this._tab, TabNames, GUILayout.Height(22));

            if (this._tab == Tab.Create)
                this.DrawCreateTab();
            else
                this.DrawRegistryTab();

            this.ProcessRequestedSelection();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)) &&
                    this.ResolvePending())
                {
                    this._hasPending = false;
                    this.RefreshData();
                    GUIUtility.ExitGUI();
                }

                this._showAllFields = GUILayout.Toggle(this._showAllFields, "Show all fields",
                    EditorStyles.toolbarButton, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>Where new registrations go: used by the Create tab and by "Add to
        /// collection" in the Registry tab. Independent from the Registry tab's view filter.</summary>
        private void DrawTargetCollectionBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Register to:", EditorStyles.toolbarButton, GUILayout.Width(80));

                if (this._collections.Count == 0)
                {
                    GUILayout.Label("No collection yet", EditorStyles.miniLabel);
                    if (GUILayout.Button("Create Collection", EditorStyles.toolbarButton, GUILayout.Width(120)))
                        this.CreateCollection();
                }
                else
                {
                    int index = Mathf.Max(0, this._collections.IndexOf(this._targetCollection));
                    int selected = EditorGUILayout.Popup(index, this._collectionNames, EditorStyles.toolbarPopup,
                        GUILayout.Width(220));
                    this._targetCollection = this._collections[Mathf.Clamp(selected, 0, this._collections.Count - 1)];

                    if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(45)))
                        EditorGUIUtility.PingObject(this._targetCollection);

                    if (GUILayout.Button("New Collection...", EditorStyles.toolbarButton, GUILayout.Width(120)))
                        this.CreateCollection();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void CreateCollection()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create UIViewCollection", "UIViewCollection",
                "asset", "Choose where to save the new collection.", "Assets");
            if (string.IsNullOrEmpty(path))
                return;

            this._targetCollection = UIRegistryAssetOps.CreateCollection(path);
            this.RefreshData();
            GUIUtility.ExitGUI();
        }

        // ---------------------------------------------------------------
        // Field drawing (shared by the Create draft and the Registry detail pane)
        // ---------------------------------------------------------------

        private static GUIStyle _nameStyle;
        private static GUIStyle _vmStyle;
        private static GUIStyle _badgeStyle;

        private static void EnsureStyles()
        {
            if (_nameStyle != null)
                return;

            _nameStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            _vmStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            _badgeStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
            _badgeStyle.normal.textColor = new Color(0.9f, 0.25f, 0.25f);
        }

        /// <summary>Draws the definition's fields against the SerializedObject's own buffer.
        /// Deliberately never calls Update()/ApplyModifiedProperties(): Update() would wipe
        /// edits the user has not applied yet, so the caller decides when to load and commit.
        /// Returns whether anything was edited this event.</summary>
        private bool DrawFields(SerializedObject serialized)
        {
            var preset = (UIViewPreset)serialized.FindProperty("preset").enumValueIndex;
            bool changed = false;

            foreach (string field in UIViewDefinitionFieldVisibility.AllFields)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null || !UIViewDefinitionFieldVisibility.IsVisible(field, preset, this._showAllFields))
                    continue;

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(property, true);
                if (!EditorGUI.EndChangeCheck())
                    continue;

                changed = true;
                if (field == "viewPrefab")
                    AutoFillViewModel(serialized);
            }

            return changed;
        }

        /// <summary>The view already names its view model (UIView&lt;TVM&gt;), so the key never
        /// has to be picked by hand or can drift from the prefab.</summary>
        private static void AutoFillViewModel(SerializedObject serialized)
        {
            var view = serialized.FindProperty("viewPrefab").objectReferenceValue as UIViewBase;
            if (view == null || !UIRegistryValidator.TryGetViewModelType(view.GetType(), out Type vmType))
                return;

            serialized.FindProperty("viewModelType").FindPropertyRelative("assemblyQualifiedName").stringValue =
                vmType.AssemblyQualifiedName;
        }

        private static void ApplyPresetDefaults(SerializedObject serialized)
        {
            var preset = (UIViewPreset)serialized.FindProperty("preset").enumValueIndex;
            UIViewDefinitionFieldVisibility.ApplyPresetDefaults(serialized, preset);
        }
    }
}