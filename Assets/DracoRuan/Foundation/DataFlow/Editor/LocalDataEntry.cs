using System;
using System.IO;
using System.Text.RegularExpressions;
using MessagePack;
using Newtonsoft.Json;
using DracoRuan.Foundation.DataFlow.LocalData;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Represents a single file-based data entry for a DynamicGameDataController.
    /// Reads/writes binary MessagePack .data files, automatically loading the highest available version.
    ///
    /// The data object is rendered using Odin Inspector's <see cref="PropertyTree"/>,
    /// which supports all common types including Dictionary, List, nested objects,
    /// and SerializedDictionary from the AYellowpaper.SerializedCollections plugin.
    /// </summary>
    public class LocalDataEntry
    {
        // ---------------------------------------------------------------------------
        // Constants
        // ---------------------------------------------------------------------------
        private const int MaxVersionScan = 100;
        private const string LocalDataFolder = "GameData";
        private const string FileExtension = ".data";

        // ---------------------------------------------------------------------------
        // Fields
        // ---------------------------------------------------------------------------
        private readonly Type dataType;
        private readonly string controllerKey;
        private readonly string baseSaveFolder;

        private object currentData;
        private int loadedVersion = 1;
        private string statusText = "—";
        private bool isExpanded;
        private PropertyTree propertyTree;

        // ---------------------------------------------------------------------------
        // Shared styles (static, lazy-initialised during OnGUI)
        // ---------------------------------------------------------------------------
        private static GUIStyle _entryBoxStyle;
        private static GUIStyle _entryTitleStyle;
        private static GUIStyle _keyLabelStyle;
        private static GUIStyle _contentBoxStyle;
        private static GUIStyle _statusLabelStyle;
        private static readonly Color DeleteBtnColor = new Color(1f, 0.38f, 0.38f, 1f);

        // ---------------------------------------------------------------------------
        // Properties
        // ---------------------------------------------------------------------------
        public Type DataType => this.dataType;
        public string TypeName => this.dataType.Name;
        public string ControllerKey => this.controllerKey;
        public bool HasData => this.currentData != null;

        public event Action<LocalDataEntry> OnDataChanged;
        public event Action<LocalDataEntry> OnEntryDeleted;

        // ---------------------------------------------------------------------------
        // Constructor
        // ---------------------------------------------------------------------------
        public LocalDataEntry(Type dataType, string controllerKey)
        {
            this.dataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            this.controllerKey = controllerKey ?? dataType.Name;
            this.baseSaveFolder = Path.Combine(Application.persistentDataPath, LocalDataFolder);
        }

        // ---------------------------------------------------------------------------
        // Version helpers
        // ---------------------------------------------------------------------------
        private string GetFilePath(int version) =>
            Path.Combine(this.baseSaveFolder, $"{this.dataType.Name}_v{version}{FileExtension}");

        /// <summary>
        /// Scans from MaxVersionScan down to 1 and returns the highest version that has a saved file.
        /// Returns 1 if no file is found (mirrors DynamicGameDataController.GetMostRecentDataVersion).
        /// </summary>
        private int GetMostRecentVersion()
        {
            for (int v = MaxVersionScan; v >= 1; v--)
                if (File.Exists(this.GetFilePath(v)))
                    return v;
            return 1;
        }

        // ---------------------------------------------------------------------------
        // Load / Save / Delete
        // ---------------------------------------------------------------------------

        /// <summary>Loads data from the most recent version file using MessagePack.</summary>
        public void LoadData()
        {
            try
            {
                int version = this.GetMostRecentVersion();
                string path = this.GetFilePath(version);

                if (!File.Exists(path))
                {
                    this.currentData = Activator.CreateInstance(this.dataType);
                    this.loadedVersion = 1;
                    this.statusText = "⚠️ No saved data (defaults)";
                }
                else
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    this.currentData = MessagePackSerializer.Deserialize(this.dataType, bytes);
                    this.loadedVersion = version;
                    this.statusText = $"✅ Loaded v{version}";
                }

                this.RebuildPropertyTree();
                this.isExpanded = true;
                this.OnDataChanged?.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataEntry] Error loading {this.dataType.Name}: {ex.Message}");
                this.statusText = "❌ Load failed";
            }
        }

        /// <summary>
        /// Applies any pending Odin property changes to the data object, then serializes
        /// and writes to disk using MessagePack.
        /// Version is taken from IGameData.DataVersion when available, otherwise uses loadedVersion.
        /// </summary>
        public void SaveData()
        {
            try
            {
                if (this.currentData == null)
                {
                    this.statusText = "⚠️ No data to save";
                    return;
                }

                // Flush any in-progress Odin edits to the underlying object
                this.propertyTree?.ApplyChanges();

                // Prefer IGameData.DataVersion so the save key matches the runtime controller
                int version = this.loadedVersion;
                if (this.currentData is IGameData gameData && gameData.DataVersion > 0)
                    version = gameData.DataVersion;

                if (!Directory.Exists(this.baseSaveFolder))
                    Directory.CreateDirectory(this.baseSaveFolder);

                string path = this.GetFilePath(version);
                byte[] bytes = MessagePackSerializer.Serialize(this.dataType, this.currentData);
                File.WriteAllBytes(path, bytes);

                this.loadedVersion = version;
                this.statusText = $"✅ Saved v{version}";
                Debug.Log($"[LocalDataEntry] Saved {this.dataType.Name} v{version} → {path}");
                this.OnDataChanged?.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataEntry] Error saving {this.dataType.Name}: {ex.Message}");
                this.statusText = "❌ Save failed";
            }
        }

        /// <summary>Deletes all versioned .data files for this data type and notifies the parent tool.</summary>
        public void DeleteAllVersions()
        {
            try
            {
                bool any = false;
                for (int v = 1; v <= MaxVersionScan; v++)
                {
                    string path = this.GetFilePath(v);
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                    any = true;
                }

                this.currentData = null;
                this.loadedVersion = 1;
                this.statusText = any ? "🗑️ Deleted" : "⚠️ No files found";

                this.propertyTree?.Dispose();
                this.propertyTree = null;

                this.OnEntryDeleted?.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataEntry] Error deleting {this.dataType.Name}: {ex.Message}");
                this.statusText = "❌ Delete failed";
            }
        }

        // ---------------------------------------------------------------------------
        // Odin PropertyTree
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Recreates the Odin <see cref="PropertyTree"/> from the current data object.
        /// Called after every Load so the tree reflects the freshly deserialised state.
        /// </summary>
        private void RebuildPropertyTree()
        {
            this.propertyTree?.Dispose();
            this.propertyTree = null;

            if (this.currentData != null)
                this.propertyTree = PropertyTree.Create(this.currentData);
        }

        // ---------------------------------------------------------------------------
        // Draw  (called from LocalDataTool.OnGUI per-frame)
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Draws this entry as a collapsible card.
        /// The header shows the type name, controller key, version, status and action buttons.
        /// When expanded, the data object is rendered by Odin's PropertyTree — which automatically
        /// applies the correct drawer for every field type including Dictionary, List,
        /// SerializedDictionary, nested objects, enums, vectors, colors, etc.
        /// </summary>
        public void Draw(EditorWindow window)
        {
            EnsureStyles();

            using (new EditorGUILayout.VerticalScope(_entryBoxStyle))
            {
                this.DrawHeader(window);

                if (this.isExpanded)
                {
                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.VerticalScope(_contentBoxStyle))
                    {
                        this.DrawDataContent(window);
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Header row
        // ---------------------------------------------------------------------------
        private void DrawHeader(EditorWindow window)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // ---- Expand / collapse toggle ----
                string arrow = this.isExpanded ? "▼" : "▶";
                if (GUILayout.Button(arrow, EditorStyles.miniButtonLeft,
                        GUILayout.Width(24), GUILayout.Height(20)))
                {
                    this.isExpanded = !this.isExpanded;
                    window.Repaint();
                }

                // ---- Type name ----
                GUILayout.Label($"📄 {this.PrettyName(this.dataType.Name)}", _entryTitleStyle);

                GUILayout.FlexibleSpace();

                // ---- Controller key ----
                GUILayout.Label($"🔑 {this.controllerKey}", _keyLabelStyle, GUILayout.Width(180));

                // ---- Version badge ----
                if (this.HasData)
                    GUILayout.Label($"v{this.loadedVersion}",
                        EditorStyles.centeredGreyMiniLabel, GUILayout.Width(36));

                // ---- Status ----
                GUILayout.Label(this.statusText, _statusLabelStyle, GUILayout.Width(170));

                // ---- Action buttons ----
                if (GUILayout.Button("📥 Load", EditorStyles.miniButton, GUILayout.Width(65)))
                {
                    this.LoadData();
                    window.Repaint();
                }

                if (GUILayout.Button("💾 Save", EditorStyles.miniButton, GUILayout.Width(65)))
                {
                    this.SaveData();
                    window.Repaint();
                }

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = DeleteBtnColor;
                if (GUILayout.Button("🗑️", EditorStyles.miniButton, GUILayout.Width(28)))
                    this.ShowDeleteConfirmation();
                GUI.backgroundColor = prevBg;
            }
        }

        // ---------------------------------------------------------------------------
        // Data content (Odin PropertyTree)
        // ---------------------------------------------------------------------------
        private void DrawDataContent(EditorWindow window)
        {
            if (this.currentData == null)
            {
                EditorGUILayout.HelpBox(
                    "No data loaded. Click '📥 Load' to load data from disk.",
                    MessageType.Info);
                return;
            }

            // Lazily build the tree in case the entry was loaded before the first Draw call
            if (this.propertyTree == null)
                this.RebuildPropertyTree();

            if (this.propertyTree != null)
            {
                // Draw all properties with Odin's full inspector.
                // Odin automatically applies the correct drawer per type:
                //   • Primitives           → standard fields
                //   • enum                 → dropdown
                //   • List<T>              → reorderable list with add/remove
                //   • Dictionary<K,V>      → key-value table with add/remove
                //   • SerializedDictionary → AYellowpaper custom drawer
                //   • Nested types         → foldout groups
                //   • Vector / Color       → Unity widget
                EditorGUI.BeginChangeCheck();
                this.propertyTree.Draw(false);
                if (EditorGUI.EndChangeCheck())
                {
                    this.OnDataChanged?.Invoke(this);
                    window.Repaint();
                }
            }
            else
            {
                // Fallback — only reached if PropertyTree.Create() fails for unusual types
                EditorGUILayout.HelpBox(
                    "Unable to build Odin PropertyTree for this type. Showing raw JSON.",
                    MessageType.Warning);
                try
                {
                    string json = JsonConvert.SerializeObject(this.currentData, Formatting.Indented);
                    EditorGUILayout.TextArea(json, GUILayout.ExpandHeight(true), GUILayout.MinHeight(60));
                }
                catch (Exception ex)
                {
                    EditorGUILayout.HelpBox($"Serialization error: {ex.Message}", MessageType.Error);
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Delete confirmation
        // ---------------------------------------------------------------------------
        private void ShowDeleteConfirmation()
        {
            bool ok = EditorUtility.DisplayDialog(
                $"Delete {this.dataType.Name}",
                $"Delete all saved data files for '{this.dataType.Name}'?\n\nThis cannot be undone.",
                "Delete", "Cancel");
            if (ok) this.DeleteAllVersions();
        }

        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        /// <summary>Inserts a space before each capital letter sequence (e.g. MyFieldName → My Field Name).</summary>
        private string PrettyName(string name) =>
            Regex.Replace(name, "(\\B[A-Z])", " $1");

        // ---------------------------------------------------------------------------
        // Static style initialisation  (must be called inside OnGUI / Draw)
        // ---------------------------------------------------------------------------
        private static void EnsureStyles()
        {
            if (_entryBoxStyle != null) return;

            _entryBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(6, 6, 5, 5),
                margin = new RectOffset(4, 4, 2, 2),
            };

            _entryTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
            };

            _keyLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Italic,
            };

            _contentBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 2, 0),
            };

            _statusLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
            };
        }

        public class GameDataOdinAttributeProcessor : OdinAttributeProcessor
        {
            public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty,
                System.Reflection.MemberInfo member)
            {
                if (parentProperty.Tree.WeakTargets != null && parentProperty.Tree.WeakTargets.Count > 0)
                {
                    if (parentProperty.Tree.WeakTargets[0] is IGameData) return true;
                }

                return false;
            }

            public override void ProcessChildMemberAttributes(InspectorProperty parentProperty,
                System.Reflection.MemberInfo member, System.Collections.Generic.List<Attribute> attributes)
            {
                bool isPublic = false;

                if (member is System.Reflection.PropertyInfo pi && pi.GetMethod != null && pi.GetMethod.IsPublic)
                    isPublic = true;
                else if (member is System.Reflection.FieldInfo fi && fi.IsPublic)
                    isPublic = true;

                if (isPublic)
                {
                    // Add [ShowInInspector] if not ignored and not already shown
                    if (!attributes.Exists(x => x is Sirenix.OdinInspector.ShowInInspectorAttribute ||
                                                x.GetType().Name.Contains("IgnoreMember")))
                    {
                        attributes.Add(new Sirenix.OdinInspector.ShowInInspectorAttribute());
                    }
                }
            }
        }
    }
}
