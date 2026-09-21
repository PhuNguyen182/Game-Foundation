using System;
using System.Collections.Generic;
using System.IO;
using DracoRuan.Foundation.DataFlow.Core.Serialization;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.Runtime;
using DracoRuan.Foundation.DataFlow.Serialization;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Editor window for inspecting and editing local save data.
    ///
    /// <para>
    /// Master–detail: domains on the left, the selected domain's data on the right. Only the
    /// selected entry holds an Odin <c>PropertyTree</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para><b>Why master–detail rather than the previous accordion.</b> The accordion let every
    /// entry be expanded at once, and "Load All" expanded all of them, so every domain's Odin tree
    /// was rebuilt and redrawn on every repaint — of which IMGUI fires several per frame. Showing
    /// one at a time caps the cost at a single tree regardless of how many domains a project has.</para>
    ///
    /// <para><b>Discovery uses <see cref="TypeCache"/></b>, Unity's prebuilt type index, instead of
    /// walking <c>AppDomain.GetAssemblies()</c> and calling <c>GetTypes()</c> on each — which forced
    /// every type in every editor assembly to load, and ran again before every "Load All".</para>
    ///
    /// <para><b>Everything goes through <see cref="SaveEnvelopeStore"/></b>, the same code the game
    /// uses, so the tool cannot drift out of sync with the save format.</para>
    /// </remarks>
    public sealed class LocalDataTool : EditorWindow
    {
        private const string SelectedDomainPrefsKey = "DracoRuan.LocalDataTool.SelectedDomain";
        private const string SplitWidthPrefsKey = "DracoRuan.LocalDataTool.SplitWidth";

        private const float MinListWidth = 200f;
        private const float MaxListWidth = 460f;
        private const float SplitterWidth = 6f;
        private const float EntryRowHeight = 44f;
        private const int EntriesPerPage = 10;

        private static readonly Color SplitterColor = new(0.13f, 0.13f, 0.13f, 1f);
        private static readonly Color SplitterHoverColor = new(0.24f, 0.48f, 0.90f, 0.9f);
        private static readonly Color SelectedRowColor = new(0.24f, 0.48f, 0.90f, 0.28f);
        private static readonly Color AlternateRowColor = new(0f, 0f, 0f, 0.06f);

        private static readonly Color LoadAllColor = new(0.45f, 0.68f, 0.98f, 1f);
        private static readonly Color SaveAllColor = new(0.45f, 0.78f, 0.55f, 1f);
        private static readonly Color NeutralButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color DeleteAllColor = new(0.95f, 0.45f, 0.45f, 1f);

        private readonly List<LocalDataEntry> _entries = new();
        private readonly List<LocalDataEntry> _visibleEntries = new();

        private SaveEnvelopeStore _store;
        private IPayloadCodec _codec;

        private LocalDataEntry _selected;
        private string _searchFilter = string.Empty;
        private string _statusText = "Ready";
        private string _saveFolderPath;

        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private float _listWidth = 240f;
        private bool _isDraggingSplitter;
        private bool _isHoveringSplitter;
        private int _currentPage;

        /// <summary>
        /// Repaints are coalesced to one per editor frame. Previously every changed control fired a
        /// status update that formatted a timestamp and forced an immediate repaint, so typing in a
        /// field redrew the whole window on every keystroke.
        /// </summary>
        private bool _needsRepaint;

        private int _loadedCountCache;
        private bool _loadedCountDirty = true;

        [MenuItem("Tools/Foundations/Local Data Editor/Local Game Data Manager", false, 100)]
        public static void ShowWindow()
        {
            LocalDataTool window = GetWindow<LocalDataTool>();
            window.titleContent = new GUIContent("💽 Local Game Data Manager");
            // Wide enough that the toolbar's buttons are never resized, relabeled, or dropped to
            // fit - see DrawToolbar's remarks for exactly what this width covers.
            window.minSize = new Vector2(Mathf.Max(720f, MinWindowWidth), 420);
            window.Show();
        }

        private void OnEnable()
        {
            this._saveFolderPath = Path.Combine(
                Application.persistentDataPath, DataFlowRegistration.SaveDirectoryName);

            this._store = new SaveEnvelopeStore(this._saveFolderPath);
            this._codec = new MessagePackPayloadCodec();

            this._listWidth = EditorPrefs.GetFloat(SplitWidthPrefsKey, 240f);

            this.Rescan();
            this.RestoreSelection();
        }

        private void OnDisable()
        {
            this._selected?.ReleaseTree();
            EditorPrefs.SetFloat(SplitWidthPrefsKey, this._listWidth);
        }

        private void OnGUI()
        {
            using (NoFoldoutAnimationScope.Enter())
            {
                this.DrawToolbar();

                using (new EditorGUILayout.HorizontalScope())
                {
                    this.DrawDomainList();
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
        /// then restores whatever it was.
        /// </summary>
        /// <remarks>
        /// <para><b>What was actually slow.</b> Every individual OnGUI pass in this window measured
        /// under 3ms even with a loaded List and Dictionary. The stutter reported by users was
        /// several hundred back-to-back Layout/Repaint pairs firing over roughly a second after a
        /// single click - Odin animating a foldout's expand/collapse by easing it open over many
        /// frames via <c>SirenixEditorGUI.BeginFadeGroup</c>, each frame requesting the next repaint
        /// itself. No single frame was ever slow; the editor was just kept busy re-rendering the same
        /// transition dozens of times in a row, which reads as "the tool is frozen for over a
        /// second".</para>
        ///
        /// <para><b>Why this is scoped instead of a one-time global setting.</b>
        /// <see cref="SirenixEditorGUI.DefaultFadeGroupDuration"/> is a single static shared by every
        /// Odin-drawn window in the editor - Inspectors included. Setting it once on
        /// <c>OnEnable</c> would silently remove foldout animation from the rest of the editor for as
        /// long as this window exists, which is a bigger behavior change than "make this tool
        /// responsive" calls for. Zeroing it only around this window's own <c>OnGUI</c> and putting it
        /// back immediately after keeps the effect confined to here.</para>
        /// </remarks>
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

        /// <summary>Sum of every fixed-width button plus the spacing between them and around the
        /// search field, not counting the field's own width. Whatever room is left over past this
        /// goes entirely to the search field, down to <see cref="MinSearchWidth"/>.</summary>
        private const float ToolbarButtonsWidth =
            6f + 96f + 6f + 96f + 12f + 90f + 6f + 112f + 12f + 12f + 104f + 6f;

        /// <summary>Search field never shrinks below this - narrow enough to still show a few
        /// characters of a domain id, which is all it needs to do at the window's minimum width.</summary>
        private const float MinSearchWidth = 120f;

        /// <summary>Search field never grows past this even when the window is very wide - a filter
        /// box has no reason to become a paragraph-length text field.</summary>
        private const float MaxSearchWidth = 420f;

        /// <summary>Bar width below which the buttons alone, plus <see cref="MinSearchWidth"/>, no
        /// longer fit comfortably; the window cannot usefully go narrower than this, so it is
        /// enforced as <c>minSize</c>.</summary>
        private const float MinWindowWidth = ToolbarButtonsWidth + MinSearchWidth;

        /// <summary>
        /// Drawn on a plain background rect rather than <see cref="EditorStyles.toolbar"/>, which is
        /// a fixed-height style built for the thin default toolbar row and clips anything taller than
        /// it - which is exactly what made the previous 26-32px buttons render cut off at the top and
        /// bottom instead of centered. Every control here shares <see cref="ToolbarControlHeight"/>
        /// and sits directly in the normal IMGUI layout flow (no <c>BeginArea</c>) inside one
        /// <see cref="EditorGUILayout.HorizontalScope"/>, so Unity recomputes each control's real
        /// position from the window's actual current width every layout pass rather than from a
        /// <see cref="Rect"/> this code would otherwise have to keep in sync by hand.
        ///
        /// <para><b>Staying usable at any window width.</b> The search field is the one element
        /// treated as elastic: it fills whatever space is left between the two button groups,
        /// clamped between <see cref="MinSearchWidth"/> and <see cref="MaxSearchWidth"/>, so it grows
        /// on a wide window and shrinks - never disappears - on a narrow one. Every button keeps the
        /// exact size and position this design calls for regardless of window width.
        /// <see cref="ShowWindow"/> also sets <c>minSize</c> to <see cref="MinWindowWidth"/>, the
        /// point below which even the field's minimum would start crowding the buttons.</para>
        /// </summary>
        private void DrawToolbar()
        {
            // One HorizontalScope is the entire toolbar's layout space - background, buttons, and
            // the search field are all measured and positioned from this single Rect. The previous
            // version reserved a separate ToolbarHeight-tall Rect purely to paint the background,
            // then laid the actual controls out afterward in their own GUILayout.Space + Horizontal-
            // Scope block; those were two independent regions stacked one after the other in the
            // layout flow rather than one overlapping the other, which is what left a tall band of
            // empty background above a short, cramped control row.
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(ToolbarHeight)))
            {
                Rect barRect = GUILayoutUtility.GetRect(0f, ToolbarHeight, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(barRect, ToolbarBackgroundColor);

                // The search field is the one elastic element: it takes whatever width is left over
                // between the button groups on either side of it, clamped to a sane range, so it
                // grows on a wide window and only gives up typing room - never legibility or being
                // clickable - once the window gets narrow. Every button keeps the exact size and
                // position this design calls for at any window width.
                float searchWidth = Mathf.Clamp(barRect.width - ToolbarButtonsWidth, MinSearchWidth, MaxSearchWidth);

                // Every control below is placed with an absolute Rect derived from barRect, on the
                // same vertical center, rather than nested in GUILayout's own flow - GUILayout has no
                // way to overlap new controls onto a Rect it already consumed for the background.
                float centerY = barRect.y + barRect.height * 0.5f;
                float cursorX = barRect.x + 6f;

                cursorX = DrawCommandButtonAt(cursorX, centerY, "📥 Load All", LoadAllColor, 96f, this.LoadAll);
                cursorX += 6f;
                cursorX = DrawCommandButtonAt(cursorX, centerY, "💾 Save All", SaveAllColor, 96f, this.SaveAll);
                cursorX += 12f;
                cursorX = DrawCommandButtonAt(cursorX, centerY, "🔄 Refresh", NeutralButtonColor, 90f, this.Rescan);
                cursorX += 6f;
                cursorX = DrawCommandButtonAt(cursorX, centerY, "📁 Open Folder", NeutralButtonColor, 112f,
                    this.OpenSaveFolder);

                float deleteWidth = 104f;
                float rightEdge = barRect.xMax - 6f;
                float deleteX = rightEdge - deleteWidth;
                DrawCommandButtonAt(deleteX, centerY, "🗑 Delete All", DeleteAllColor, deleteWidth, this.DeleteAll);

                float searchX = deleteX - 12f - searchWidth;
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

        /// <summary>
        /// Placed at an absolute Rect, like <see cref="DrawCommandButtonAt"/>, since it shares the
        /// same background Rect as the buttons rather than GUILayout's own flow.
        /// </summary>
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
                    GUI.Label(placeholderRect, "Search domains…", EditorStyles.label);
            }
        }

        // -----------------------------------------------------------------
        // Left pane
        // -----------------------------------------------------------------

        private GUIStyle _entryNameStyle;
        private GUIStyle _detailTitleStyle;

        /// <summary>Bold and a size step up from the default label, so a domain's title is the
        /// first thing the eye lands on in each row.</summary>
        private GUIStyle EntryNameStyle => this._entryNameStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
        };

        /// <summary>
        /// Bigger again than <see cref="EntryNameStyle"/> - this is the page heading for the selected
        /// domain, not a row in a list, so it reads as the most prominent text in the detail pane.
        /// </summary>
        /// <remarks>
        /// Built from <c>GUIStyle.none</c> rather than copy-constructed from
        /// <see cref="EditorStyles.boldLabel"/>. Copying a built-in style and overriding
        /// <c>normal.textColor</c> - and separately, wrapping the draw call in
        /// <see cref="GUI.contentColor"/> - both failed to change the rendered color here, which
        /// means the built-in style's <c>GUIStyleState</c> is not plain data this code can safely
        /// override; something about the editor skin keeps re-asserting its own color on it. Starting
        /// from an empty style with its own freshly-created <see cref="GUIStyleState"/> sidesteps
        /// that entirely - there is no shared state left for anything else to reassert control over.
        /// </remarks>
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

        private void DrawDomainList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(this._listWidth)))
            {
                IReadOnlyList<LocalDataEntry> pageEntries = this.GetCurrentPageEntries();

                this._listScroll = EditorGUILayout.BeginScrollView(this._listScroll);

                if (this._visibleEntries.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        this._entries.Count == 0
                            ? "No save repositories found.\n\nAdd [DynamicGameDataController(Id)] to a " +
                              "controller, then press Refresh."
                            : "No domains match the current search.",
                        MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < pageEntries.Count; i++)
                    {
                        this.DrawDomainRow(pageEntries[i], isAlternate: i % 2 == 1);
                        GUILayout.Space(3f);
                    }
                }

                EditorGUILayout.EndScrollView();

                this.DrawPager();

                GUILayout.Space(2f);
                GUILayout.Label(
                    $"{this._entries.Count} domain(s), {this.GetLoadedCount()} loaded",
                    EditorStyles.label);
            }
        }

        /// <summary>Total pages for the current filtered list, at least 1 so page arithmetic never
        /// divides against zero.</summary>
        private int PageCount => Mathf.Max(1, Mathf.CeilToInt(this._visibleEntries.Count / (float)EntriesPerPage));

        private IReadOnlyList<LocalDataEntry> GetCurrentPageEntries()
        {
            this._currentPage = Mathf.Clamp(this._currentPage, 0, this.PageCount - 1);

            int start = this._currentPage * EntriesPerPage;
            int count = Mathf.Min(EntriesPerPage, this._visibleEntries.Count - start);

            return count > 0 ? this._visibleEntries.GetRange(start, count) : Array.Empty<LocalDataEntry>();
        }

        /// <summary>Prev/Next controls. Hidden entirely when everything fits on one page, so a
        /// small project never sees dead navigation chrome.</summary>
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

        private void DrawDomainRow(LocalDataEntry entry, bool isAlternate)
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

            (string glyph, Color glyphColor, string tooltip) = this.GetStateGlyph(entry);

            Rect markerRect = new(rect.x + 8f, rect.y + rect.height * 0.5f - 10f, 20f, 20f);
            Color previousColor = GUI.color;
            GUI.color = glyphColor;
            GUI.Label(markerRect, new GUIContent(glyph, tooltip), EditorStyles.boldLabel);
            GUI.color = previousColor;

            Rect nameRect = new(rect.x + 32f, rect.y + 5f, rect.width - 38f, 20f);
            GUI.Label(nameRect, entry.DisplayName, this.EntryNameStyle);

            Rect subRect = new(rect.x + 32f, rect.y + 24f, rect.width - 38f, 16f);

            // "latest v3", not "version 3": this row is one domain, never one version, and the
            // number here is what is newest on disk rather than what the detail pane is showing.
            // Spelled as a bare version it reads as the row's own version, which invites the
            // question of where the rows for the other versions went - and it contradicts the
            // version dropdown outright whenever an older version is selected.
            string subtitle = entry.HasFiles
                ? $"{entry.DomainId}   ·   latest v{entry.LatestVersion}"
                : $"{entry.DomainId}   ·   no data";
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

        private (string glyph, Color color, string tooltip) GetStateGlyph(LocalDataEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.LastError))
                return ("🔴", DeleteAllColor, entry.LastError);

            if (entry.HasData)
                return ("🟢", SaveAllColor, "Loaded");

            return ("⚪", NeutralButtonColor, entry.HasFiles ? "Not loaded" : "No save data");
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
                // Wide and clearly colored, with a brighter accent while hovered or dragged, so the
                // boundary between the two panels reads as a deliberate divider rather than a
                // one-pixel seam that is easy to miss and hard to grab.
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
                    GUILayout.Label("Select a domain to inspect its save data.",
                        EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                    return;
                }

                this.DrawDetailHeader(this._selected);

                this._detailScroll = EditorGUILayout.BeginScrollView(this._detailScroll);

                if (this._selected.HasData)
                {
                    // Only the Layout event is checked. Odin reports GUI.changed on the Repaint
                    // event too whenever a foldout, list, or dictionary is mid-animation - not just
                    // when a field's value actually changed - so checking every event type turned
                    // "expand this list" into "Modified: <domain>" in the status bar, plus an extra
                    // Repaint() request stacked on top of the animation's own repaint schedule. The
                    // one Layout event per interaction already reflects the same control state.
                    EditorGUI.BeginChangeCheck();
                    this._selected.DrawData();
                    if (EditorGUI.EndChangeCheck() && Event.current.type == EventType.Layout)
                        this.SetStatus($"Modified: {this._selected.DomainId}");
                }
                else if (!string.IsNullOrEmpty(this._selected.LastError))
                {
                    EditorGUILayout.HelpBox(this._selected.LastError, MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        this._selected.HasFiles
                            ? "Press Load to read this domain's save data."
                            : "This domain has no save file yet. Run the game to create one.",
                        MessageType.Info);
                }

                EditorGUILayout.EndScrollView();

                this.DrawDetailActions(this._selected);
            }
        }

        private void DrawDetailHeader(LocalDataEntry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // GUIStyle.normal.textColor set on a style copy-constructed from a built-in style
                // (EditorStyles.boldLabel here) does not reliably stick - the built-in style's
                // GUIStyleState objects are shared, editor-skin-driven state, not plain data the
                // copy constructor duplicates. GUI.contentColor bypasses that entirely: it tints
                // whatever the style would have drawn, which is the reliable way to force a color
                // regardless of what state the source style holds.
                Color previousContentColor = GUI.contentColor;
                GUI.contentColor = Color.white;
                GUILayout.Label(entry.DisplayName, this.DetailTitleStyle);
                GUI.contentColor = previousContentColor;

                GUILayout.Label(entry.DomainId, EditorStyles.label);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Version", GUILayout.Width(52));

                    if (entry.VisibleVersions.Count > 0)
                    {
                        string[] labels = new string[entry.VisibleVersions.Count];
                        int currentIndex = 0;

                        for (int i = 0; i < entry.VisibleVersions.Count; i++)
                        {
                            int version = entry.VisibleVersions[i];
                            // This dropdown is the only place that says which version is on screen;
                            // the list row deliberately says "latest vN" instead, so the two never
                            // show conflicting numbers while an older version is selected.
                            labels[i] = i == 0 ? $"v{version} (latest)" : $"v{version}";

                            if (version == entry.LoadedVersion)
                                currentIndex = i;
                        }

                        int picked = EditorGUILayout.Popup(currentIndex, labels, GUILayout.Width(110));
                        if (picked != currentIndex)
                            this.LoadVersion(entry, entry.VisibleVersions[picked]);
                    }
                    else
                    {
                        GUILayout.Label("none", EditorStyles.label, GUILayout.Width(110));
                    }

                    GUILayout.FlexibleSpace();

                    if (entry.HasData)
                    {
                        GUILayout.Label(
                            $"{FormatBytes(entry.LoadedSizeBytes)}  ·  Revision {entry.LoadedHeader.Revision}  ·  " +
                            entry.GetModifiedUtc(entry.LoadedVersion),
                            EditorStyles.label);
                    }
                }

                if (entry.IsViewingOldVersion)
                {
                    EditorGUILayout.HelpBox(
                        $"You are viewing v{entry.LoadedVersion}, which is not the latest (v{entry.LatestVersion}). " +
                        $"Saving writes back to v{entry.LoadedVersion}.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawDetailActions(LocalDataEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(!entry.HasFiles))
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = LoadAllColor;
                    if (GUILayout.Button("📥 Load", StaticCommandButtonStyle, GUILayout.Width(96),
                            GUILayout.Height(26)))
                        this.LoadVersion(entry, entry.LoadedVersion > 0 ? entry.LoadedVersion : 0);
                    GUI.backgroundColor = previous;
                }

                GUILayout.Space(6);

                using (new EditorGUI.DisabledScope(!entry.HasData))
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = SaveAllColor;
                    if (GUILayout.Button("💾 Save", StaticCommandButtonStyle, GUILayout.Width(96),
                            GUILayout.Height(26)))
                        this.SaveOne(entry);
                    GUI.backgroundColor = previous;
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!entry.HasFiles))
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = DeleteAllColor;
                    if (GUILayout.Button("🗑 Delete", StaticCommandButtonStyle, GUILayout.Width(96),
                            GUILayout.Height(26)))
                        this.DeleteOne(entry);
                    GUI.backgroundColor = previous;
                }
            }
        }

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button(this._saveFolderPath, EditorStyles.miniLabel))
                    this.OpenSaveFolder();

                GUILayout.FlexibleSpace();
                GUILayout.Label(this._statusText, EditorStyles.miniLabel);
            }
        }

        // -----------------------------------------------------------------
        // Operations
        // -----------------------------------------------------------------

        private void LoadVersion(LocalDataEntry entry, int version)
        {
            this.Select(entry);

            if (entry.Load(version))
            {
                this.SetStatus($"Loaded {entry.DomainId} v{entry.LoadedVersion}");
                this._loadedCountDirty = true;
                return;
            }

            LocalDataDialogs.ReportLoadFailed(entry.DisplayName, entry.LastError);
            this.SetStatus($"Load failed: {entry.DomainId}");
        }

        private void SaveOne(LocalDataEntry entry)
        {
            int version = entry.LoadedVersion;
            string fileName = entry.GetFileName(version);

            bool confirmed = LocalDataDialogs.ConfirmSave(
                entry.DisplayName,
                version,
                fileName,
                entry.GetFileSize(version),
                entry.GetModifiedUtc(version),
                !entry.IsViewingOldVersion,
                entry.LatestVersion);

            if (!confirmed)
            {
                LocalDataDialogs.ReportCancelled();
                this.SetStatus("Save cancelled");
                return;
            }

            if (entry.Save())
            {
                LocalDataDialogs.ReportSaveSucceeded(
                    entry.DisplayName, version, fileName, entry.GetFileSize(version));
                this.SetStatus($"Saved {entry.DomainId} v{version}");
            }
            else
            {
                LocalDataDialogs.ReportSaveFailed(entry.DisplayName, entry.LastError);
                this.SetStatus($"Save failed: {entry.DomainId}");
            }
        }

        private void DeleteOne(LocalDataEntry entry)
        {
            IReadOnlyList<string> fileNames = entry.GetFileNames();

            if (!LocalDataDialogs.ConfirmDelete(entry.DisplayName, fileNames))
            {
                LocalDataDialogs.ReportCancelled();
                this.SetStatus("Delete cancelled");
                return;
            }

            if (entry.Delete(out int deleted))
            {
                LocalDataDialogs.ReportDeleteSucceeded(entry.DisplayName, deleted);
                this.SetStatus($"Deleted {entry.DomainId}");
                this._loadedCountDirty = true;
            }
            else
            {
                LocalDataDialogs.ReportDeleteFailed(entry.DisplayName, entry.LastError);
            }
        }

        private void LoadAll()
        {
            int loaded = 0;
            List<string> failures = new();

            foreach (LocalDataEntry entry in this._entries)
            {
                if (!entry.HasFiles)
                    continue;

                if (entry.Load())
                    loaded++;
                else
                    failures.Add($"{entry.DomainId} - {entry.LastError}");

                // Only the selected entry keeps a tree alive.
                if (!ReferenceEquals(entry, this._selected))
                    entry.ReleaseTree();
            }

            this._loadedCountDirty = true;
            this.SetStatus($"Loaded {loaded} domain(s)");

            if (failures.Count > 0)
                LocalDataDialogs.ReportBatch("Load All", loaded, loaded + failures.Count, failures);
        }

        private void SaveAll()
        {
            List<LocalDataEntry> loaded = new();
            List<string> lines = new();

            foreach (LocalDataEntry entry in this._entries)
            {
                if (!entry.HasData)
                    continue;

                loaded.Add(entry);
                lines.Add($"{entry.DomainId}  ->  v{entry.LoadedVersion}");
            }

            if (loaded.Count == 0)
            {
                EditorUtility.DisplayDialog("Save All Data",
                    "No domains are loaded, so there is nothing to save.", "OK");
                return;
            }

            if (!LocalDataDialogs.ConfirmSaveAll(lines, loaded.Count, this._entries.Count))
            {
                LocalDataDialogs.ReportCancelled();
                this.SetStatus("Save all cancelled");
                return;
            }

            int saved = 0;
            List<string> failures = new();

            foreach (LocalDataEntry entry in loaded)
            {
                if (entry.Save())
                    saved++;
                else
                    failures.Add($"{entry.DomainId} - {entry.LastError}");
            }

            LocalDataDialogs.ReportBatch("Save All", saved, loaded.Count, failures);
            this.SetStatus($"Saved {saved} of {loaded.Count} domain(s)");
        }

        private void DeleteAll()
        {
            int fileCount = 0;
            int domainCount = 0;

            foreach (LocalDataEntry entry in this._entries)
            {
                int files = entry.GetFileNames().Count;
                if (files == 0)
                    continue;

                fileCount += files;
                domainCount++;
            }

            if (domainCount == 0)
            {
                EditorUtility.DisplayDialog("Delete All Data", "There is no save data to delete.", "OK");
                return;
            }

            if (!LocalDataDialogs.ConfirmDeleteAll(domainCount, fileCount, this._saveFolderPath))
            {
                LocalDataDialogs.ReportCancelled();
                this.SetStatus("Delete all cancelled");
                return;
            }

            int deleted = 0;
            List<string> failures = new();

            foreach (LocalDataEntry entry in this._entries)
            {
                if (entry.GetFileNames().Count == 0)
                    continue;

                if (entry.Delete(out _))
                    deleted++;
                else
                    failures.Add($"{entry.DomainId} - {entry.LastError}");
            }

            LocalDataDialogs.ReportBatch("Delete All", deleted, domainCount, failures);
            this._loadedCountDirty = true;
            this.SetStatus($"Deleted {deleted} domain(s)");
        }

        private void OpenSaveFolder()
        {
            if (!Directory.Exists(this._saveFolderPath))
                Directory.CreateDirectory(this._saveFolderPath);

            EditorUtility.RevealInFinder(this._saveFolderPath);
        }

        // -----------------------------------------------------------------
        // Discovery
        // -----------------------------------------------------------------

        /// <summary>
        /// Finds every save repository via Unity's prebuilt type index.
        /// </summary>
        private void Rescan()
        {
            this._selected?.ReleaseTree();
            this._selected = null;
            this._entries.Clear();

            foreach (Type controllerType in
                     TypeCache.GetTypesWithAttribute<DynamicGameDataControllerAttribute>())
            {
                if (controllerType.IsAbstract)
                    continue;

                DynamicGameDataControllerAttribute attribute =
                    (DynamicGameDataControllerAttribute)Attribute.GetCustomAttribute(
                        controllerType, typeof(DynamicGameDataControllerAttribute));

                if (attribute == null || string.IsNullOrEmpty(attribute.DomainId))
                    continue;

                Type dataType = ResolveDataType(controllerType);
                if (dataType == null)
                {
                    Debug.LogWarning(
                        $"[LocalDataTool] Could not resolve the data type for {controllerType.FullName}. " +
                        "It should derive from DynamicGameDataController<TData>.");
                    continue;
                }

                this._entries.Add(new LocalDataEntry(this._store, this._codec, attribute.DomainId, dataType));
            }

            this._entries.Sort((a, b) => string.CompareOrdinal(a.DomainId, b.DomainId));
            this._loadedCountDirty = true;

            this.ApplyFilter();
            this.SetStatus($"Discovered {this._entries.Count} domain(s)");
        }

        /// <summary>Walks up to <c>DynamicGameDataController&lt;TData&gt;</c> and returns TData.</summary>
        private static Type ResolveDataType(Type controllerType)
        {
            Type current = controllerType.BaseType;

            while (current != null)
            {
                if (current.IsGenericType &&
                    current.GetGenericTypeDefinition() == typeof(DynamicGameDataController<>))
                    return current.GetGenericArguments()[0];

                current = current.BaseType;
            }

            return null;
        }

        private void RestoreSelection()
        {
            string domainId = EditorPrefs.GetString(SelectedDomainPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(domainId))
                return;

            foreach (LocalDataEntry entry in this._entries)
            {
                if (!string.Equals(entry.DomainId, domainId, StringComparison.Ordinal))
                    continue;

                this.Select(entry);
                return;
            }
        }

        private void Select(LocalDataEntry entry)
        {
            if (ReferenceEquals(entry, this._selected))
                return;

            // Exactly one tree alive at a time.
            this._selected?.ReleaseTree();
            this._selected = entry;

            EditorPrefs.SetString(SelectedDomainPrefsKey, entry?.DomainId ?? string.Empty);
            this._needsRepaint = true;
        }

        private void ApplyFilter()
        {
            this._visibleEntries.Clear();

            foreach (LocalDataEntry entry in this._entries)
            {
                if (string.IsNullOrWhiteSpace(this._searchFilter) ||
                    entry.DomainId.IndexOf(this._searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.DisplayName.IndexOf(this._searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    this._visibleEntries.Add(entry);
                }
            }

            // A narrower result set can leave the current page past the end, and a fresh search is
            // read starting from the top regardless.
            this._currentPage = 0;
            this._needsRepaint = true;
        }

        /// <summary>Cached: this used to run a LINQ predicate on every OnGUI event.</summary>
        private int GetLoadedCount()
        {
            if (!this._loadedCountDirty)
                return this._loadedCountCache;

            this._loadedCountCache = 0;
            foreach (LocalDataEntry entry in this._entries)
            {
                if (entry.HasData)
                    this._loadedCountCache++;
            }

            this._loadedCountDirty = false;
            return this._loadedCountCache;
        }

        private void SetStatus(string message)
        {
            this._statusText = message;
            this._needsRepaint = true;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0)
                return "unknown";

            return bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024d:N1} KB";
        }
    }
}