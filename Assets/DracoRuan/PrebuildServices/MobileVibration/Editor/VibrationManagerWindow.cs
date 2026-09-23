using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>
    /// Creates and inspects vibration entries, and generates the <c>VibrationId</c> class.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately far smaller than <c>AudioManagerWindow</c>: there is no mixer or channel
    /// concept to give a second pane to, so this is just an entry list, a detail pane drawn through
    /// Odin's <c>PropertyTree</c> (one line, renders exactly like the Inspector, <c>[ShowIf]</c>
    /// included), a Generate Ids button and conflict/staleness banners.</para>
    /// </remarks>
    public sealed class VibrationManagerWindow : EditorWindow
    {
        private const float ListWidth = 240f;

        private static readonly Color StaleColor = new Color(0.95f, 0.78f, 0.35f, 1f);
        private static readonly Color SelectedRowColor = new Color(0.24f, 0.36f, 0.52f, 0.5f);

        private readonly List<VibrationEntry> _entries = new List<VibrationEntry>();
        private readonly List<VibrationEntry> _visibleEntries = new List<VibrationEntry>();

        private PropertyTree _detailTree;
        private SerializedObject _curveObject;
        private VibrationEntry _selected;

        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;

        private bool _isCreating;
        private string _newId = string.Empty;
        private string _newIdFeedback;
        private MessageType _newIdFeedbackType = MessageType.None;
        private string _lastCheckedId;

        [MenuItem("Tools/Foundations/Vibration Editor/Vibration Manager", false, 104)]
        public static void ShowWindow()
        {
            VibrationManagerWindow window = GetWindow<VibrationManagerWindow>();
            window.titleContent = new GUIContent("📳 Vibration Manager");
            window.minSize = new Vector2(620f, 380f);
            window.Show();
        }

        [MenuItem("Tools/Foundations/Vibration Editor/Regenerate Vibration Ids", false, 105)]
        public static void RegenerateFromMenu()
        {
            VibrationIdIndex.Invalidate();
            VibrationIdGenerationPlan plan = VibrationIdGenerationService.BuildPlan();

            if (!plan.CanApply)
            {
                VibrationDialogs.Report("Cannot generate vibration ids", plan.BlockingError);
                return;
            }

            if (!plan.IsNoOp && !VibrationDialogs.ConfirmGenerate(plan))
                return;

            if (!VibrationIdGenerationService.Apply(plan, out string error))
                VibrationDialogs.Report("Cannot generate vibration ids", error);
        }

        private void OnEnable()
        {
            this.Rescan();
            this.RestoreSelection();
        }

        private void OnDisable() => this.ReleaseDetailTree();

        private void OnGUI()
        {
            // DrawDetail is deliberately outside this scope: it is the only place this window draws
            // an Odin PropertyTree, and VibrationEntry's [ShowIf] groups (Preset/CustomPattern/Curve)
            // rely on SirenixEditorGUI's fade groups. A zeroed duration drives their internal AnimBool
            // speed to infinity, and on the first draw after the mode changes or a list entry is added,
            // that multiplies against a zero elapsed time and produces NaN heights — which is what
            // showed up as the Custom Pattern foldout rendering with no children and the panel
            // collapsing narrow. The toolbar, banners and list never draw Odin content, so they keep
            // the zero-duration perf fix with no such risk. Same fix as
            // AudioManagerWindow.NoFoldoutAnimationScope.
            using (NoFoldoutAnimationScope.Enter())
            {
                this.DrawToolbar();
                this.DrawBanners();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (NoFoldoutAnimationScope.Enter())
                    this.DrawList();

                this.DrawDetail();
            }
        }

        /// <summary>
        /// Zeroes Odin's global fade-group duration for one <see cref="OnGUI"/> call and puts it back.
        /// The static is shared with every Odin-drawn window, so it cannot just be set once.
        /// </summary>
        private readonly struct NoFoldoutAnimationScope : IDisposable
        {
            private readonly float _previousDuration;

            private NoFoldoutAnimationScope(float previousDuration) => this._previousDuration = previousDuration;

            public static NoFoldoutAnimationScope Enter()
            {
                float previous = SirenixEditorGUI.DefaultFadeGroupDuration;
                SirenixEditorGUI.DefaultFadeGroupDuration = 0f;
                return new NoFoldoutAnimationScope(previous);
            }

            public void Dispose() => SirenixEditorGUI.DefaultFadeGroupDuration = this._previousDuration;
        }

        #region Toolbar and banners

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    this.Rescan();

                if (GUILayout.Button("New Entry", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    this.BeginCreate();

                bool stale = SessionState.GetBool(VibrationEditorState.IdsStaleKey, false);
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = stale ? StaleColor : previous;

                if (GUILayout.Button(stale ? "Generate Ids *" : "Generate Ids", EditorStyles.toolbarButton,
                        GUILayout.Width(110f)))
                    this.Generate();

                GUI.backgroundColor = previous;

                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                this._search = EditorGUILayout.TextField(this._search, EditorStyles.toolbarSearchField,
                    GUILayout.Width(160f));
                if (EditorGUI.EndChangeCheck())
                    this.RebuildVisible();
            }
        }

        private void DrawBanners()
        {
            IReadOnlyList<VibrationIdConflict> conflicts = VibrationIdIndex.Conflicts;

            for (int i = 0; i < conflicts.Count; i++)
                EditorGUILayout.HelpBox(conflicts[i].Message, MessageType.Error);

            if (conflicts.Count == 0 && SessionState.GetBool(VibrationEditorState.IdsStaleKey, false))
                EditorGUILayout.HelpBox("VibrationId.cs no longer matches the project. Press Generate Ids.",
                    MessageType.Warning);
        }

        #endregion

        #region List

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                this._listScroll = EditorGUILayout.BeginScrollView(this._listScroll);

                for (int i = 0; i < this._visibleEntries.Count; i++)
                    this.DrawRow(this._visibleEntries[i]);

                EditorGUILayout.EndScrollView();

                EditorGUILayout.LabelField($"{this._entries.Count} entr(ies)", EditorStyles.miniLabel);
            }
        }

        private void DrawRow(VibrationEntry entry)
        {
            bool isSelected = ReferenceEquals(entry, this._selected);
            string label = string.IsNullOrEmpty(entry.Id) ? "(no id)" : entry.Id;

            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);

            if (Event.current.type == EventType.Repaint && isSelected)
                EditorGUI.DrawRect(rect, SelectedRowColor);

            GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, rect.height), label,
                isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                this.Select(entry);
                Event.current.Use();
            }
        }

        #endregion

        #region Detail

        private void DrawDetail()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                this._detailScroll = EditorGUILayout.BeginScrollView(this._detailScroll);

                if (this._isCreating)
                    this.DrawCreateForm();
                else
                    this.DrawSelectedEntry();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedEntry()
        {
            if (this._selected == null)
            {
                EditorGUILayout.HelpBox("Select an entry, or press New Entry.", MessageType.Info);
                return;
            }

            string path = AssetDatabase.GetAssetPath(this._selected);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(this._selected.Id, EditorStyles.boldLabel);

                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(50f)))
                    EditorGUIUtility.PingObject(this._selected);
            }

            EditorGUILayout.LabelField(path, EditorStyles.miniLabel);

            if (!this._selected.IsPlayable(out string reason))
                EditorGUILayout.HelpBox(reason, MessageType.Warning);

            VibrationDatabaseLocator.Result database = VibrationDatabaseLocator.Find();
            if (database.Found && !database.Collection.Contains(this._selected))
            {
                EditorGUILayout.HelpBox("This entry is not registered in the VibrationCollection, so the "
                                        + "game will not load it. Its identifier is still generated.",
                    MessageType.Warning);

                if (GUILayout.Button("Add to collection"))
                {
                    VibrationDatabaseLocator.Register(database.Collection, this._selected, out string error);
                    if (error != null)
                        VibrationDialogs.Report("Could not register the entry", error);
                }
            }

            EditorGUILayout.Space(6f);

            // One line renders the whole entry exactly as the Inspector would, ShowIf included.
            // _curve is the one exception: see VibrationEntry._curve's comment for why it carries
            // [HideInInspector] and is drawn separately below instead.
            this._detailTree ??= PropertyTree.Create(this._selected);
            this._detailTree.Draw(false);
            this._detailTree.ApplyChanges();

            if (this._selected.SourceMode == VibrationSourceMode.Curve)
                this.DrawCurveField();
        }

        /// <summary>
        /// Draws <see cref="VibrationEntry"/>'s <c>_curve</c> field through a plain
        /// <see cref="SerializedProperty"/> instead of Odin, using Unity's own IMGUI foldouts for the
        /// nested <c>IOS_HapticCurve</c>/<c>Android_HapticCurve</c> structs.
        /// </summary>
        /// <remarks>
        /// This is a deliberate side-step, not a stopgap: a manually created Odin
        /// <see cref="PropertyTree"/> (as <see cref="_detailTree"/> is) could not be made to lay out
        /// this field correctly. <c>_curve</c> sits behind a <c>[ShowIf]</c> fade group, and
        /// <c>HapticCurve</c> itself contains two more plain-struct foldouts - a fade group nested
        /// inside a fade group. Every angle tried against that shape failed differently: the
        /// default (nonzero) fade duration settled on a wildly oversized height, a near-zero forced
        /// duration produced fields with no rect until a second, unrelated repaint forced Odin to
        /// catch up, and rebuilding the <see cref="PropertyTree"/> from scratch every frame (to sidestep
        /// whatever internal state Odin was failing to reconcile once the two child foldouts were left
        /// open/closed differently from each other) blanked the entire detail pane outright. Unity's
        /// own <c>EditorGUILayout.PropertyField</c> foldout for a
        /// plain <c>[Serializable]</c> struct has no animation and no such failure mode, so this
        /// field is drawn through it instead of continuing to chase the underlying Odin defect.
        /// </remarks>
        private void DrawCurveField()
        {
            this._curveObject ??= new SerializedObject(this._selected);
            this._curveObject.Update();

            SerializedProperty curveProperty = this._curveObject.FindProperty("_curve");
            EditorGUILayout.PropertyField(curveProperty, new GUIContent("Curve"), true);

            this._curveObject.ApplyModifiedProperties();
        }

        #endregion

        #region Create form

        private void BeginCreate()
        {
            this._isCreating = true;
            this._newId = string.Empty;
            this._newIdFeedback = null;
            this._lastCheckedId = null;
        }

        private void DrawCreateForm()
        {
            EditorGUILayout.LabelField("New vibration entry", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            this._newId = EditorGUILayout.TextField("Identifier", this._newId);

            // Recomputed only when the text changed, so repaint stays free.
            if (EditorGUI.EndChangeCheck() || this._lastCheckedId != this._newId)
                this.RecheckNewId();

            if (!string.IsNullOrEmpty(this._newIdFeedback))
            {
                EditorGUILayout.HelpBox(this._newIdFeedback, this._newIdFeedbackType);

                string suggestion = VibrationIdSanitizer.Suggest(this._newId);
                bool canSuggest = this._newIdFeedbackType == MessageType.Error
                                  && !string.IsNullOrEmpty(suggestion)
                                  && suggestion != this._newId
                                  && VibrationIdFormat.IsValid(suggestion, out _);

                if (canSuggest && GUILayout.Button($"Use '{suggestion}'"))
                {
                    this._newId = suggestion;
                    this.RecheckNewId();
                }
            }

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel"))
                    this._isCreating = false;

                using (new EditorGUI.DisabledScope(this._newIdFeedbackType == MessageType.Error
                                                   || string.IsNullOrEmpty(this._newId)))
                {
                    // The ellipsis signals that a file dialog follows.
                    if (GUILayout.Button("Create…"))
                        this.CreateEntry();
                }
            }
        }

        private void RecheckNewId()
        {
            this._lastCheckedId = this._newId;

            if (string.IsNullOrEmpty(this._newId))
            {
                this._newIdFeedback = null;
                this._newIdFeedbackType = MessageType.None;
                return;
            }

            VibrationIdSanitizeResult sanitized = VibrationIdSanitizer.ToMemberName(this._newId);

            if (sanitized.Status == VibrationIdStatus.Rejected)
            {
                this._newIdFeedback = sanitized.Message;
                this._newIdFeedbackType = MessageType.Error;
                return;
            }

            VibrationIdConflict? conflict = VibrationIdCollisionDetector.CheckAgainst(
                this._newId, VibrationIdIndex.Entries, ignoreOwnerPath: null);

            if (conflict.HasValue)
            {
                this._newIdFeedback = conflict.Value.Message;
                this._newIdFeedbackType = MessageType.Error;
                return;
            }

            this._newIdFeedback = sanitized.Status == VibrationIdStatus.Adjusted
                ? sanitized.Message
                : $"Available. Generates as VibrationId.{sanitized.MemberName}";

            this._newIdFeedbackType = sanitized.Status == VibrationIdStatus.Adjusted
                ? MessageType.Warning
                : MessageType.Info;
        }

        private void CreateEntry()
        {
            VibrationEntryCreationRequest request = new VibrationEntryCreationRequest { Id = this._newId };
            VibrationEntry created = VibrationEntryCreationService.CreateInteractive(request, out string error);

            if (created == null)
            {
                // A cancel reports no error and must leave the form exactly as it was, so nothing has
                // to be retyped after a mis-click.
                if (error != null)
                    VibrationDialogs.Report("Could not create the entry", error);
                else
                    SessionState.SetString(VibrationEditorState.LastStatusKey,
                        "Creation cancelled. Nothing was written.");

                return;
            }

            if (error != null)
                VibrationDialogs.Report("Entry created, but not registered", error);

            this._isCreating = false;
            this.Rescan();
            this.Select(created);
            this.Generate();
        }

        #endregion

        #region Generate and state

        private void Generate()
        {
            VibrationIdIndex.Invalidate();
            VibrationIdGenerationPlan plan = VibrationIdGenerationService.BuildPlan();

            if (!plan.CanApply)
            {
                VibrationDialogs.Report("Cannot generate vibration ids", plan.BlockingError);
                return;
            }

            if (!plan.IsNoOp && !VibrationDialogs.ConfirmGenerate(plan))
                return;

            if (!VibrationIdGenerationService.Apply(plan, out string error))
                VibrationDialogs.Report("Cannot generate vibration ids", error);
        }

        private void Rescan()
        {
            VibrationIdIndex.Invalidate();

            this._entries.Clear();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(VibrationEntry)}");

            for (int i = 0; i < guids.Length; i++)
            {
                VibrationEntry entry =
                    AssetDatabase.LoadAssetAtPath<VibrationEntry>(AssetDatabase.GUIDToAssetPath(guids[i]));

                if (entry != null)
                    this._entries.Add(entry);
            }

            this._entries.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));

            this.RebuildVisible();
        }

        private void RebuildVisible()
        {
            this._visibleEntries.Clear();

            for (int i = 0; i < this._entries.Count; i++)
            {
                if (string.IsNullOrEmpty(this._search)
                    || (this._entries[i].Id?.IndexOf(this._search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    this._visibleEntries.Add(this._entries[i]);
            }
        }

        private void Select(VibrationEntry entry)
        {
            this._selected = entry;
            this._isCreating = false;
            this.ReleaseDetailTree();

            // Stored as a GUID, not a path, so the selection survives the asset being moved.
            string path = AssetDatabase.GetAssetPath(entry);
            EditorPrefs.SetString(VibrationEditorState.SelectedGuidKey, AssetDatabase.AssetPathToGUID(path));
        }

        private void RestoreSelection()
        {
            string guid = EditorPrefs.GetString(VibrationEditorState.SelectedGuidKey, null);
            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                this._selected = AssetDatabase.LoadAssetAtPath<VibrationEntry>(path);
        }

        private void ReleaseDetailTree()
        {
            this._detailTree?.Dispose();
            this._detailTree = null;
            this._curveObject = null;
        }

        #endregion
    }
}