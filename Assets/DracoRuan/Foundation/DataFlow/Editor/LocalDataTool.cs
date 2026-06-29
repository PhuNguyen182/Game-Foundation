using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;
using UnityEditor;
using UnityEngine;

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
    ///
    /// Rendering: standard EditorWindow IMGUI.
    /// Data fields are rendered via Odin Inspector's PropertyTree (in LocalDataEntry).
    /// </summary>
    public class LocalDataTool : EditorWindow
    {
        private const string LocalDataFolder = "GameData";

        // ---------------------------------------------------------------------------
        // Data  (private, not serialized — intentionally transient)
        // ---------------------------------------------------------------------------
        private readonly List<LocalDataEntry>             entries    = new();
        private readonly Dictionary<Type, LocalDataEntry> entryMap   = new();
        private          List<LocalDataEntry>             visibleEntries = new();

        private string  searchFilter = "";
        private string  statusText   = "Ready";
        private Vector2 scrollPos;

        // Cached GUIStyles (lazy-initialised on first OnGUI)
        private GUIStyle _toolbarButtonStyle;

        // ---------------------------------------------------------------------------
        // Menu item
        // ---------------------------------------------------------------------------
        [MenuItem("Tools/Foundations/Local Data Editor/Local Data Manager", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalDataTool>();
            window.titleContent = new GUIContent("🎮 Local Data Manager");
            window.minSize      = new Vector2(700, 500);
            window.Show();
        }

        // ---------------------------------------------------------------------------
        // Lifecycle  (standard EditorWindow — no base class overrides needed)
        // ---------------------------------------------------------------------------
        private void OnEnable()
        {
            // Defer the first scan so Unity is fully initialised
            EditorApplication.delayCall += this.ScanForControllers;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= this.ScanForControllers;

            foreach (var entry in this.entries)
            {
                entry.OnDataChanged  -= this.OnEntryChanged;
                entry.OnEntryDeleted -= this.HandleEntryDeleted;
            }
        }

        // ---------------------------------------------------------------------------
        // Main draw  (standard IMGUI)
        // ---------------------------------------------------------------------------
        private void OnGUI()
        {
            // Lazy-init styles (must be inside OnGUI so GUISkin is available)
            if (this._toolbarButtonStyle == null)
                this._toolbarButtonStyle = EditorStyles.toolbarButton;

            this.DrawToolbar();
            this.DrawStatusBar();
            this.DrawEntries();
        }

        // ---------------------------------------------------------------------------
        // Toolbar
        // ---------------------------------------------------------------------------
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("📥 Load All",    this._toolbarButtonStyle, GUILayout.Width(90)))
                {
                    this.LoadAllData();
                    this.Repaint();
                }
                if (GUILayout.Button("💾 Save All",    this._toolbarButtonStyle, GUILayout.Width(90)))
                {
                    this.SaveAllData();
                    this.Repaint();
                }
                if (GUILayout.Button("🗑️ Delete All", this._toolbarButtonStyle, GUILayout.Width(95)))
                    this.ShowDeleteAllConfirmation();
                if (GUILayout.Button("📁 Open Folder", this._toolbarButtonStyle, GUILayout.Width(100)))
                    this.OpenDataFolder();
                if (GUILayout.Button("🔄 Refresh",     this._toolbarButtonStyle, GUILayout.Width(80)))
                {
                    this.ScanForControllers();
                    this.Repaint();
                }

                GUILayout.FlexibleSpace();

                // Entry counter
                int total    = this.entries.Count;
                int withData = this.entries.Count(e => e.HasData);
                GUILayout.Label($"📊 {total} controller(s)  ({withData} loaded)",
                    EditorStyles.miniLabel);
                GUILayout.Space(8);

                // Search field
                EditorGUI.BeginChangeCheck();
                this.searchFilter = EditorGUILayout.TextField(
                    this.searchFilter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160));
                if (EditorGUI.EndChangeCheck())
                {
                    this.FilterEntries(this.searchFilter);
                    this.Repaint();
                }

                if (GUILayout.Button("✕", this._toolbarButtonStyle, GUILayout.Width(22)))
                    this.ClearSearch();
            }
        }

        // ---------------------------------------------------------------------------
        // Status bar
        // ---------------------------------------------------------------------------
        private void DrawStatusBar()
        {
            var path = Path.Combine(Application.persistentDataPath, LocalDataFolder);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"📁 {path}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(this.statusText, EditorStyles.miniLabel);
            }
        }

        // ---------------------------------------------------------------------------
        // Entry list
        // ---------------------------------------------------------------------------
        private void DrawEntries()
        {
            if (this.visibleEntries.Count == 0)
            {
                GUILayout.Space(16);
                EditorGUILayout.HelpBox(
                    this.entries.Count == 0
                        ? "No DynamicGameDataController found.\nClick '🔄 Refresh' or '📥 Load All' to scan the project."
                        : "No results match the current search.",
                    this.entries.Count == 0 ? MessageType.Info : MessageType.Warning);
                return;
            }

            this.scrollPos = GUILayout.BeginScrollView(this.scrollPos);

            foreach (var entry in this.visibleEntries)
            {
                entry.Draw(this);
                GUILayout.Space(3);
            }

            GUILayout.EndScrollView();
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

            this.FilterEntries(this.searchFilter);
            this.UpdateStatus($"Discovered {this.entries.Count} controller(s)");
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

            this.UpdateStatus(errors > 0
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

            this.UpdateStatus(errors > 0
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
            this.visibleEntries.Clear();
            this.UpdateStatus($"🗑️ Cleared data for {deleted} controller(s)");
            this.Repaint();
        }

        // ---------------------------------------------------------------------------
        // UI helpers
        // ---------------------------------------------------------------------------
        private void FilterEntries(string search)
        {
            this.visibleEntries = string.IsNullOrWhiteSpace(search)
                ? this.entries.ToList()
                : this.entries.Where(e =>
                    e.TypeName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.ControllerKey.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        private void UpdateStatus(string msg)
        {
            this.statusText = $"[{DateTime.Now:HH:mm:ss}]  {msg}";
            this.Repaint();
        }

        private void ClearSearch()
        {
            this.searchFilter = "";
            this.FilterEntries(string.Empty);
            this.Repaint();
        }

        private void OpenDataFolder()
        {
            var path = Path.Combine(Application.persistentDataPath, LocalDataFolder);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        // ---------------------------------------------------------------------------
        // Entry events
        // ---------------------------------------------------------------------------
        private void OnEntryChanged(LocalDataEntry entry)
        {
            this.UpdateStatus($"✏️ Modified: {entry.TypeName}");
            this.Repaint();
        }

        private void HandleEntryDeleted(LocalDataEntry entry)
        {
            this.entries.Remove(entry);
            this.entryMap.Remove(entry.DataType);
            this.FilterEntries(this.searchFilter);
            this.UpdateStatus($"🗑️ Deleted: {entry.TypeName}");
            this.Repaint();
        }
    }
}
