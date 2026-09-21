using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.StaticData;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Editor window for inspecting and editing the fallback chain of every static config table.
    ///
    /// <para>
    /// Master–detail: tables on the left, the selected table's chain on the right. Applying writes
    /// the chain back into the controller's own <c>.cs</c> file and lets Unity recompile.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para><b>Why it edits source rather than an asset.</b> The chain is the controller's
    /// <c>Sources</c> property, so it belongs in the controller — it is reviewed in a pull request,
    /// it is versioned with the code that depends on it, and there is exactly one place it is
    /// written down. An override asset would be faster to change but would mean the chain a
    /// developer reads in the file is not necessarily the chain that runs.</para>
    ///
    /// <para><b>Edit mode only.</b> Nothing here resolves a controller or a container: a chain is
    /// read out of source text and written back to it. Showing which source actually won at runtime
    /// is a different job, and it needs a live container, so it is deliberately not in this
    /// window.</para>
    ///
    /// <para><b>The risky part is not here.</b> All parsing and rewriting lives in
    /// <see cref="StaticDataChainSource"/>, which is covered by tests, because this is a tool that
    /// overwrites files a person wrote by hand.</para>
    /// </remarks>
    public sealed class StaticDataTool : EditorWindow
    {
        private const string SelectedDataIdPrefsKey = "DracoRuan.StaticDataTool.SelectedDataId";
        private const string SplitWidthPrefsKey = "DracoRuan.StaticDataTool.SplitWidth";

        private const float MinListWidth = 200f;
        private const float MaxListWidth = 460f;
        private const float SplitterWidth = 6f;
        private const float EntryRowHeight = 44f;
        private const int EntriesPerPage = 10;

        private static readonly Color SplitterColor = new(0.13f, 0.13f, 0.13f, 1f);
        private static readonly Color SplitterHoverColor = new(0.24f, 0.48f, 0.90f, 0.9f);
        private static readonly Color SelectedRowColor = new(0.24f, 0.48f, 0.90f, 0.28f);
        private static readonly Color AlternateRowColor = new(0f, 0f, 0f, 0.06f);

        private static readonly Color ApplyAllColor = new(0.45f, 0.78f, 0.55f, 1f);
        private static readonly Color NeutralButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color RevertColor = new(0.95f, 0.45f, 0.45f, 1f);
        private static readonly Color AccentColor = new(0.45f, 0.68f, 0.98f, 1f);

        private readonly List<StaticDataEntry> _entries = new();
        private readonly List<StaticDataEntry> _visibleEntries = new();

        private StaticDataEntry _selected;
        private string _searchFilter = string.Empty;
        private string _statusText = "Ready";

        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private float _listWidth = 240f;
        private bool _isDraggingSplitter;
        private bool _isHoveringSplitter;
        private int _currentPage;

        /// <summary>
        /// Repaints are coalesced to one per editor frame, as in <see cref="LocalDataTool"/> —
        /// nothing calls <see cref="EditorWindow.Repaint"/> directly.
        /// </summary>
        private bool _needsRepaint;

        private int _dirtyCountCache;
        private bool _dirtyCountDirty = true;

        [MenuItem("Tools/Foundations/Static Data Editor/Static Config Data Manager", false, 101)]
        public static void ShowWindow()
        {
            StaticDataTool window = GetWindow<StaticDataTool>();
            window.titleContent = new GUIContent("⚙️ Static Config Data Manager");
            window.minSize = new Vector2(Mathf.Max(760f, MinWindowWidth), 440);
            window.Show();
        }

        private void OnEnable()
        {
            this._listWidth = EditorPrefs.GetFloat(SplitWidthPrefsKey, 240f);

            this.Rescan();
            this.RestoreSelection();
        }

        private void OnDisable() => EditorPrefs.SetFloat(SplitWidthPrefsKey, this._listWidth);

        private void OnGUI()
        {
            using (NoFoldoutAnimationScope.Enter())
            {
                this.DrawToolbar();

                using (new EditorGUILayout.HorizontalScope())
                {
                    this.DrawTableList();
                    this.DrawSplitter();
                    this.DrawDetail();
                }

                this.DrawStatusBar();
            }

            if (!this._needsRepaint)
                return;

            this._needsRepaint = false;
            this.Repaint();
        }

        /// <summary>
        /// Zeroes Odin's global fade-group duration for the scope of one <see cref="OnGUI"/> call,
        /// then restores whatever it was. Copied from <see cref="LocalDataTool"/>, where the reason
        /// is documented at length: the static is shared with every Odin-drawn window in the editor,
        /// so it has to be put back rather than set once.
        /// </summary>
        private readonly struct NoFoldoutAnimationScope : IDisposable
        {
            private readonly float _previousDuration;

            private NoFoldoutAnimationScope(float previousDuration)
            {
                this._previousDuration = previousDuration;
            }

            public static NoFoldoutAnimationScope Enter()
            {
                float previous = SirenixEditorGUI.DefaultFadeGroupDuration;
                SirenixEditorGUI.DefaultFadeGroupDuration = 0f;
                return new NoFoldoutAnimationScope(previous);
            }

            public void Dispose() => SirenixEditorGUI.DefaultFadeGroupDuration = this._previousDuration;
        }

        // -----------------------------------------------------------------
        // Toolbar
        // -----------------------------------------------------------------

        private const float ToolbarHeight = 34f;
        private const float ToolbarControlHeight = 24f;

        private static readonly Color ToolbarBackgroundColor = new(0.19f, 0.19f, 0.19f, 1f);

        private GUIStyle _searchFieldStyle;

        private GUIStyle SearchFieldStyle => this._searchFieldStyle ??= new GUIStyle(EditorStyles.textField)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(20, 6, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
        };

        /// <summary>Kept as an un-collapsed sum so each term maps to one button width or gap below.</summary>
        private const float ToolbarButtonsWidth =
            6f + 90f + 6f + 104f + 12f + 12f + 110f + 6f;

        private const float MinSearchWidth = 120f;
        private const float MaxSearchWidth = 420f;
        private const float MinWindowWidth = ToolbarButtonsWidth + MinSearchWidth;

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(ToolbarHeight)))
            {
                Rect barRect = GUILayoutUtility.GetRect(0f, ToolbarHeight, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(barRect, ToolbarBackgroundColor);

                float searchWidth = Mathf.Clamp(barRect.width - ToolbarButtonsWidth, MinSearchWidth, MaxSearchWidth);

                float centerY = barRect.y + barRect.height * 0.5f;
                float cursorX = barRect.x + 6f;

                cursorX = DrawCommandButtonAt(cursorX, centerY, "🔄 Refresh", NeutralButtonColor, 90f, this.Rescan);
                cursorX += 6f;
                DrawCommandButtonAt(cursorX, centerY, "💾 Apply All", ApplyAllColor, 104f, this.ApplyAll);

                float revertWidth = 110f;
                float rightEdge = barRect.xMax - 6f;
                float revertX = rightEdge - revertWidth;
                DrawCommandButtonAt(revertX, centerY, "↩ Revert All", RevertColor, revertWidth, this.RevertAll);

                float searchX = revertX - 12f - searchWidth;
                this.DrawSearchFieldAt(searchX, centerY, searchWidth);
            }
        }

        private static float DrawCommandButtonAt(
            float x, float centerY, string label, Color tint, float width, Action clicked)
        {
            Rect rect = new(x, centerY - ToolbarControlHeight * 0.5f, width, ToolbarControlHeight);

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = tint;

            if (GUI.Button(rect, label, StaticCommandButtonStyle))
                clicked();

            GUI.backgroundColor = previous;
            return x + width;
        }

        private static GUIStyle _staticCommandButtonStyle;

        private static GUIStyle StaticCommandButtonStyle => _staticCommandButtonStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        private void DrawSearchFieldAt(float x, float centerY, float width)
        {
            Rect fieldRect = new(x, centerY - ToolbarControlHeight * 0.5f, width, ToolbarControlHeight);

            EditorGUI.BeginChangeCheck();
            string newFilter = EditorGUI.TextField(fieldRect, this._searchFilter, this.SearchFieldStyle);
            if (EditorGUI.EndChangeCheck())
            {
                this._searchFilter = newFilter;
                this.ApplyFilter();
            }

            Rect iconRect = new(fieldRect.x + 4f, fieldRect.y + (fieldRect.height - 16f) * 0.5f, 16f, 16f);
            GUI.Label(iconRect, "🔍");

            if (string.IsNullOrEmpty(this._searchFilter))
            {
                Rect placeholderRect = new(fieldRect.x + 22f, fieldRect.y, fieldRect.width - 26f, fieldRect.height);
                using (new EditorGUI.DisabledScope(true))
                    GUI.Label(placeholderRect, "Search tables…", EditorStyles.label);
            }
        }

        // -----------------------------------------------------------------
        // Left pane
        // -----------------------------------------------------------------

        private GUIStyle _entryNameStyle;
        private GUIStyle _detailTitleStyle;

        private GUIStyle EntryNameStyle => this._entryNameStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
        };

        /// <summary>
        /// Built from <c>GUIStyle.none</c> rather than copy-constructed from a built-in style —
        /// see <see cref="LocalDataTool"/>, where the editor-skin reason is documented. Copying
        /// <see cref="EditorStyles.boldLabel"/> and overriding its color does not stick.
        /// </summary>
        private GUIStyle DetailTitleStyle
        {
            get
            {
                if (this._detailTitleStyle != null)
                    return this._detailTitleStyle;

                this._detailTitleStyle = new GUIStyle(GUIStyle.none)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = new GUIStyleState { textColor = Color.white },
                };

                return this._detailTitleStyle;
            }
        }

        private void DrawTableList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(this._listWidth)))
            {
                IReadOnlyList<StaticDataEntry> pageEntries = this.GetCurrentPageEntries();

                this._listScroll = EditorGUILayout.BeginScrollView(this._listScroll);

                if (this._visibleEntries.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        this._entries.Count == 0
                            ? "No static data tables found.\n\nAdd [StaticDataId(Id)] to a " +
                              "controller, then press Refresh."
                            : "No tables match the current search.",
                        MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < pageEntries.Count; i++)
                    {
                        this.DrawTableRow(pageEntries[i], isAlternate: i % 2 == 1);
                        GUILayout.Space(3f);
                    }
                }

                EditorGUILayout.EndScrollView();

                this.DrawPager();

                GUILayout.Space(2f);
                GUILayout.Label(
                    $"{this._entries.Count} table(s), {this.GetDirtyCount()} edited",
                    EditorStyles.label);
            }
        }

        private int PageCount => Mathf.Max(1, Mathf.CeilToInt(this._visibleEntries.Count / (float)EntriesPerPage));

        private IReadOnlyList<StaticDataEntry> GetCurrentPageEntries()
        {
            this._currentPage = Mathf.Clamp(this._currentPage, 0, this.PageCount - 1);

            int start = this._currentPage * EntriesPerPage;
            int count = Mathf.Min(EntriesPerPage, this._visibleEntries.Count - start);

            return count > 0 ? this._visibleEntries.GetRange(start, count) : Array.Empty<StaticDataEntry>();
        }

        private void DrawPager()
        {
            if (this.PageCount <= 1)
                return;

            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(this._currentPage <= 0))
                {
                    if (GUILayout.Button("◀ Prev", EditorStyles.miniButtonLeft, GUILayout.Height(22f)))
                    {
                        this._currentPage--;
                        this._needsRepaint = true;
                    }
                }

                GUILayout.Label($"Page {this._currentPage + 1} / {this.PageCount}",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Width(84f));

                using (new EditorGUI.DisabledScope(this._currentPage >= this.PageCount - 1))
                {
                    if (GUILayout.Button("Next ▶", EditorStyles.miniButtonRight, GUILayout.Height(22f)))
                    {
                        this._currentPage++;
                        this._needsRepaint = true;
                    }
                }
            }
        }

        private void DrawTableRow(StaticDataEntry entry, bool isAlternate)
        {
            bool isSelected = ReferenceEquals(entry, this._selected);

            Rect rect = EditorGUILayout.GetControlRect(false, EntryRowHeight);
            if (Event.current.type == EventType.Repaint)
            {
                if (isSelected)
                    EditorGUI.DrawRect(rect, SelectedRowColor);
                else if (isAlternate)
                    EditorGUI.DrawRect(rect, AlternateRowColor);
            }

            (string glyph, Color glyphColor, string tooltip) = GetStateGlyph(entry);

            Rect markerRect = new(rect.x + 8f, rect.y + rect.height * 0.5f - 10f, 20f, 20f);
            Color previousColor = GUI.color;
            GUI.color = glyphColor;
            GUI.Label(markerRect, new GUIContent(glyph, tooltip), EditorStyles.boldLabel);
            GUI.color = previousColor;

            Rect nameRect = new(rect.x + 32f, rect.y + 5f, rect.width - 38f, 20f);
            GUI.Label(nameRect, entry.DisplayName, this.EntryNameStyle);

            Rect subRect = new(rect.x + 32f, rect.y + 24f, rect.width - 38f, 16f);

            string subtitle = entry.IsEditable
                ? $"{entry.DataId}   ·   {CountEnabled(entry)} source(s)"
                : $"{entry.DataId}   ·   not editable";
            GUI.Label(subRect, subtitle, EditorStyles.label);

            if (Event.current.type == EventType.Repaint)
            {
                Rect underline = new(rect.x, rect.yMax - 1f, rect.width, 1f);
                EditorGUI.DrawRect(underline, new Color(0f, 0f, 0f, 0.12f));
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                this.Select(entry);
                Event.current.Use();
            }
        }

        private static int CountEnabled(StaticDataEntry entry)
        {
            int count = 0;

            foreach (StaticDataChainStep step in entry.Steps)
            {
                if (step.IsEnabled)
                    count++;
            }

            return count;
        }

        private static (string glyph, Color color, string tooltip) GetStateGlyph(StaticDataEntry entry)
        {
            if (!entry.IsEditable)
                return ("🔴", RevertColor, entry.ParseError);

            if (entry.IsDirty)
                return ("🟡", AccentColor, "Edited - not applied yet");

            return ("🟢", ApplyAllColor, "Matches the source file");
        }

        private void DrawSplitter()
        {
            Rect splitter = EditorGUILayout.GetControlRect(false,
                GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));

            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);

            bool isHovering = splitter.Contains(Event.current.mousePosition);
            if (isHovering != this._isHoveringSplitter)
            {
                this._isHoveringSplitter = isHovering;
                this._needsRepaint = true;
            }

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(splitter, SplitterColor);

                bool highlight = this._isHoveringSplitter || this._isDraggingSplitter;
                if (highlight)
                {
                    Rect accent = new(splitter.x + splitter.width * 0.5f - 1f, splitter.y, 2f, splitter.height);
                    EditorGUI.DrawRect(accent, SplitterHoverColor);
                }
            }

            if (Event.current.type == EventType.MouseDown && splitter.Contains(Event.current.mousePosition))
                this._isDraggingSplitter = true;

            if (Event.current.type == EventType.MouseUp)
                this._isDraggingSplitter = false;

            if (!this._isDraggingSplitter || Event.current.type != EventType.MouseDrag)
                return;

            this._listWidth = Mathf.Clamp(Event.current.mousePosition.x, MinListWidth, MaxListWidth);
            this._needsRepaint = true;
        }

        // -----------------------------------------------------------------
        // Right pane
        // -----------------------------------------------------------------

        private void DrawDetail()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (this._selected == null)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Select a table to inspect its fallback chain.",
                        EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                    return;
                }

                this.DrawDetailHeader(this._selected);

                this._detailScroll = EditorGUILayout.BeginScrollView(this._detailScroll);

                if (this._selected.IsEditable)
                {
                    this.DrawChainEditor(this._selected);
                    this.DrawPreview(this._selected);
                }
                else
                {
                    EditorGUILayout.HelpBox(this._selected.ParseError, MessageType.Warning);

                    if (!string.IsNullOrEmpty(this._selected.ScriptPath))
                        this.DrawOpenScriptButton(this._selected);
                }

                EditorGUILayout.EndScrollView();

                this.DrawDetailActions(this._selected);
            }
        }

        private void DrawDetailHeader(StaticDataEntry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Color previousContentColor = GUI.contentColor;
                GUI.contentColor = Color.white;
                GUILayout.Label(entry.DisplayName, this.DetailTitleStyle);
                GUI.contentColor = previousContentColor;

                GUILayout.Label(entry.DataId, EditorStyles.label);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(entry.ScriptPath ?? "script not found", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    if (entry.IsDirty)
                        GUILayout.Label("edited", EditorStyles.miniLabel);
                }
            }
        }

        /// <summary>
        /// One row per link in the chain, in the order the game tries them.
        /// </summary>
        /// <remarks>
        /// <para><b>Built on <see cref="ReorderableList"/> rather than a hand-rolled drag.</b> Three
        /// attempts at doing the dragging here were all subtly wrong, each in a way that only showed
        /// up under the hand: rows that lagged the cursor, and swaps that cost more travel upwards
        /// than downwards because the row rectangles excluded the margin between them. Unity's list
        /// already solves this, consistently with every other reorderable list in the editor.</para>
        ///
        /// <para>The list is rebuilt whenever the selected table changes, because it binds to that
        /// table's step collection.</para>
        /// </remarks>
        private void DrawChainEditor(StaticDataEntry entry)
        {
            GUILayout.Space(4f);

            ReorderableList list = this.GetChainList(entry);
            list.DoLayoutList();

            string invalid = entry.Validate();
            if (invalid != null)
                EditorGUILayout.HelpBox(invalid, MessageType.Warning);
        }

        // -----------------------------------------------------------------
        // Chain list
        // -----------------------------------------------------------------

        private ReorderableList _chainList;
        private StaticDataEntry _chainListEntry;

        /// <summary>Row height for a step that only needs its header line.</summary>
        private const float StepHeaderHeight = 22f;

        /// <summary>Height of one stacked field under the header.</summary>
        private const float StepFieldHeight = 20f;

        private const float StepPadding = 4f;

        /// <summary>
        /// The list for this table, rebuilt when the selection moves to a different one.
        /// </summary>
        private ReorderableList GetChainList(StaticDataEntry entry)
        {
            if (this._chainList != null && ReferenceEquals(this._chainListEntry, entry))
                return this._chainList;

            this._chainListEntry = entry;
            this._chainList = new ReorderableList(
                entry.StepsForReordering, typeof(StaticDataChainStep),
                draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, "Fallback chain — tried top to bottom"),

                elementHeightCallback = index => this.GetStepHeight(entry, index),

                drawElementCallback = (rect, index, isActive, isFocused) =>
                    this.DrawStepElement(entry, rect, index),

                onAddCallback = _ =>
                {
                    entry.AddStep();
                    this._dirtyCountDirty = true;
                    this._needsRepaint = true;
                },

                onRemoveCallback = list =>
                {
                    int index = list.index;
                    if (index < 0 || index >= entry.Steps.Count)
                        return;

                    StaticDataChainStep step = entry.Steps[index];

                    // A disabled step is usually a deliberate note - a link kept in the file but
                    // switched off - and once it is gone the reason it was there goes with it. An
                    // enabled step is just a line that can be added back, so it goes quietly.
                    if (!step.IsEnabled &&
                        !StaticDataDialogs.ConfirmRemoveDisabledStep(step.SourceType.ToString(), step.Key))
                        return;

                    entry.RemoveStep(index);
                    this._dirtyCountDirty = true;
                    this._needsRepaint = true;
                },

                // The list reorders its own backing collection; this only records that the file no
                // longer matches what is on screen.
                onReorderCallback = _ =>
                {
                    entry.MarkDirty();
                    this._dirtyCountDirty = true;
                    this._needsRepaint = true;
                },
            };

            return this._chainList;
        }

        /// <summary>Drops the cached list, so the next draw rebuilds it against the new table.</summary>
        private void InvalidateChainList()
        {
            this._chainList = null;
            this._chainListEntry = null;
        }

        private float GetStepHeight(StaticDataEntry entry, int index)
        {
            if (index < 0 || index >= entry.Steps.Count)
                return StepHeaderHeight;

            StaticDataChainStep step = entry.Steps[index];
            float height = StepHeaderHeight + StepPadding;

            if (!step.IsKeyEditable)
                return height + StepFieldHeight + StepPadding + 36f;

            if (IsAssetSource(step.SourceType))
                height += StepFieldHeight + StepPadding;

            height += StepFieldHeight + StepPadding;

            if (this._assetWarnings.TryGetValue(index, out string warning) && !string.IsNullOrEmpty(warning))
                height += 36f;

            return height;
        }

        private void DrawStepElement(StaticDataEntry entry, Rect rect, int index)
        {
            if (index < 0 || index >= entry.Steps.Count)
                return;

            StaticDataChainStep step = entry.Steps[index];

            Rect line = new(rect.x, rect.y + 2f, rect.width, StepHeaderHeight - 2f);

            Rect toggleRect = new(line.x, line.y, 16f, line.height);
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUI.Toggle(toggleRect, step.IsEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                step.IsEnabled = enabled;
                entry.MarkDirty();
                this._dirtyCountDirty = true;
            }

            Rect popupRect = new(line.x + 20f, line.y, 130f, line.height);
            EditorGUI.BeginChangeCheck();
            StaticDataSourceType sourceType = DrawSourceTypePopupAt(popupRect, step.SourceType);
            if (EditorGUI.EndChangeCheck())
            {
                step.SourceType = sourceType;
                entry.MarkDirty();
                this._dirtyCountDirty = true;
            }

            // A disabled step is shown struck through in words rather than only by its checkbox, so
            // a commented-out link does not read as an active one at a glance.
            if (!step.IsEnabled)
            {
                Rect noteRect = new(popupRect.xMax + 8f, line.y, line.width - popupRect.width - 28f, line.height);
                EditorGUI.LabelField(noteRect, "disabled — kept as a comment", EditorStyles.miniLabel);
            }

            float y = rect.y + StepHeaderHeight + StepPadding;
            this.DrawStepKeyAt(entry, step, index, new Rect(rect.x, y, rect.width, StepFieldHeight));
        }

        /// <summary>
        /// Asset-backed sources get an object field, because a dropped asset cannot be misspelled
        /// the way a hand-typed path can. The resolved key is shown underneath so what will be
        /// written is never hidden.
        /// </summary>
        private void DrawStepKeyAt(StaticDataEntry entry, StaticDataChainStep step, int index, Rect rect)
        {
            if (!step.IsKeyEditable)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.TextField(rect, "Key", step.RawArgument);

                Rect helpRect = new(rect.x, rect.yMax + StepPadding, rect.width, 32f);
                EditorGUI.HelpBox(helpRect,
                    "This key is a constant in the source file, not a literal. It is shown here but " +
                    "left exactly as written — edit it in the file to change it.",
                    MessageType.Info);
                return;
            }

            bool isAssetSource = IsAssetSource(step.SourceType);
            Rect row = rect;

            if (isAssetSource)
            {
                EditorGUI.BeginChangeCheck();
                UnityEngine.Object asset = EditorGUI.ObjectField(
                    row, "Asset", entry.GetAsset(index), typeof(UnityEngine.Object), false);

                if (EditorGUI.EndChangeCheck())
                {
                    this._assetWarnings[index] = entry.SetAssetForStep(index, asset);
                    this._dirtyCountDirty = true;
                }

                row = new Rect(row.x, row.yMax + StepPadding, row.width, row.height);
            }

            EditorGUI.BeginChangeCheck();
            string key = EditorGUI.TextField(row, isAssetSource ? "Key (resolved)" : "Key", step.Key);
            if (EditorGUI.EndChangeCheck())
            {
                step.Key = key;
                entry.MarkDirty();
                this._dirtyCountDirty = true;
            }

            if (!this._assetWarnings.TryGetValue(index, out string message) || string.IsNullOrEmpty(message))
                return;

            Rect warningRect = new(row.x, row.yMax + StepPadding, row.width, 32f);
            EditorGUI.HelpBox(warningRect, message, MessageType.Warning);
        }

        private static bool IsAssetSource(StaticDataSourceType sourceType) =>
            sourceType == StaticDataSourceType.Resources ||
            sourceType == StaticDataSourceType.Addressable;

        /// <summary>Per-step warnings from asset resolution, cleared whenever the selection changes.</summary>
        private readonly Dictionary<int, string> _assetWarnings = new();

        private static StaticDataSourceType DrawSourceTypePopupAt(Rect rect, StaticDataSourceType current)
        {
            // None is deliberately absent: it is the enum's "no source" value, never a chain link.
            StaticDataSourceType[] options =
            {
                StaticDataSourceType.RemoteConfig,
                StaticDataSourceType.Resources,
                StaticDataSourceType.Addressable,
                StaticDataSourceType.Url,
            };

            string[] labels = { "Remote Config", "Resources", "Addressable", "URL (R2/S3)" };

            int currentIndex = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == current)
                    currentIndex = i;
            }

            int picked = EditorGUI.Popup(rect, currentIndex, labels);
            return options[picked];
        }

        /// <summary>
        /// Shows the exact code that Apply would write. The point of the tool is that a source
        /// rewrite is never a surprise.
        /// </summary>
        private void DrawPreview(StaticDataEntry entry)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Generated code", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextArea(entry.GetPreviewChainText(), GUILayout.MinHeight(72f));

            this.DrawOpenScriptButton(entry);
        }

        private void DrawOpenScriptButton(StaticDataEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (!GUILayout.Button("Open Script", EditorStyles.miniButton, GUILayout.Width(96f)))
                    return;

                UnityEngine.Object script =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.ScriptPath);

                if (script != null)
                    AssetDatabase.OpenAsset(script);
            }
        }

        private void DrawDetailActions(StaticDataEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(!entry.IsEditable || !entry.IsDirty))
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = ApplyAllColor;
                    if (GUILayout.Button("💾 Apply", StaticCommandButtonStyle, GUILayout.Width(96),
                            GUILayout.Height(26)))
                        this.ApplyOne(entry);
                    GUI.backgroundColor = previous;
                }

                GUILayout.Space(6);

                using (new EditorGUI.DisabledScope(!entry.IsDirty))
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = NeutralButtonColor;
                    if (GUILayout.Button("↩ Revert", StaticCommandButtonStyle, GUILayout.Width(96),
                            GUILayout.Height(26)))
                        this.RevertOne(entry);
                    GUI.backgroundColor = previous;
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Chains are stored in each controller's source file", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(this._statusText, EditorStyles.miniLabel);
            }
        }

        // -----------------------------------------------------------------
        // Operations
        // -----------------------------------------------------------------

        private void ApplyOne(StaticDataEntry entry)
        {
            if (!StaticDataDialogs.ConfirmApply(
                    entry.DisplayName, entry.ScriptPath,
                    entry.GetCurrentChainText(), entry.GetPreviewChainText()))
            {
                StaticDataDialogs.ReportCancelled();
                this.SetStatus("Apply cancelled");
                return;
            }

            if (!entry.Apply())
            {
                StaticDataDialogs.ReportApplyFailed(entry.DisplayName, entry.LastError);
                this.SetStatus($"Apply failed: {entry.DataId}");
                return;
            }

            this._assetWarnings.Clear();
            this.InvalidateChainList();
            this._dirtyCountDirty = true;
            this.SetStatus($"Applied {entry.DataId} - recompiling");

            AssetDatabase.Refresh();
        }

        private void ApplyAll()
        {
            List<StaticDataEntry> dirty = new();
            List<string> lines = new();

            foreach (StaticDataEntry entry in this._entries)
            {
                if (!entry.IsDirty || !entry.IsEditable)
                    continue;

                dirty.Add(entry);
                lines.Add($"{entry.DataId}  ->  {CountEnabled(entry)} source(s)");
            }

            if (dirty.Count == 0)
            {
                EditorUtility.DisplayDialog("Apply All Chains",
                    "No chains have been edited, so there is nothing to apply.", "OK");
                return;
            }

            if (!StaticDataDialogs.ConfirmApplyAll(lines))
            {
                StaticDataDialogs.ReportCancelled();
                this.SetStatus("Apply all cancelled");
                return;
            }

            int applied = 0;
            List<string> failures = new();

            foreach (StaticDataEntry entry in dirty)
            {
                if (entry.Apply())
                    applied++;
                else
                    failures.Add($"{entry.DataId} - {entry.LastError}");
            }

            StaticDataDialogs.ReportBatch(applied, dirty.Count, failures);

            this._assetWarnings.Clear();
            this.InvalidateChainList();
            this._dirtyCountDirty = true;
            this.SetStatus($"Applied {applied} of {dirty.Count} table(s)");

            // One refresh for the batch: recompiling after each file would reload the domain out
            // from under the loop still running it.
            if (applied > 0)
                AssetDatabase.Refresh();
        }

        private void RevertOne(StaticDataEntry entry)
        {
            entry.Reload();

            this._assetWarnings.Clear();
            this.InvalidateChainList();
            this._dirtyCountDirty = true;
            this.SetStatus($"Reverted {entry.DataId}");
        }

        private void RevertAll()
        {
            int dirty = this.GetDirtyCount();
            if (dirty == 0)
            {
                EditorUtility.DisplayDialog("Revert Changes", "Nothing has been edited.", "OK");
                return;
            }

            if (!StaticDataDialogs.ConfirmRevertAll(dirty))
            {
                StaticDataDialogs.ReportCancelled();
                return;
            }

            foreach (StaticDataEntry entry in this._entries)
                entry.Reload();

            this._assetWarnings.Clear();
            this.InvalidateChainList();
            this._dirtyCountDirty = true;
            this.SetStatus($"Reverted {dirty} table(s)");
        }

        // -----------------------------------------------------------------
        // Discovery
        // -----------------------------------------------------------------

        /// <summary>
        /// Finds every static data table via Unity's prebuilt type index, the same way
        /// <see cref="LocalDataTool"/> finds save domains.
        /// </summary>
        private void Rescan()
        {
            this._selected = null;
            this._entries.Clear();
            this._assetWarnings.Clear();
            this.InvalidateChainList();

            foreach (Type controllerType in TypeCache.GetTypesWithAttribute<StaticDataIdAttribute>())
            {
                if (controllerType.IsAbstract)
                    continue;

                StaticDataIdAttribute attribute = (StaticDataIdAttribute)Attribute.GetCustomAttribute(
                    controllerType, typeof(StaticDataIdAttribute));

                if (attribute == null || string.IsNullOrEmpty(attribute.DataId))
                    continue;

                this._entries.Add(new StaticDataEntry(attribute.DataId, controllerType));
            }

            this._entries.Sort((a, b) => string.CompareOrdinal(a.DataId, b.DataId));
            this._dirtyCountDirty = true;

            this.ApplyFilter();
            this.SetStatus($"Discovered {this._entries.Count} table(s)");
        }

        private void ApplyFilter()
        {
            this._visibleEntries.Clear();

            foreach (StaticDataEntry entry in this._entries)
            {
                if (string.IsNullOrWhiteSpace(this._searchFilter) ||
                    entry.DataId.IndexOf(this._searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.DisplayName.IndexOf(this._searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    this._visibleEntries.Add(entry);
                }
            }

            this._currentPage = 0;
            this._needsRepaint = true;
        }

        private void RestoreSelection()
        {
            string dataId = EditorPrefs.GetString(SelectedDataIdPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(dataId))
                return;

            foreach (StaticDataEntry entry in this._entries)
            {
                if (!string.Equals(entry.DataId, dataId, StringComparison.Ordinal))
                    continue;

                this.Select(entry);
                return;
            }
        }

        private void Select(StaticDataEntry entry)
        {
            if (ReferenceEquals(entry, this._selected))
                return;

            this._selected = entry;
            this._assetWarnings.Clear();
            this.InvalidateChainList();

            EditorPrefs.SetString(SelectedDataIdPrefsKey, entry?.DataId ?? string.Empty);
            this._needsRepaint = true;
        }

        /// <summary>Cached, so the row footer does not walk every entry on each OnGUI event.</summary>
        private int GetDirtyCount()
        {
            if (!this._dirtyCountDirty)
                return this._dirtyCountCache;

            this._dirtyCountCache = 0;
            foreach (StaticDataEntry entry in this._entries)
            {
                if (entry.IsDirty)
                    this._dirtyCountCache++;
            }

            this._dirtyCountDirty = false;
            return this._dirtyCountCache;
        }

        private void SetStatus(string message)
        {
            this._statusText = message;
            this._needsRepaint = true;
        }
    }
}