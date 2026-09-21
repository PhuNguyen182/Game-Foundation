using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.StaticData;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using Sirenix.Utilities.Editor;
using UnityEditor;
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

            // Both halves of the drag are handled here rather than inside the handle, because the
            // handle only receives events while the pointer is still inside its own 16x18 slot -
            // move the cursor faster than the list can follow and it would simply stop hearing
            // about the drag, stalling the row until the pointer wandered back.
            this.UpdateDragFromWindowEvents();

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
        /// One row per link in the chain. The order of the rows is the order the game tries them,
        /// which is why reordering is the primary control rather than a numeric field.
        /// </summary>
        private void DrawChainEditor(StaticDataEntry entry)
        {
            GUILayout.Space(4f);
            GUILayout.Label("Fallback chain — tried top to bottom, drag the grip to reorder",
                EditorStyles.boldLabel);


            for (int index = 0; index < entry.Steps.Count; index++)
                this.DrawChainStep(entry, index);

            // After every row, so the tint is not painted over by the row that follows it.
            this.DrawDraggedRowHighlight();

            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add Source", GUILayout.Width(110f), GUILayout.Height(22f)))
                {
                    entry.AddStep();
                    this._dirtyCountDirty = true;
                    this._needsRepaint = true;
                }

                GUILayout.FlexibleSpace();
            }

            string invalid = entry.Validate();
            if (invalid != null)
                EditorGUILayout.HelpBox(invalid, MessageType.Warning);
        }

        private void DrawChainStep(StaticDataEntry entry, int index)
        {
            StaticDataChainStep step = entry.Steps[index];

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    this.DrawDragHandle(entry, index);

                    GUILayout.Label($"{index + 1}.", GUILayout.Width(18f));

                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.Toggle(step.IsEnabled, GUILayout.Width(16f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        step.IsEnabled = enabled;
                        entry.MarkDirty();
                        this._dirtyCountDirty = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    StaticDataSourceType sourceType = DrawSourceTypePopup(step.SourceType);
                    if (EditorGUI.EndChangeCheck())
                    {
                        step.SourceType = sourceType;
                        entry.MarkDirty();
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        // A disabled step is usually a deliberate note - a link kept in the file but
                        // switched off - and once it is gone the reason it was there goes with it.
                        // An enabled step is just a line that can be added back, so it goes quietly.
                        if (step.IsEnabled ||
                            StaticDataDialogs.ConfirmRemoveDisabledStep(step.SourceType.ToString(), step.Key))
                        {
                            entry.RemoveStep(index);
                            this._dirtyCountDirty = true;
                            this._needsRepaint = true;
                        }

                        return;
                    }
                }

                this.DrawStepKey(entry, step, index);
            }

            // Measured after the row is laid out, so the drop test uses the row's real height
            // whatever the source type put inside it.
            if (Event.current.type == EventType.Repaint)
                this._stepRects[index] = GUILayoutUtility.GetLastRect();
        }

        // -----------------------------------------------------------------
        // Reordering by drag
        // -----------------------------------------------------------------

        /// <summary>Row rectangles from the last repaint, used to decide what the pointer is over.</summary>
        private readonly Dictionary<int, Rect> _stepRects = new();

        /// <summary>
        /// Index of the row being dragged, or -1. It follows the row as the list reorders under it,
        /// so it is where the row is now, not where the drag began.
        /// </summary>
        private int _draggingStep = -1;

        /// <summary>
        /// Pointer offset inside the handle when the drag began, so the grip stays under the cursor
        /// instead of snapping its top-left corner to it.
        /// </summary>
        private float _dragGrabOffset;

        /// <summary>Pointer position while dragging, used to draw the row at the cursor.</summary>
        private float _dragPointerY;

        private static readonly Color DragHandleColor = new(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color DraggedRowColor = new(0.24f, 0.48f, 0.90f, 0.20f);
        private static readonly Color DraggedRowEdgeColor = new(0.24f, 0.48f, 0.90f, 0.9f);

        /// <summary>
        /// Advances or ends the drag from events seen anywhere in the window.
        /// </summary>
        /// <remarks>
        /// Reads <see cref="Event.current"/> without consuming it: by this point the panes have
        /// already had their turn, and a drag of a chain row is not something any of them also
        /// wants.
        /// </remarks>
        private void UpdateDragFromWindowEvents()
        {
            if (this._draggingStep < 0 || this._selected == null)
                return;

            if (Event.current.type == EventType.MouseDrag)
            {
                this._dragPointerY = Event.current.mousePosition.y;
                this.ReorderWhilstDragging(this._selected);
                this._needsRepaint = true;
                return;
            }

            // MouseLeaveWindow covers the pointer leaving with the button still down, after which
            // the release is delivered somewhere this window will never see.
            if (Event.current.type != EventType.MouseUp &&
                Event.current.type != EventType.MouseLeaveWindow &&
                Event.current.rawType != EventType.MouseUp)
                return;

            this._draggingStep = -1;
            this._needsRepaint = true;
        }

        /// <summary>
        /// A grip that reorders the chain by dragging.
        /// </summary>
        /// <remarks>
        /// <para><b>The list reorders under the pointer, rather than on release.</b> As soon as the
        /// cursor passes the midpoint of a neighbouring row, the two swap, so the chain always reads
        /// as it will end up and there is no separate marker to interpret. <c>_draggingStep</c> is
        /// updated to the row's new index in the same step, which is what keeps the row that is
        /// being dragged following the cursor instead of being left behind by its own move.</para>
        ///
        /// <para>Only the handle starts a drag, not the whole row: the row also carries a popup, a
        /// toggle, an object field and a text field, and a row-wide drag would steal the press that
        /// was meant for one of those.</para>
        /// </remarks>
        private void DrawDragHandle(StaticDataEntry entry, int index)
        {
            Rect handleRect = GUILayoutUtility.GetRect(16f, 18f, GUILayout.Width(16f), GUILayout.Height(18f));

            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.MoveArrow);

            if (Event.current.type == EventType.Repaint)
            {
                // Three short bars - the conventional grip, and legible at this size where a glyph
                // would render as an unreadable smudge.
                Color barColor = this._draggingStep == index ? DraggedRowEdgeColor : DragHandleColor;

                for (int line = 0; line < 3; line++)
                {
                    Rect bar = new(handleRect.x + 3f, handleRect.y + 4f + line * 4f, 10f, 1.5f);
                    EditorGUI.DrawRect(bar, barColor);
                }
            }

            switch (Event.current.type)
            {
                case EventType.MouseDown when handleRect.Contains(Event.current.mousePosition):
                    this._draggingStep = index;
                    this._dragPointerY = Event.current.mousePosition.y;
                    this._dragGrabOffset = Event.current.mousePosition.y -
                                           this.GetStepRect(index, handleRect).y;
                    Event.current.Use();
                    break;


            }
        }

        /// <summary>
        /// Swaps the dragged row with its neighbour as soon as the pointer passes that neighbour's
        /// midpoint, and keeps <see cref="_draggingStep"/> pointing at the row afterwards.
        /// </summary>
        /// <remarks>
        /// One step per event on purpose. The rects come from the last repaint, so after a swap the
        /// geometry this is reading is stale by one row; moving further than that in a single event
        /// would be moving against measurements that no longer describe the list.
        /// </remarks>
        private void ReorderWhilstDragging(StaticDataEntry entry)
        {
            int current = this._draggingStep;
            if (current < 0 || current >= entry.Steps.Count)
                return;

            // Where the top of the dragged row now sits, following the cursor.
            float draggedTop = this._dragPointerY - this._dragGrabOffset;

            if (current > 0 && this._stepRects.TryGetValue(current - 1, out Rect above) &&
                draggedTop < above.y + above.height * 0.5f)
            {
                entry.MoveStepTo(current, current - 1);
                this.SwapStepRects(current, current - 1);
                this._draggingStep = current - 1;
                this._dirtyCountDirty = true;
                return;
            }

            if (current >= entry.Steps.Count - 1 ||
                !this._stepRects.TryGetValue(current, out Rect self) ||
                !this._stepRects.TryGetValue(current + 1, out Rect below))
                return;

            if (draggedTop + self.height > below.y + below.height * 0.5f)
            {
                entry.MoveStepTo(current, current + 1);
                this.SwapStepRects(current, current + 1);
                this._draggingStep = current + 1;
                this._dirtyCountDirty = true;
            }
        }

        /// <summary>
        /// Rebuilds two cached rects so the next drag event reads geometry that matches the order
        /// the list is now in, without waiting for a repaint to re-measure it.
        /// </summary>
        /// <remarks>
        /// The heights travel with the rows while the block they occupy stays put, because rows are
        /// not all the same height - a Resources step carries an object field that a remote config
        /// step does not. Reusing the old positions would leave the next event comparing the pointer
        /// against a row boundary that has moved, which reads as the drag stuttering.
        /// </remarks>
        private void SwapStepRects(int from, int to)
        {
            if (!this._stepRects.TryGetValue(from, out Rect dragged) ||
                !this._stepRects.TryGetValue(to, out Rect displaced))
                return;

            // The row that ends up on top is whichever index is smaller after the move.
            bool draggedEndsOnTop = to < from;

            Rect upper = draggedEndsOnTop ? dragged : displaced;
            Rect lower = draggedEndsOnTop ? displaced : dragged;

            int upperIndex = Mathf.Min(from, to);
            int lowerIndex = Mathf.Max(from, to);

            float top = Mathf.Min(dragged.y, displaced.y);

            this._stepRects[upperIndex] = new Rect(upper.x, top, upper.width, upper.height);
            this._stepRects[lowerIndex] = new Rect(lower.x, top + upper.height, lower.width, lower.height);
        }

        /// <summary>The cached rect for a row, falling back to the handle when none was measured yet.</summary>
        private Rect GetStepRect(int index, Rect fallback) =>
            this._stepRects.TryGetValue(index, out Rect rect) ? rect : fallback;

        /// <summary>
        /// Tints the row being dragged so it reads as lifted out of the list while the rows around
        /// it move. Drawn after every row, so it is not painted over by the next one.
        /// </summary>
        private void DrawDraggedRowHighlight()
        {
            if (this._draggingStep < 0 || Event.current.type != EventType.Repaint ||
                !this._stepRects.TryGetValue(this._draggingStep, out Rect rect))
                return;

            EditorGUI.DrawRect(rect, DraggedRowColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), DraggedRowEdgeColor);

            this._needsRepaint = true;
        }

        /// <summary>
        /// Asset-backed sources get an object field, because a dropped asset cannot be misspelled
        /// the way a hand-typed path can. The resolved key is shown underneath so what will be
        /// written is never hidden.
        /// </summary>
        private void DrawStepKey(StaticDataEntry entry, StaticDataChainStep step, int index)
        {
            if (!step.IsKeyEditable)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("Key", step.RawArgument);

                EditorGUILayout.HelpBox(
                    "This key is a constant in the source file, not a literal. It is shown here but " +
                    "left exactly as written — edit it in the file to change it.",
                    MessageType.Info);
                return;
            }

            bool isAssetSource = step.SourceType == StaticDataSourceType.Resources ||
                                 step.SourceType == StaticDataSourceType.Addressable;

            if (isAssetSource)
            {
                EditorGUI.BeginChangeCheck();
                UnityEngine.Object asset = EditorGUILayout.ObjectField(
                    "Asset", entry.GetAsset(index), typeof(UnityEngine.Object), false);

                if (EditorGUI.EndChangeCheck())
                {
                    string warning = entry.SetAssetForStep(index, asset);
                    this._assetWarnings[index] = warning;
                    this._dirtyCountDirty = true;
                }
            }

            EditorGUI.BeginChangeCheck();
            string key = EditorGUILayout.TextField(isAssetSource ? "Key (resolved)" : "Key", step.Key);
            if (EditorGUI.EndChangeCheck())
            {
                step.Key = key;
                entry.MarkDirty();
                this._dirtyCountDirty = true;
            }

            if (this._assetWarnings.TryGetValue(index, out string message) && !string.IsNullOrEmpty(message))
                EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        /// <summary>Per-step warnings from asset resolution, cleared whenever the selection changes.</summary>
        private readonly Dictionary<int, string> _assetWarnings = new();

        private static StaticDataSourceType DrawSourceTypePopup(StaticDataSourceType current)
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

            int picked = EditorGUILayout.Popup(currentIndex, labels, GUILayout.Width(130f));
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