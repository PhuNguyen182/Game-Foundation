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

        private const float MinListWidth = 160f;
        private const float MaxListWidth = 420f;
        private const float SplitterWidth = 4f;

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

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Load All", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    this.LoadAll();

                if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    this.SaveAll();

                GUILayout.Space(12);

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65)))
                    this.Rescan();

                if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(85)))
                    this.OpenSaveFolder();

                GUILayout.FlexibleSpace();

                // Destructive action kept apart from the rest so it is not clicked by reflex.
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                if (GUILayout.Button("Delete All", EditorStyles.toolbarButton, GUILayout.Width(75)))
                    this.DeleteAll();
                GUI.backgroundColor = previousBackground;

                GUILayout.Space(8);

                EditorGUI.BeginChangeCheck();
                this._searchFilter = GUILayout.TextField(
                    this._searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(180));
                if (EditorGUI.EndChangeCheck())
                    this.ApplyFilter();
            }
        }

        // -----------------------------------------------------------------
        // Left pane
        // -----------------------------------------------------------------

        private void DrawDomainList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(this._listWidth)))
            {
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

                foreach (LocalDataEntry entry in this._visibleEntries)
                    this.DrawDomainRow(entry);

                EditorGUILayout.EndScrollView();

                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    $"{this._entries.Count} domain(s), {this.GetLoadedCount()} loaded",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawDomainRow(LocalDataEntry entry)
        {
            bool isSelected = ReferenceEquals(entry, this._selected);

            Rect rect = EditorGUILayout.GetControlRect(false, 34f);
            if (Event.current.type == EventType.Repaint && isSelected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.90f, 0.25f));

            Rect markerRect = new(rect.x + 4f, rect.y + 11f, 10f, 10f);
            GUI.Label(markerRect, this.GetStateGlyph(entry), EditorStyles.boldLabel);

            Rect nameRect = new(rect.x + 18f, rect.y + 2f, rect.width - 22f, 16f);
            GUI.Label(nameRect, entry.DisplayName, EditorStyles.label);

            Rect subRect = new(rect.x + 18f, rect.y + 17f, rect.width - 22f, 14f);
            string subtitle = entry.HasFiles
                ? $"{entry.DomainId}  ·  v{entry.LatestVersion}"
                : $"{entry.DomainId}  ·  no data";
            GUI.Label(subRect, subtitle, EditorStyles.miniLabel);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                this.Select(entry);
                Event.current.Use();
            }
        }

        private GUIContent GetStateGlyph(LocalDataEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.LastError))
                return new GUIContent("✕") { tooltip = entry.LastError };

            if (entry.HasData)
                return new GUIContent("●") { tooltip = "Loaded" };

            return new GUIContent("○") { tooltip = entry.HasFiles ? "Not loaded" : "No save data" };
        }

        private void DrawSplitter()
        {
            Rect splitter = EditorGUILayout.GetControlRect(false,
                GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));

            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);

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