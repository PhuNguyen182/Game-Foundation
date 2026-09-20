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

        [MenuItem("Tools/Foundations/Local Data Editor/Local Data Manager", false, 100)]
        public static void ShowWindow()
        {
            LocalDataTool window = GetWindow<LocalDataTool>();
            window.titleContent = new GUIContent("Local Data");
            window.minSize = new Vector2(720, 420);
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

        private GUIStyle _commandButtonStyle;
        private GUIStyle _searchFieldStyle;

        /// <summary>
        /// A taller, bolder button than <see cref="EditorStyles.toolbarButton"/>, built lazily since
        /// <see cref="GUIStyle"/> construction touches <see cref="GUI.skin"/> and must happen inside
        /// an IMGUI call, not a constructor or <c>OnEnable</c>.
        /// </summary>
        private GUIStyle CommandButtonStyle => this._commandButtonStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            fixedHeight = 26f,
            alignment = TextAnchor.MiddleCenter,
        };

        private GUIStyle SearchFieldStyle => this._searchFieldStyle ??= new GUIStyle(EditorStyles.textField)
        {
            fontSize = 12,
            fixedHeight = 24f,
            padding = new RectOffset(20, 6, 3, 3),
        };

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(32f)))
            {
                GUILayout.Space(4);

                this.DrawCommandButton("📥 Load All", LoadAllColor, GUILayout.Width(96), clicked: this.LoadAll);
                this.DrawCommandButton("💾 Save All", SaveAllColor, GUILayout.Width(96), clicked: this.SaveAll);

                GUILayout.Space(10);

                this.DrawCommandButton("🔄 Refresh", NeutralButtonColor, GUILayout.Width(90), clicked: this.Rescan);
                this.DrawCommandButton("📁 Open Folder", NeutralButtonColor, GUILayout.Width(112),
                    clicked: this.OpenSaveFolder);

                GUILayout.FlexibleSpace();

                this.DrawSearchField();

                GUILayout.Space(10);

                // Destructive action kept apart from the rest, at the far end, so it is never
                // clicked by reflex while reaching for something else.
                this.DrawCommandButton("🗑 Delete All", DeleteAllColor, GUILayout.Width(104),
                    clicked: this.DeleteAll);

                GUILayout.Space(4);
            }
        }

        private void DrawSearchField()
        {
            const float fieldWidth = 240f;

            Rect fieldRect = GUILayoutUtility.GetRect(fieldWidth, 24f, GUILayout.Width(fieldWidth));

            EditorGUI.BeginChangeCheck();
            string newFilter = EditorGUI.TextField(fieldRect, this._searchFilter, this.SearchFieldStyle);
            if (EditorGUI.EndChangeCheck())
            {
                this._searchFilter = newFilter;
                this.ApplyFilter();
            }

            Rect iconRect = new(fieldRect.x + 4f, fieldRect.y + 3f, 16f, 16f);
            GUI.Label(iconRect, "🔍");

            if (string.IsNullOrEmpty(this._searchFilter))
            {
                Rect placeholderRect = new(fieldRect.x + 22f, fieldRect.y, fieldRect.width - 26f, fieldRect.height);
                using (new EditorGUI.DisabledScope(true))
                    GUI.Label(placeholderRect, "Search domains…", EditorStyles.label);
            }
        }

        private void DrawCommandButton(string label, Color tint, GUILayoutOption widthOption, Action clicked)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = tint;

            if (GUILayout.Button(label, this.CommandButtonStyle, widthOption, GUILayout.Height(26f)))
                clicked();

            GUI.backgroundColor = previous;
        }

        // -----------------------------------------------------------------
        // Left pane
        // -----------------------------------------------------------------

        private GUIStyle _entryNameStyle;

        /// <summary>Bold and a size step up from the default label, so a domain's title is the
        /// first thing the eye lands on in each row.</summary>
        private GUIStyle EntryNameStyle => this._entryNameStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
        };

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
                    EditorStyles.miniLabel);
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
            string subtitle = entry.HasFiles
                ? $"{entry.DomainId}   ·   v{entry.LatestVersion}"
                : $"{entry.DomainId}   ·   no data";
            GUI.Label(subRect, subtitle, EditorStyles.miniLabel);

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
                GUILayout.Label(entry.DisplayName, EditorStyles.boldLabel);
                GUILayout.Label(entry.DomainId, EditorStyles.miniLabel);

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
                        GUILayout.Label("none", EditorStyles.miniLabel, GUILayout.Width(110));
                    }

                    GUILayout.FlexibleSpace();

                    if (entry.HasData)
                    {
                        GUILayout.Label(
                            $"{FormatBytes(entry.LoadedSizeBytes)}  ·  rev {entry.LoadedHeader.Revision}  ·  " +
                            entry.GetModifiedUtc(entry.LoadedVersion),
                            EditorStyles.miniLabel);
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
                    if (GUILayout.Button("Load", GUILayout.Width(80), GUILayout.Height(22)))
                        this.LoadVersion(entry, entry.LoadedVersion > 0 ? entry.LoadedVersion : 0);
                }

                using (new EditorGUI.DisabledScope(!entry.HasData))
                {
                    if (GUILayout.Button("Save", GUILayout.Width(80), GUILayout.Height(22)))
                        this.SaveOne(entry);
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!entry.HasFiles))
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                    if (GUILayout.Button("Delete", GUILayout.Width(80), GUILayout.Height(22)))
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