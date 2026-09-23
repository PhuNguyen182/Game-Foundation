using System.Collections.Generic;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using DracoRuan.PrebuildServices.MobileVibration.Editor.Drawing;
using DracoRuan.PrebuildServices.MobileVibration.Logic;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor
{
    /// <summary>
    /// Creates, edits and inspects vibration entries, and generates the <c>VibrationId</c> class.
    /// </summary>
    /// <remarks>
    /// Every field of the selected entry is drawn by <see cref="VibrationEntryDrawer"/> through plain
    /// <see cref="SerializedProperty"/> calls, not Odin. A hand-driven Odin <c>PropertyTree</c> used to
    /// draw this pane; it could not lay out <c>HapticCurve</c>'s nested iOS/Android structs behind a
    /// <c>[ShowIf]</c> fade group without blank gaps, vanishing fields or false validation errors, and
    /// several targeted fixes against Odin's fade-group internals failed or regressed further. This
    /// window was rebuilt to stop relying on that mechanism entirely.
    /// </remarks>
    public sealed class VibrationManagerWindow : EditorWindow
    {
        private const float ListWidth = 260f;
        private const float SplitterWidth = 4f;
        private const int EntriesPerPage = 20;

        private static readonly Color StaleColor = new Color(0.95f, 0.78f, 0.35f, 1f);
        private static readonly Color SelectedRowColor = new Color(0.24f, 0.36f, 0.52f, 0.5f);
        private static readonly Color WarningColor = new Color(0.95f, 0.55f, 0.2f, 1f);
        private static readonly Color SplitterColor = new Color(0.14f, 0.14f, 0.14f, 1f);

        private enum FormMode
        {
            None,
            Create,
            Duplicate,
            Rename
        }

        private readonly List<VibrationEntry> _entries = new List<VibrationEntry>();
        private readonly List<VibrationEntry> _visibleEntries = new List<VibrationEntry>();

        private VibrationEntry _selected;
        private VibrationEntryDrawer _drawer;
        private SerializedObject _selectedObject;

        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private int _currentPage;

        private FormMode _formMode = FormMode.None;
        private string _formId = string.Empty;
        private string _lastCheckedId;
        private VibrationIdCheck _formCheck;

        [MenuItem("Tools/Foundations/Vibration Editor/Vibration Manager", false, 104)]
        public static void ShowWindow()
        {
            VibrationManagerWindow window = GetWindow<VibrationManagerWindow>();
            window.titleContent = new GUIContent("📳 Vibration Manager");
            window.minSize = new Vector2(680f, 420f);
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

        /// <summary>Selects an entry in an already-open window. Used by the Inspector's shortcut button.</summary>
        public static void Select(VibrationEntry entry)
        {
            VibrationManagerWindow window = GetWindow<VibrationManagerWindow>();
            window.SelectEntry(entry);
            window.Repaint();
        }

        private void OnEnable()
        {
            this.Rescan();
            this.RestoreSelection();
        }

        private void OnGUI()
        {
            this.DrawToolbar();
            this.DrawBanners();

            using (new EditorGUILayout.HorizontalScope())
            {
                this.DrawList();
                this.DrawSplitter();
                this.DrawDetail();
            }
        }

        #region Toolbar and banners

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    this.Rescan();

                if (GUILayout.Button("New Entry", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    this.BeginForm(FormMode.Create, string.Empty);

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

        private int PageCount => Mathf.Max(1, Mathf.CeilToInt(this._visibleEntries.Count / (float)EntriesPerPage));

        private void DrawSplitter()
        {
            Rect splitter = EditorGUILayout.GetControlRect(false,
                SplitterWidth, GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(splitter, SplitterColor);
        }

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                this._listScroll = EditorGUILayout.BeginScrollView(this._listScroll);

                int start = this._currentPage * EntriesPerPage;
                int count = Mathf.Min(EntriesPerPage, this._visibleEntries.Count - start);

                for (int i = 0; i < count; i++)
                    this.DrawRow(this._visibleEntries[start + i]);

                EditorGUILayout.EndScrollView();

                this.DrawPager();

                EditorGUILayout.LabelField($"{this._entries.Count} entr(ies)", EditorStyles.miniLabel);
            }
        }

        private void DrawPager()
        {
            if (this.PageCount <= 1)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(this._currentPage <= 0))
                {
                    if (GUILayout.Button("◀ Prev", EditorStyles.miniButtonLeft))
                        this._currentPage--;
                }

                GUILayout.Label($"Page {this._currentPage + 1} / {this.PageCount}",
                    EditorStyles.centeredGreyMiniLabel);

                using (new EditorGUI.DisabledScope(this._currentPage >= this.PageCount - 1))
                {
                    if (GUILayout.Button("Next ▶", EditorStyles.miniButtonRight))
                        this._currentPage++;
                }
            }
        }

        private void DrawRow(VibrationEntry entry)
        {
            bool isSelected = ReferenceEquals(entry, this._selected);
            string id = string.IsNullOrEmpty(entry.Id) ? "(no id)" : entry.Id;
            string modeTag = ModeTag(entry.SourceMode);
            string duration = DurationLabel(entry);
            bool hasWarning = !entry.IsPlayable(out _) || !this.IsRegistered(entry);

            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2f + 6f);

            if (Event.current.type == EventType.Repaint && isSelected)
                EditorGUI.DrawRect(rect, SelectedRowColor);

            Rect idRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, EditorGUIUtility.singleLineHeight);
            Rect infoRect = new Rect(rect.x + 4f, idRect.yMax, rect.width - 8f, EditorGUIUtility.singleLineHeight);

            GUI.Label(idRect, id, isSelected ? EditorStyles.whiteLabel : EditorStyles.label);

            string info = string.IsNullOrEmpty(duration) ? modeTag : $"{modeTag} · {duration}";
            if (hasWarning)
                info += " ⚠";

            Color previousColor = GUI.contentColor;
            GUI.contentColor = hasWarning ? WarningColor : new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(infoRect, info, EditorStyles.miniLabel);
            GUI.contentColor = previousColor;

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 1)
                    this.ShowRowContextMenu(entry);
                else
                    this.SelectEntry(entry);

                Event.current.Use();
            }
        }

        private void ShowRowContextMenu(VibrationEntry entry)
        {
            this.SelectEntry(entry);

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(entry));
            menu.AddItem(new GUIContent("Rename"), false,
                () => this.BeginForm(FormMode.Rename, entry.Id));
            menu.AddItem(new GUIContent("Duplicate"), false,
                () => this.BeginForm(FormMode.Duplicate, $"{entry.Id}_Copy"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, () => this.DeleteEntry(entry));
            menu.ShowAsContext();
        }

        private static string ModeTag(VibrationSourceMode mode) => mode switch
        {
            VibrationSourceMode.Preset => "Preset",
            VibrationSourceMode.CustomPattern => "Pattern",
            VibrationSourceMode.Curve => "Curve",
            _ => mode.ToString()
        };

        private static string DurationLabel(VibrationEntry entry)
        {
            switch (entry.SourceMode)
            {
                case VibrationSourceMode.CustomPattern:
                    float patternMs = Mathf.Max(
                        entry.CustomPattern.IOSDuration(), entry.CustomPattern.AndroidDuration()) * 1000f;
                    return patternMs > 0f ? $"{patternMs:0} ms" : null;

                case VibrationSourceMode.Curve:
                    float curveMs = Mathf.Max(
                        entry.Curve.IOS_HapticCurve.GetDuration(),
                        entry.Curve.Android_HapticCurve.GetDuration()) * 1000f;
                    return curveMs > 0f ? $"{curveMs:0} ms" : null;

                default:
                    return null;
            }
        }

        private bool IsRegistered(VibrationEntry entry)
        {
            VibrationDatabaseLocator.Result database = VibrationDatabaseLocator.Find();
            return database.Found && database.Collection.Contains(entry);
        }

        #endregion

        #region Detail

        private void DrawDetail()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                this._detailScroll = EditorGUILayout.BeginScrollView(this._detailScroll);

                if (this._formMode != FormMode.None)
                    this.DrawForm();
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

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(this._selected.Id, EditorStyles.boldLabel);

                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawActionButton("d_Search Icon", "Ping", "Highlight the asset in the Project window."))
                        EditorGUIUtility.PingObject(this._selected);

                    if (DrawActionButton("d_editicon.sml", "Rename", "Change this entry's identifier."))
                        this.BeginForm(FormMode.Rename, this._selected.Id);

                    if (DrawActionButton("d_TreeEditor.Duplicate", "Duplicate", "Copy this entry under a new id."))
                        this.BeginForm(FormMode.Duplicate, $"{this._selected.Id}_Copy");

                    Color previousColor = GUI.color;
                    GUI.color = new Color(1f, 0.55f, 0.55f, 1f);
                    if (DrawActionButton("d_TreeEditor.Trash", "Delete", "Delete this entry and its identifier."))
                        this.DeleteEntry(this._selected);
                    GUI.color = previousColor;
                }
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

            this._drawer ??= new VibrationEntryDrawer();
            this._drawer.Draw(this._selectedObject);
        }

        #endregion

        #region Create / Rename / Duplicate form

        private void BeginForm(FormMode mode, string initialId)
        {
            this._formMode = mode;
            this._formId = initialId ?? string.Empty;
            this._lastCheckedId = null;
        }

        private void DrawForm()
        {
            string title = this._formMode switch
            {
                FormMode.Create => "New vibration entry",
                FormMode.Duplicate => $"Duplicate '{this._selected.Id}'",
                FormMode.Rename => $"Rename '{this._selected.Id}'",
                _ => string.Empty
            };

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            this._formId = EditorGUILayout.TextField("Identifier", this._formId);

            string ignoreOwnerPath = this._formMode == FormMode.Rename
                ? AssetDatabase.GetAssetPath(this._selected)
                : null;

            if (EditorGUI.EndChangeCheck() || this._lastCheckedId != this._formId)
            {
                this._lastCheckedId = this._formId;
                this._formCheck = VibrationIdInputValidator.Check(this._formId, ignoreOwnerPath);
            }

            if (!string.IsNullOrEmpty(this._formCheck.Message))
            {
                EditorGUILayout.HelpBox(this._formCheck.Message, this._formCheck.MessageType);

                if (!string.IsNullOrEmpty(this._formCheck.Suggestion)
                    && GUILayout.Button($"Use '{this._formCheck.Suggestion}'"))
                {
                    this._formId = this._formCheck.Suggestion;
                    this._formCheck = VibrationIdInputValidator.Check(this._formId, ignoreOwnerPath);
                    this._lastCheckedId = this._formId;
                }
            }

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel"))
                    this._formMode = FormMode.None;

                using (new EditorGUI.DisabledScope(!this._formCheck.CanSubmit))
                {
                    string submitLabel = this._formMode == FormMode.Create ? "Create…" : "Apply";
                    if (GUILayout.Button(submitLabel))
                        this.SubmitForm();
                }
            }
        }

        private void SubmitForm()
        {
            switch (this._formMode)
            {
                case FormMode.Create:
                    this.CreateEntry();
                    break;
                case FormMode.Duplicate:
                    this.DuplicateEntry();
                    break;
                case FormMode.Rename:
                    this.RenameEntry();
                    break;
            }
        }

        private void CreateEntry()
        {
            VibrationEntryCreationRequest request = new VibrationEntryCreationRequest { Id = this._formId };
            VibrationEntry created = VibrationEntryCreationService.CreateInteractive(request, out string error);

            if (created == null)
            {
                if (error != null)
                    VibrationDialogs.Report("Could not create the entry", error);
                else
                    SessionState.SetString(VibrationEditorState.LastStatusKey,
                        "Creation cancelled. Nothing was written.");

                return;
            }

            if (error != null)
                VibrationDialogs.Report("Entry created, but not registered", error);

            this._formMode = FormMode.None;
            this.Rescan();
            this.SelectEntry(created);
        }

        private void DuplicateEntry()
        {
            VibrationEntry copy = VibrationEntryDuplicationService.DuplicateInteractive(
                this._selected, this._formId, out string error);

            if (copy == null)
            {
                if (error != null)
                    VibrationDialogs.Report("Could not duplicate the entry", error);
                return;
            }

            if (error != null)
                VibrationDialogs.Report("Entry duplicated, but not fully registered", error);

            this._formMode = FormMode.None;
            this.Rescan();
            this.SelectEntry(copy);
        }

        private void RenameEntry()
        {
            bool renamed = VibrationEntryRenameService.RenameInteractive(
                this._selected, this._formId, out string error);

            if (error != null)
                VibrationDialogs.Report("Rename problem", error);

            if (!renamed)
                return;

            this._formMode = FormMode.None;
            this.Rescan();
            this.SelectEntry(this._selected);
        }

        private void DeleteEntry(VibrationEntry entry)
        {
            int deleted = VibrationEntryDeletionService.DeleteInteractive(
                new List<VibrationEntry> { entry }, out string error);

            if (error != null)
                VibrationDialogs.Report("Delete problem", error);

            if (deleted == 0)
                return;

            if (ReferenceEquals(entry, this._selected))
                this.SelectEntry(null);

            this.Rescan();
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
                    || (this._entries[i].Id?.IndexOf(this._search, System.StringComparison.OrdinalIgnoreCase) ?? -1) >=
                    0)
                    this._visibleEntries.Add(this._entries[i]);
            }

            this._currentPage = Mathf.Clamp(this._currentPage, 0, this.PageCount - 1);
        }

        private void SelectEntry(VibrationEntry entry)
        {
            this._selected = entry;
            this._formMode = FormMode.None;
            this._drawer = null;
            this._selectedObject = entry != null ? new SerializedObject(entry) : null;

            string path = AssetDatabase.GetAssetPath(entry);
            EditorPrefs.SetString(VibrationEditorState.SelectedGuidKey, AssetDatabase.AssetPathToGUID(path));
        }

        private void RestoreSelection()
        {
            string guid = EditorPrefs.GetString(VibrationEditorState.SelectedGuidKey, null);
            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return;

            VibrationEntry entry = AssetDatabase.LoadAssetAtPath<VibrationEntry>(path);
            if (entry != null)
                this.SelectEntry(entry);
        }

        #endregion

        #region Shared drawing helpers

        /// <summary>
        /// A large entry-action button showing a built-in Unity icon next to its label, always both at
        /// once rather than a label-on-hover tooltip, per the user's request that these four buttons
        /// read clearly at a glance. Falls back to text-only if the icon name does not resolve on the
        /// running Editor's skin (built-in icon names are not a stable public API across versions).
        /// </summary>
        /// <remarks>
        /// The <see cref="GUIStyle"/> is built fresh on every call rather than cached in a
        /// <c>static</c> field: a <c>static GUIStyle</c> built from <c>EditorStyles.miniButton</c>
        /// survives a domain reload holding a reference into the old skin, and the first
        /// <c>OnGUI</c> after reload that reads a disposed native style throws mid-layout — which
        /// aborts the whole method before any field below it draws. Constructing a small
        /// <see cref="GUIStyle"/> per repaint is cheap enough that IMGUI code does it routinely.
        /// </remarks>
        private static bool DrawActionButton(string iconName, string label, string tooltip)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniButton)
            {
                fontStyle = FontStyle.Bold,
                imagePosition = ImagePosition.ImageLeft,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 10, 4, 4)
            };

            GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
            GUIContent content = iconContent.image != null
                ? new GUIContent(label, iconContent.image, tooltip)
                : new GUIContent(label, tooltip);

            return GUILayout.Button(content, style, GUILayout.Height(26f), GUILayout.MinWidth(84f));
        }

        #endregion
    }
}