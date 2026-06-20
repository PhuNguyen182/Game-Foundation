using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using MessagePack;
using DracoRuan.Foundation.DataFlow.LocalData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Represents a single file-based data entry for a DynamicGameDataController.
    /// Reads/writes binary MessagePack .data files, automatically loading the highest available version.
    /// </summary>
    public class LocalDataEntry
    {
        // ---------------------------------------------------------------------------
        // Constants
        // ---------------------------------------------------------------------------
        private const int MaxVersionScan = 20;
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

        private VisualElement contentContainer;
        private Label statusLabel;
        private Label versionLabel;
        private readonly Dictionary<string, VisualElement> propertyFields = new();

        // ---------------------------------------------------------------------------
        // Properties
        // ---------------------------------------------------------------------------
        public Type DataType        => this.dataType;
        public string TypeName      => this.dataType.Name;
        public string ControllerKey => this.controllerKey;
        public object CurrentData   => this.currentData;
        public bool HasData         => this.currentData != null;

        public event Action<LocalDataEntry> OnDataChanged;
        public event Action<LocalDataEntry> OnEntryDeleted;

        // ---------------------------------------------------------------------------
        // Constructor
        // ---------------------------------------------------------------------------
        public LocalDataEntry(Type dataType, string controllerKey)
        {
            this.dataType      = dataType ?? throw new ArgumentNullException(nameof(dataType));
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
            {
                if (File.Exists(this.GetFilePath(v)))
                    return v;
            }
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
                string path  = this.GetFilePath(version);

                if (!File.Exists(path))
                {
                    this.currentData   = Activator.CreateInstance(this.dataType);
                    this.loadedVersion = 1;
                    this.UpdateVersionLabel(version, exists: false);
                    this.UpdateStatus("No saved data (defaults)", "status-warning");
                    this.RefreshFieldsFromData();
                    return;
                }

                byte[] bytes       = File.ReadAllBytes(path);
                this.currentData   = MessagePackSerializer.Deserialize(this.dataType, bytes);
                this.loadedVersion = version;
                this.UpdateVersionLabel(version, exists: true);
                this.UpdateStatus($"Loaded v{version}", "status-success");
                this.RefreshFieldsFromData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataEntry] Error loading {this.dataType.Name}: {ex.Message}");
                this.UpdateStatus("Load failed", "status-error");
            }
        }

        /// <summary>
        /// Collects values from UI, then serializes and writes to disk using MessagePack.
        /// Version is taken from IGameData.DataVersion when available, otherwise uses loadedVersion.
        /// </summary>
        public void SaveData()
        {
            try
            {
                if (this.currentData == null)
                {
                    this.UpdateStatus("No data to save", "status-warning");
                    return;
                }

                this.CollectDataFromUI();

                // Prefer IGameData.DataVersion so save key matches the runtime controller
                int version = this.loadedVersion;
                if (this.currentData is IGameData gameData && gameData.DataVersion > 0)
                    version = gameData.DataVersion;

                if (!Directory.Exists(this.baseSaveFolder))
                    Directory.CreateDirectory(this.baseSaveFolder);

                string path  = this.GetFilePath(version);
                byte[] bytes = MessagePackSerializer.Serialize(this.dataType, this.currentData);
                File.WriteAllBytes(path, bytes);

                this.loadedVersion = version;
                this.UpdateVersionLabel(version, exists: true);
                this.UpdateStatus($"Saved v{version}", "status-success");
                Debug.Log($"[LocalDataEntry] Saved {this.dataType.Name} v{version} → {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataEntry] Error saving {this.dataType.Name}: {ex.Message}");
                this.UpdateStatus("Save failed", "status-error");
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

                this.currentData   = null;
                this.loadedVersion = 1;
                this.UpdateStatus(any ? "Deleted" : "No files found", "status-info");
                this.OnEntryDeleted?.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDataEntry] Error deleting {this.dataType.Name}: {ex.Message}");
                this.UpdateStatus("Delete failed", "status-error");
            }
        }

        // ---------------------------------------------------------------------------
        // UI creation
        // ---------------------------------------------------------------------------

        /// <summary>Builds and returns the full UI element for this entry.</summary>
        public VisualElement CreateUI()
        {
            var container = new VisualElement();
            container.AddToClassList("data-entry");
            container.AddToClassList("file-data-entry");
            container.name = $"entry-{this.dataType.Name}";

            container.Add(this.BuildHeader());

            this.contentContainer = new VisualElement();
            this.contentContainer.AddToClassList("data-entry-content");
            this.contentContainer.style.display = DisplayStyle.None;
            container.Add(this.contentContainer);

            this.BuildPropertyFields();
            return container;
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("data-entry-header");
            header.AddToClassList("file-data-header");

            // --- Title row ---
            var titleRow = new VisualElement();
            titleRow.style.flexDirection  = FlexDirection.Row;
            titleRow.style.alignItems     = Align.Center;
            titleRow.style.justifyContent = Justify.SpaceBetween;

            var titleLbl = new Label($"📄 {this.dataType.Name}");
            titleLbl.AddToClassList("data-entry-title");
            titleLbl.AddToClassList("file-data-title");

            var expandBtn = new Button(this.ToggleExpansion) { text = "▶" };
            expandBtn.AddToClassList("expand-button");
            expandBtn.name = "expand-button";

            titleRow.Add(titleLbl);
            titleRow.Add(expandBtn);

            // --- Info row (controller key + version) ---
            var infoRow = new VisualElement();
            infoRow.style.flexDirection = FlexDirection.Row;
            infoRow.style.marginTop     = 4;
            infoRow.style.marginBottom  = 2;

            var keyLbl = new Label($"🔑 {this.controllerKey}");
            keyLbl.AddToClassList("file-path-label");
            keyLbl.style.flexGrow = 1;

            this.versionLabel = new Label("—");
            this.versionLabel.AddToClassList("file-path-label");

            infoRow.Add(keyLbl);
            infoRow.Add(this.versionLabel);

            // --- Controls row ---
            var controlsRow = new VisualElement();
            controlsRow.style.flexDirection  = FlexDirection.Row;
            controlsRow.style.alignItems     = Align.Center;
            controlsRow.style.justifyContent = Justify.SpaceBetween;

            this.statusLabel = new Label("—");
            this.statusLabel.AddToClassList("status-label");

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;

            var loadBtn = new Button(this.LoadData) { text = "📥 Load" };
            loadBtn.AddToClassList("small-button");
            loadBtn.AddToClassList("load-button");

            var saveBtn = new Button(this.SaveData) { text = "💾 Save" };
            saveBtn.AddToClassList("small-button");
            saveBtn.AddToClassList("save-button");

            var deleteBtn = new Button(this.ShowDeleteConfirmation) { text = "🗑️ Delete" };
            deleteBtn.AddToClassList("small-button");
            deleteBtn.AddToClassList("delete-button");

            btnRow.Add(loadBtn);
            btnRow.Add(saveBtn);
            btnRow.Add(deleteBtn);

            controlsRow.Add(this.statusLabel);
            controlsRow.Add(btnRow);

            header.Add(titleRow);
            header.Add(infoRow);
            header.Add(controlsRow);
            return header;
        }

        private void ToggleExpansion()
        {
            if (this.contentContainer == null) return;
            bool visible = this.contentContainer.style.display == DisplayStyle.Flex;
            this.contentContainer.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;

            var btn = this.contentContainer.parent?.Q<Button>("expand-button");
            if (btn != null) btn.text = visible ? "▶" : "▼";
        }

        private void BuildPropertyFields()
        {
            this.propertyFields.Clear();
            if (this.contentContainer == null) return;

            int count = 0;

            foreach (var prop in this.dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!this.IsSupportedType(prop.PropertyType) || !prop.CanRead) continue;
                var el = this.CreateFieldElement(prop.Name, prop.PropertyType);
                this.propertyFields[prop.Name] = el;
                this.contentContainer.Add(el);
                count++;
            }

            foreach (var field in this.dataType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!this.IsSupportedType(field.FieldType) || field.IsInitOnly) continue;
                var el = this.CreateFieldElement(field.Name, field.FieldType);
                this.propertyFields[field.Name] = el;
                this.contentContainer.Add(el);
                count++;
            }

            if (count == 0)
            {
                this.contentContainer.Add(new Label("⚠️ No editable primitive fields found")
                    { style = { color = Color.yellow } });
            }
        }

        private VisualElement CreateFieldElement(string name, Type type)
        {
            var container = new VisualElement();
            container.AddToClassList("property-field");
            container.name = $"field-{name}";

            var label = new Label(this.PrettyName(name));
            label.AddToClassList("property-label");
            container.Add(label);

            var input = this.CreateInput(type);
            if (input != null)
            {
                input.AddToClassList("property-value");
                input.name = $"input-{name}";
                this.BindChange(input);
                container.Add(input);
            }
            else
            {
                container.Add(new Label($"⚠️ {type.Name}") { style = { color = Color.yellow } });
            }

            return container;
        }

        private VisualElement CreateInput(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (t == typeof(int))        return new IntegerField();
            if (t == typeof(long))       return new LongField();
            if (t == typeof(float))      return new FloatField();
            if (t == typeof(double))     return new DoubleField();
            if (t == typeof(string))     return new TextField();
            if (t == typeof(bool))       return new Toggle();
            if (t == typeof(Vector2))    return new Vector2Field();
            if (t == typeof(Vector3))    return new Vector3Field();
            if (t == typeof(Vector2Int)) return new Vector2IntField();
            if (t == typeof(Vector3Int)) return new Vector3IntField();
            if (t == typeof(Color))      return new ColorField();
            if (t.IsEnum)
            {
                try   { return new EnumField((Enum)Activator.CreateInstance(t)); }
                catch { return new TextField(); }
            }
            return null;
        }

        private void BindChange(VisualElement input)
        {
            switch (input)
            {
                case IntegerField    f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case LongField       f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case FloatField      f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case DoubleField     f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case TextField       f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case Toggle          f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case Vector2Field    f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case Vector3Field    f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case Vector2IntField f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case Vector3IntField f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case ColorField      f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
                case EnumField       f: f.RegisterValueChangedCallback(_ => this.OnFieldChanged()); break;
            }
        }

        private void OnFieldChanged() => this.OnDataChanged?.Invoke(this);

        // ---------------------------------------------------------------------------
        // Sync data ↔ UI
        // ---------------------------------------------------------------------------
        private void RefreshFieldsFromData()
        {
            if (this.currentData == null) return;

            foreach (var prop in this.dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!this.propertyFields.TryGetValue(prop.Name, out var el)) continue;
                var input = el.Q<VisualElement>($"input-{prop.Name}");
                if (input != null) this.SetValue(input, prop.GetValue(this.currentData));
            }

            foreach (var field in this.dataType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!this.propertyFields.TryGetValue(field.Name, out var el)) continue;
                var input = el.Q<VisualElement>($"input-{field.Name}");
                if (input != null) this.SetValue(input, field.GetValue(this.currentData));
            }
        }

        private void CollectDataFromUI()
        {
            if (this.currentData == null) return;

            foreach (var prop in this.dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite || !this.propertyFields.TryGetValue(prop.Name, out var el)) continue;
                var input = el.Q<VisualElement>($"input-{prop.Name}");
                if (input == null) continue;
                var value = this.GetValue(input);
                if (value != null) prop.SetValue(this.currentData, value);
            }

            foreach (var field in this.dataType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsInitOnly || !this.propertyFields.TryGetValue(field.Name, out var el)) continue;
                var input = el.Q<VisualElement>($"input-{field.Name}");
                if (input == null) continue;
                var value = this.GetValue(input);
                if (value != null) field.SetValue(this.currentData, value);
            }
        }

        private void SetValue(VisualElement input, object value)
        {
            if (value == null) return;
            switch (input)
            {
                case IntegerField    f: f.value = Convert.ToInt32(value);   break;
                case LongField       f: f.value = Convert.ToInt64(value);   break;
                case FloatField      f: f.value = Convert.ToSingle(value);  break;
                case DoubleField     f: f.value = Convert.ToDouble(value);  break;
                case TextField       f: f.value = value.ToString();         break;
                case Toggle          f: f.value = Convert.ToBoolean(value); break;
                case Vector2Field    f: f.value = (Vector2)value;           break;
                case Vector3Field    f: f.value = (Vector3)value;           break;
                case Vector2IntField f: f.value = (Vector2Int)value;        break;
                case Vector3IntField f: f.value = (Vector3Int)value;        break;
                case ColorField      f: f.value = (Color)value;             break;
                case EnumField       f: f.value = (Enum)value;              break;
            }
        }

        private object GetValue(VisualElement input) => input switch
        {
            IntegerField    f => (object)f.value,
            LongField       f => f.value,
            FloatField      f => f.value,
            DoubleField     f => f.value,
            TextField       f => f.value,
            Toggle          f => f.value,
            Vector2Field    f => f.value,
            Vector3Field    f => f.value,
            Vector2IntField f => f.value,
            Vector3IntField f => f.value,
            ColorField      f => f.value,
            EnumField       f => f.value,
            _               => null
        };

        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        /// <summary>Only primitive-like types are shown inline; complex objects are skipped.</summary>
        private bool IsSupportedType(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t.IsPrimitive
                || t == typeof(string)
                || t.IsEnum
                || t == typeof(Vector2)    || t == typeof(Vector3)
                || t == typeof(Vector2Int) || t == typeof(Vector3Int)
                || t == typeof(Color);
        }

        private string PrettyName(string name) =>
            Regex.Replace(name, "(\\B[A-Z])", " $1");

        private void UpdateStatus(string msg, string cssClass)
        {
            if (this.statusLabel == null) return;
            this.statusLabel.text = msg;
            this.statusLabel.ClearClassList();
            this.statusLabel.AddToClassList("status-label");
            this.statusLabel.AddToClassList(cssClass);
        }

        private void UpdateVersionLabel(int version, bool exists)
        {
            if (this.versionLabel == null) return;
            this.versionLabel.text = exists ? $"v{version}" : $"v{version} (new)";
        }

        private void ShowDeleteConfirmation()
        {
            bool ok = EditorUtility.DisplayDialog(
                $"Delete {this.dataType.Name}",
                $"Delete all saved data files for '{this.dataType.Name}'?\n\nThis cannot be undone.",
                "Delete", "Cancel");
            if (ok) this.DeleteAllVersions();
        }
    }
}
