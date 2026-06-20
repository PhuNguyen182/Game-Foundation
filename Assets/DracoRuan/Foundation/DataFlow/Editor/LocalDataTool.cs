using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Local Data Manager — Unity Editor tool for managing file-based persistent game data.
    /// Automatically discovers all classes decorated with [DynamicGameDataControllerAttribute],
    /// resolves the TData generic type, and presents a UI to load, edit, save or delete entries.
    ///
    /// Data is stored as MessagePack binary files (.data) under:
    ///   {Application.persistentDataPath}/GameData/{TypeName}_v{version}.data
    ///
    /// The tool always loads the highest available version, mirroring the runtime behaviour of
    /// DynamicGameDataController.GetMostRecentDataVersion().
    /// </summary>
    public class LocalDataTool : EditorWindow
    {
        private const string LocalDataFolder = "GameData";

        // ---------------------------------------------------------------------------
        // Cached UI elements
        // ---------------------------------------------------------------------------
        private VisualElement root;
        private ScrollView    dataScrollView;
        private VisualElement dataContainer;
        private VisualElement emptyState;
        private Button        loadAllButton;
        private Button        saveAllButton;
        private Button        deleteAllButton;
        private Button        openFolderButton;
        private Label         dataCountLabel;
        private Label         lastActionLabel;
        private Label         fileCountLabel;
        private Label         folderPathLabel;
        private TextField     searchField;
        private Button        clearSearchButton;

        // ---------------------------------------------------------------------------
        // Data
        // ---------------------------------------------------------------------------
        private readonly List<LocalDataEntry>          entries  = new();
        private readonly Dictionary<Type, LocalDataEntry> entryMap = new();

        // ---------------------------------------------------------------------------
        // Menu items
        // ---------------------------------------------------------------------------
        [MenuItem("Tools/Foundations/Local Data Editor/Local Data Manager", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalDataTool>();
            window.titleContent = new GUIContent("🎮 Local Data Manager");
            window.minSize = new Vector2(700, 500);
            window.Show();
        }

        // ---------------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------------
        public void CreateGUI()
        {
            // ---- Load UXML by asset name (no hard-coded path) ----
            var uxmlGuids = AssetDatabase.FindAssets("LocalDataTool t:VisualTreeAsset");
            if (uxmlGuids.Length == 0)
            {
                Debug.LogError("[LocalDataTool] LocalDataTool.uxml not found in project.");
                return;
            }
            var uxmlPath  = AssetDatabase.GUIDToAssetPath(uxmlGuids[0]);
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[LocalDataTool] Failed to load LocalDataTool.uxml at: {uxmlPath}");
                return;
            }

            this.root = visualTree.CloneTree();

            // ---- Load USS by asset name (no hard-coded path) ----
            var ussGuids = AssetDatabase.FindAssets("LocalDataTool t:StyleSheet");
            if (ussGuids.Length > 0)
            {
                var ussPath    = AssetDatabase.GUIDToAssetPath(ussGuids[0]);
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
                if (styleSheet != null)
                    this.root.styleSheets.Add(styleSheet);
            }

            this.rootVisualElement.Add(this.root);
            this.CacheUIElements();
            this.BindEvents();
            this.UpdateUI();
        }

        private void OnDisable()
        {
            foreach (var entry in this.entries)
            {
                entry.OnDataChanged  -= this.OnEntryChanged;
                entry.OnEntryDeleted -= this.HandleEntryDeleted;
            }
        }

        // ---------------------------------------------------------------------------
        // Cache + Bind
        // ---------------------------------------------------------------------------
        private void CacheUIElements()
        {
            this.dataScrollView    = this.root.Q<ScrollView>("data-scroll-view");
            this.dataContainer     = this.root.Q<VisualElement>("data-container");
            this.emptyState        = this.root.Q<VisualElement>("empty-state");
            this.loadAllButton     = this.root.Q<Button>("load-all-button");
            this.saveAllButton     = this.root.Q<Button>("save-all-button");
            this.deleteAllButton   = this.root.Q<Button>("delete-all-button");
            this.openFolderButton  = this.root.Q<Button>("open-folder-button");
            this.dataCountLabel    = this.root.Q<Label>("data-count-label");
            this.lastActionLabel   = this.root.Q<Label>("last-action-label");
            this.fileCountLabel    = this.root.Q<Label>("file-count-label");
            this.folderPathLabel   = this.root.Q<Label>("folder-path-label");
            this.searchField       = this.root.Q<TextField>("search-field");
            this.clearSearchButton = this.root.Q<Button>("clear-search-button");

            this.RefreshFolderPathLabel();
        }

        private void BindEvents()
        {
            this.loadAllButton?.RegisterCallback<ClickEvent>(_ => this.LoadAllData());
            this.saveAllButton?.RegisterCallback<ClickEvent>(_ => this.SaveAllData());
            this.deleteAllButton?.RegisterCallback<ClickEvent>(_ => this.ShowDeleteAllConfirmation());
            this.openFolderButton?.RegisterCallback<ClickEvent>(_ => this.OpenDataFolder());
            this.searchField?.RegisterValueChangedCallback(evt => this.FilterEntries(evt.newValue));
            this.clearSearchButton?.RegisterCallback<ClickEvent>(_ => this.ClearSearch());

            // Defer initial scan so the window is fully initialised first
            EditorApplication.delayCall += this.ScanForControllers;
        }

        // ---------------------------------------------------------------------------
        // Controller discovery
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Scans all loaded assemblies for concrete classes decorated with
        /// [DynamicGameDataControllerAttribute], resolves TData via reflection, and
        /// creates a LocalDataEntry for each discovered controller.
        /// </summary>
        private void ScanForControllers()
        {
            foreach (var entry in this.entries)
            {
                entry.OnDataChanged  -= this.OnEntryChanged;
                entry.OnEntryDeleted -= this.HandleEntryDeleted;
            }
            this.entries.Clear();
            this.entryMap.Clear();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try   { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (!type.IsClass || type.IsAbstract) continue;

                    var attr = type.GetCustomAttribute<DynamicGameDataControllerAttribute>();
                    if (attr == null) continue;

                    var dataType = ResolveDataType(type);
                    if (dataType == null)
                    {
                        Debug.LogWarning($"[LocalDataTool] Cannot resolve TData for controller: {type.FullName}");
                        continue;
                    }

                    var entry = new LocalDataEntry(dataType, attr.DataControllerKey);
                    entry.OnDataChanged  += this.OnEntryChanged;
                    entry.OnEntryDeleted += this.HandleEntryDeleted;
                    this.entries.Add(entry);
                    this.entryMap[dataType] = entry;
                }
            }

            this.UpdateUI();
            this.UpdateLastAction($"Discovered {this.entries.Count} controller(s)");
            Debug.Log($"[LocalDataTool] Discovered {this.entries.Count} DynamicGameDataController(s).");
        }

        /// <summary>
        /// Walks up the inheritance chain of <paramref name="controllerType"/> until it reaches
        /// <c>DynamicGameDataController&lt;TData&gt;</c> and returns the <c>TData</c> generic argument.
        /// </summary>
        private static Type ResolveDataType(Type controllerType)
        {
            var current = controllerType.BaseType;
            while (current != null)
            {
                if (current.IsGenericType &&
                    current.GetGenericTypeDefinition() == typeof(DynamicGameDataController<>))
                    return current.GetGenericArguments()[0];
                current = current.BaseType;
            }
            return null;
        }

        // ---------------------------------------------------------------------------
        // Load / Save / Delete All
        // ---------------------------------------------------------------------------
        private void LoadAllData()
        {
            this.ScanForControllers();

            int loaded = 0, errors = 0;
            foreach (var entry in this.entries)
            {
                try   { entry.LoadData(); loaded++; }
                catch (Exception ex)
                {
                    Debug.LogError($"[LocalDataTool] Load failed for {entry.TypeName}: {ex.Message}");
                    errors++;
                }
            }

            this.UpdateDataCount();
            this.UpdateLastAction(errors > 0
                ? $"Loaded {loaded} entries ({errors} errors)"
                : $"✅ Loaded {loaded} entries");
        }

        private void SaveAllData()
        {
            int saved = 0, errors = 0;
            foreach (var entry in this.entries.Where(e => e.HasData))
            {
                try   { entry.SaveData(); saved++; }
                catch (Exception ex)
                {
                    Debug.LogError($"[LocalDataTool] Save failed for {entry.TypeName}: {ex.Message}");
                    errors++;
                }
            }

            this.UpdateLastAction(errors > 0
                ? $"Saved {saved} entries ({errors} errors)"
                : $"✅ Saved {saved} entries");
        }

        private void ShowDeleteAllConfirmation()
        {
            bool ok = EditorUtility.DisplayDialog(
                "🗑️ Delete All Data",
                "Delete ALL persistent data files managed by this tool?\n\nThis cannot be undone.",
                "Delete All", "Cancel");
            if (ok) this.DeleteAllData();
        }

        private void DeleteAllData()
        {
            int deleted = 0;
            foreach (var entry in this.entries.ToList())
            {
                try   { entry.DeleteAllVersions(); deleted++; }
                catch (Exception ex)
                {
                    Debug.LogError($"[LocalDataTool] Delete failed for {entry.TypeName}: {ex.Message}");
                }
            }

            this.entries.Clear();
            this.entryMap.Clear();
            this.UpdateUI();
            this.UpdateLastAction($"🗑️ Cleared data for {deleted} controller(s)");
        }

        // ---------------------------------------------------------------------------
        // UI
        // ---------------------------------------------------------------------------
        private void UpdateUI()
        {
            this.dataContainer?.Clear();

            bool hasEntries = this.entries.Count > 0;
            if (this.emptyState    != null) this.emptyState.style.display    = hasEntries ? DisplayStyle.None : DisplayStyle.Flex;
            if (this.dataScrollView != null) this.dataScrollView.style.display = hasEntries ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasEntries)
                this.FilterEntries(this.searchField?.value ?? string.Empty);

            this.UpdateDataCount();
        }

        private void FilterEntries(string search)
        {
            this.dataContainer?.Clear();

            IEnumerable<LocalDataEntry> visible = string.IsNullOrWhiteSpace(search)
                ? this.entries
                : this.entries.Where(e =>
                    e.TypeName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.ControllerKey.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var entry in visible)
                this.dataContainer?.Add(entry.CreateUI());

            this.UpdateDataCount();
        }

        private void UpdateDataCount()
        {
            int total    = this.entries.Count;
            int withData = this.entries.Count(e => e.HasData);

            if (this.dataCountLabel != null)
                this.dataCountLabel.text = $"📊 {total} controller(s)  ({withData} loaded)";

            if (this.fileCountLabel != null)
                this.fileCountLabel.text = $"{withData}/{total}";
        }

        private void UpdateLastAction(string msg)
        {
            if (this.lastActionLabel != null) this.lastActionLabel.text = msg;
        }

        private void RefreshFolderPathLabel()
        {
            if (this.folderPathLabel == null) return;
            var path = Path.Combine(Application.persistentDataPath, LocalDataFolder);
            this.folderPathLabel.text = $"📁 {path}";
        }

        private void OpenDataFolder()
        {
            var path = Path.Combine(Application.persistentDataPath, LocalDataFolder);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private void ClearSearch()
        {
            if (this.searchField != null) this.searchField.value = string.Empty;
            this.FilterEntries(string.Empty);
        }

        // ---------------------------------------------------------------------------
        // Entry events
        // ---------------------------------------------------------------------------
        private void OnEntryChanged(LocalDataEntry entry)
        {
            this.UpdateDataCount();
            this.UpdateLastAction($"✏️ Modified: {entry.TypeName}");
        }

        private void HandleEntryDeleted(LocalDataEntry entry)
        {
            this.entries.Remove(entry);
            this.entryMap.Remove(entry.DataType);
            this.FilterEntries(this.searchField?.value ?? string.Empty);
            this.UpdateLastAction($"🗑️ Deleted: {entry.TypeName}");
        }
    }
}
