using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Represents a single file-based data entry in the Data Tool
    /// Handles UI creation, data binding, and individual save/load operations for JSON files
    /// </summary>
    public class FileDataEntry
    {
        private readonly Type dataType;
        private readonly string typeName;
        private readonly string fileName;
        private readonly string fullFilePath;
        private object currentData;
        private VisualElement contentContainer;
        private Label statusLabel;
        private readonly Dictionary<string, VisualElement> propertyFields;
        
        public Type DataType => this.dataType;
        public string TypeName => this.typeName;
        public string FileName => this.fileName;
        public string FullFilePath => this.fullFilePath;
        public object CurrentData => this.currentData;
        public bool HasData => this.currentData != null;
        
        public event System.Action<FileDataEntry> OnDataChanged;
        public event System.Action<FileDataEntry> OnEntryDeleted;
        
        public FileDataEntry(Type dataType, string localDataPrefix)
        {
            this.dataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            this.typeName = dataType.Name;
            this.fileName = $"{this.typeName}.json";
            
            var folderPath = Path.Combine(Application.persistentDataPath, localDataPrefix);
            this.fullFilePath = Path.Combine(folderPath, this.fileName);
            this.propertyFields = new Dictionary<string, VisualElement>();
            
            // Ensure directory exists
            this.EnsureDirectoryExists();
        }
        
        /// <summary>
        /// Ensures the data directory exists
        /// </summary>
        private void EnsureDirectoryExists()
        {
            try
            {
                var directory = Path.GetDirectoryName(this.fullFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Debug.Log($"📁 Created directory: {directory}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error creating directory for {this.typeName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates the UI element for this file data entry
        /// </summary>
        public VisualElement CreateUI()
        {
            try
            {
                Debug.Log($"🔨 Creating UI for file entry: {this.typeName}");
                
                var container = new VisualElement();
                container.AddToClassList("data-entry");
                container.AddToClassList("file-data-entry");
                container.name = $"file-entry-{this.typeName}";
                
                // Create header container
                var headerContainer = this.CreateFileHeader();
                container.Add(headerContainer);
                
                // Create content container  
                this.contentContainer = new VisualElement();
                this.contentContainer.AddToClassList("data-entry-content");
                this.contentContainer.style.display = DisplayStyle.None;
                this.contentContainer.name = $"file-content-{this.typeName}";
                
                container.Add(this.contentContainer);
                
                // Create property fields
                this.CreatePropertyFields();
                
                Debug.Log($"✅ Successfully created file UI for {this.typeName}");
                return container;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error creating file UI for {this.typeName}: {ex.Message}\n{ex.StackTrace}");
                
                // Return error UI  
                var errorContainer = new VisualElement();
                errorContainer.AddToClassList("data-entry");
                
                var errorLabel = new Label($"❌ Error creating file UI for {this.typeName}: {ex.Message}")
                {
                    style = { color = Color.red }
                };
                
                errorContainer.Add(errorLabel);
                return errorContainer;
            }
        }
        
        /// <summary>
        /// Creates header for file data entry
        /// </summary>
        private VisualElement CreateFileHeader()
        {
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("data-entry-header");
            headerContainer.AddToClassList("file-data-header");
            headerContainer.name = $"file-header-{this.typeName}";
            
            // Title and expand/collapse toggle
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            
            var titleLabel = new Label($"📄 {this.typeName}");
            titleLabel.AddToClassList("data-entry-title");
            titleLabel.AddToClassList("file-data-title");
            
            var expandButton = new Button(() => this.ToggleExpansion())
            {
                text = "▶"
            };
            expandButton.AddToClassList("expand-button");
            expandButton.name = "expand-button";
            
            titleRow.Add(titleLabel);
            titleRow.Add(expandButton);
            
            // Controls row
            var controlsRow = new VisualElement();
            controlsRow.style.flexDirection = FlexDirection.Row;
            controlsRow.style.alignItems = Align.Center;
            controlsRow.style.justifyContent = Justify.SpaceBetween;
            controlsRow.style.marginTop = 5;
            
            // Status and file path info
            var statusContainer = new VisualElement();
            
            this.statusLabel = new Label("No Data");
            this.statusLabel.AddToClassList("status-label");
            
            var filePathLabel = new Label($"📂 {this.fileName}");
            filePathLabel.AddToClassList("file-path-label");
            
            statusContainer.Add(this.statusLabel);
            statusContainer.Add(filePathLabel);
            
            // Button container
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            
            // Load button
            var loadButton = new Button(() => this.LoadData())
            {
                text = "📥 Load"
            };
            loadButton.AddToClassList("small-button");
            loadButton.AddToClassList("load-button");
            
            // Save button
            var saveButton = new Button(() => this.SaveData())
            {
                text = "💾 Save"
            };
            saveButton.AddToClassList("small-button");
            saveButton.AddToClassList("save-button");
            
            // Delete button
            var deleteButton = new Button(() => this.ShowDeleteConfirmation())
            {
                text = "🗑️ Delete"
            };
            deleteButton.AddToClassList("small-button");
            deleteButton.AddToClassList("delete-button");
            
            buttonContainer.Add(loadButton);
            buttonContainer.Add(saveButton);
            buttonContainer.Add(deleteButton);
            
            controlsRow.Add(statusContainer);
            controlsRow.Add(buttonContainer);
            
            headerContainer.Add(titleRow);
            headerContainer.Add(controlsRow);
            
            Debug.Log($"✅ Created file header for {this.typeName}");
            return headerContainer;
        }
        
        /// <summary>
        /// Toggles the expansion of the file data entry
        /// </summary>
        private void ToggleExpansion()
        {
            try
            {
                if (this.contentContainer != null)
                {
                    var isVisible = this.contentContainer.style.display == DisplayStyle.Flex;
                    this.contentContainer.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
                    
                    // Update expand button text
                    var parentContainer = this.contentContainer.parent;
                    if (parentContainer != null)
                    {
                        var expandButton = parentContainer.Q<Button>("expand-button");
                        if (expandButton != null)
                        {
                            expandButton.text = isVisible ? "▶" : "▼";
                            Debug.Log($"🔄 Updated file expand button: {expandButton.text}");
                        }
                    }
                    
                    Debug.Log($"🔄 Toggled file expansion for {this.typeName}: {(isVisible ? "Collapsed" : "Expanded")}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error toggling file expansion for {this.typeName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates property fields for all serializable properties
        /// </summary>
        private void CreatePropertyFields()
        {
            try
            {
                Debug.Log($"🔨 Creating property fields for file {this.typeName}...");
                
                this.propertyFields.Clear();
                var fieldCount = 0;
                
                var properties = this.dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var fields = this.dataType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                
                Debug.Log($"🔍 Found {properties.Length} properties and {fields.Length} fields for file {this.typeName}");
                
                // Add property fields
                foreach (var prop in properties)
                {
                    if (this.ShouldIncludeProperty(prop))
                    {
                        try
                        {
                            var fieldElement = this.CreatePropertyField(prop.Name, prop.PropertyType, prop);
                            if (fieldElement != null)
                            {
                                this.propertyFields[prop.Name] = fieldElement;
                                this.contentContainer.Add(fieldElement);
                                fieldCount++;
                                Debug.Log($"✅ Created file property field: {prop.Name} ({prop.PropertyType.Name})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ Error creating file property field {prop.Name}: {ex.Message}");
                        }
                    }
                }
                
                // Add field fields
                foreach (var field in fields)
                {
                    if (this.ShouldIncludeField(field))
                    {
                        try
                        {
                            var fieldElement = this.CreatePropertyField(field.Name, field.FieldType, field);
                            if (fieldElement != null)
                            {
                                this.propertyFields[field.Name] = fieldElement;
                                this.contentContainer.Add(fieldElement);
                                fieldCount++;
                                Debug.Log($"✅ Created file field: {field.Name} ({field.FieldType.Name})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ Error creating file field {field.Name}: {ex.Message}");
                        }
                    }
                }
                
                Debug.Log($"✅ Created {fieldCount} property fields for file {this.typeName}");
                
                if (fieldCount == 0)
                {
                    var noFieldsLabel = new Label($"⚠️ No editable fields found for {this.typeName}")
                    {
                        style = { color = Color.yellow }
                    };
                    this.contentContainer.Add(noFieldsLabel);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error creating file property fields for {this.typeName}: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Creates a property field UI element
        /// </summary>
        private VisualElement CreatePropertyField(string name, Type type, MemberInfo memberInfo)
        {
            try
            {
                var container = new VisualElement();
                container.AddToClassList("property-field");
                container.name = $"file-field-{name}";
                
                var label = new Label(this.FormatPropertyName(name));
                label.AddToClassList("property-label");
                container.Add(label);
                
                VisualElement inputField = this.CreateInputFieldForType(type);
                
                if (inputField != null)
                {
                    inputField.AddToClassList("property-value");
                    inputField.name = $"file-input-{name}";
                    container.Add(inputField);
                    
                    this.BindFieldChangeEvent(inputField, name);
                }
                else
                {
                    var errorLabel = new Label($"⚠️ Unsupported type: {type.Name}")
                    {
                        style = { color = Color.yellow }
                    };
                    container.Add(errorLabel);
                }
                
                return container;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error creating file property field {name}: {ex.Message}");
                
                var errorContainer = new VisualElement();
                errorContainer.Add(new Label($"❌ Error: {name}") { style = { color = Color.red } });
                return errorContainer;
            }
        }
        
        /// <summary>
        /// Creates appropriate input field for the given type
        /// </summary>
        private VisualElement CreateInputFieldForType(Type type)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;
            
            return actualType switch
            {
                var t when t == typeof(int) => new IntegerField(),
                var t when t == typeof(float) => new FloatField(),
                var t when t == typeof(double) => new DoubleField(),
                var t when t == typeof(string) => new TextField(),
                var t when t == typeof(bool) => new Toggle(),
                var t when t == typeof(Vector2) => new Vector2Field(),
                var t when t == typeof(Vector3) => new Vector3Field(),
                var t when t == typeof(Vector2Int) => new Vector2IntField(),
                var t when t == typeof(Vector3Int) => new Vector3IntField(),
                var t when t == typeof(Color) => new ColorField(),
                var t when t.IsEnum => this.CreateEnumField(actualType),
                _ => new TextField() // Fallback for complex types
            };
        }
        
        /// <summary>
        /// Creates enum field safely
        /// </summary>
        private VisualElement CreateEnumField(Type enumType)
        {
            try
            {
                var enumValue = Activator.CreateInstance(enumType);
                return new EnumField((Enum)enumValue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Could not create enum field for {enumType.Name}: {ex.Message}");
                return new TextField();
            }
        }
        
        /// <summary>
        /// Binds change events to input fields
        /// </summary>
        private void BindFieldChangeEvent(VisualElement inputField, string fieldName)
        {
            try
            {
                switch (inputField)
                {
                    case IntegerField intField:
                        intField.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case FloatField floatField:
                        floatField.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case DoubleField doubleField:
                        doubleField.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case TextField textField:
                        textField.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case Toggle toggle:
                        toggle.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case Vector2Field vec2Field:
                        vec2Field.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case Vector3Field vec3Field:
                        vec3Field.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case Vector2IntField vec2IntField:
                        vec2IntField.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case Vector3IntField vec3IntField:
                        vec3IntField.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case ColorField colorField:
                        colorField.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                    case EnumField enumField:
                        enumField.RegisterValueChangedCallback(_ => this.OnFieldChanged(fieldName));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error binding file change event for {fieldName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Called when a field value is changed
        /// </summary>
        private void OnFieldChanged(string fieldName)
        {
            try
            {
                Debug.Log($"🔄 File field changed: {fieldName} in {this.typeName}");
                this.OnDataChanged?.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error handling file field change for {fieldName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads data from JSON file
        /// </summary>
        public void LoadData()
        {
            try
            {
                Debug.Log($"📥 Loading file data for {this.typeName} from {this.fullFilePath}");
                
                if (!File.Exists(this.fullFilePath))
                {
                    Debug.Log($"⚠️ File does not exist: {this.fullFilePath}");
                    this.CreateDefaultInstance();
                    return;
                }
                
                var jsonContent = File.ReadAllText(this.fullFilePath);
                
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    Debug.Log($"⚠️ File is empty: {this.fullFilePath}");
                    this.CreateDefaultInstance();
                    return;
                }
                
                this.currentData = this.DeserializeJsonSafely(jsonContent);
                
                if (this.currentData != null)
                {
                    this.UpdateUI();
                    this.UpdateStatus($"✅ Loaded from file", "status-success");
                    Debug.Log($"✅ Successfully loaded file data for {this.typeName}");
                }
                else
                {
                    Debug.LogError($"❌ Failed to deserialize file data for {this.typeName}");
                    this.CreateDefaultInstance();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error loading file data for {this.typeName}: {ex.Message}\n{ex.StackTrace}");
                this.UpdateStatus("❌ Load failed", "status-error");
                this.CreateDefaultInstance();
            }
        }
        
        /// <summary>
        /// Saves current data to JSON file
        /// </summary>
        public void SaveData()
        {
            try
            {
                Debug.Log($"💾 Saving file data for {this.typeName} to {this.fullFilePath}");
                
                if (this.currentData == null)
                {
                    this.UpdateStatus("⚠️ No data to save", "status-warning");
                    return;
                }
                
                // Collect data from UI first
                this.CollectDataFromUI();
                
                // Serialize data
                var jsonData = this.SerializeDataSafely();
                if (string.IsNullOrEmpty(jsonData))
                {
                    this.UpdateStatus("❌ Serialization failed", "status-error");
                    return;
                }
                
                // Ensure directory exists
                this.EnsureDirectoryExists();
                
                // Write to file
                File.WriteAllText(this.fullFilePath, jsonData);
                
                this.UpdateStatus("✅ Saved to file", "status-success");
                Debug.Log($"✅ Successfully saved file data for {this.typeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error saving file data for {this.typeName}: {ex.Message}\n{ex.StackTrace}");
                this.UpdateStatus("❌ Save failed", "status-error");
            }
        }
        
        /// <summary>
        /// Shows confirmation dialog before deleting this file entry
        /// </summary>
        private void ShowDeleteConfirmation()
        {
            try
            {
                var result = EditorUtility.DisplayDialog(
                    $"🗑️ Delete {this.typeName}",
                    $"Are you sure you want to delete the file '{this.fileName}'?\n\nFile path: {this.fullFilePath}\n\nThis action cannot be undone!",
                    "Delete",
                    "Cancel"
                );
                
                if (result)
                {
                    this.DeleteEntry();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error showing file delete confirmation for {this.typeName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Deletes this file entry and notifies parent
        /// </summary>
        private void DeleteEntry()
        {
            try
            {
                // Delete the file
                this.DeleteFile();
                
                // Notify parent tool that this entry was deleted
                this.OnEntryDeleted?.Invoke(this);
                
                Debug.Log($"✅ Successfully deleted file entry: {this.typeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error deleting file entry {this.typeName}: {ex.Message}");
                this.UpdateStatus("❌ Delete failed", "status-error");
            }
        }
        
        /// <summary>
        /// Deletes the JSON file
        /// </summary>
        public void DeleteFile()
        {
            try
            {
                if (File.Exists(this.fullFilePath))
                {
                    File.Delete(this.fullFilePath);
                    this.UpdateStatus("🗑️ File deleted", "status-info");
                    Debug.Log($"✅ Deleted file: {this.fullFilePath}");
                }
                else
                {
                    this.UpdateStatus("⚠️ File not found", "status-warning");
                    Debug.Log($"⚠️ File not found: {this.fullFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error deleting file for {this.typeName}: {ex.Message}");
                this.UpdateStatus("❌ Delete failed", "status-error");
            }
        }
        
        /// <summary>
        /// Creates a default instance of the data type
        /// </summary>
        private void CreateDefaultInstance()
        {
            try
            {
                this.currentData = Activator.CreateInstance(this.dataType);
                this.UpdateUI();
                this.UpdateStatus("⚠️ No saved file, using defaults", "status-warning");
                Debug.Log($"📦 Created default instance for file {this.typeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Could not create default instance for file {this.typeName}: {ex.Message}");
                this.UpdateStatus("❌ Failed to create default", "status-error");
            }
        }
        
        /// <summary>
        /// Safely deserializes JSON data to the target type
        /// </summary>
        private object DeserializeJsonSafely(string jsonData)
        {
            if (string.IsNullOrWhiteSpace(jsonData))
            {
                return null;
            }
            
            try
            {
                var settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Include,
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    Error = (sender, args) =>
                    {
                        Debug.LogWarning($"⚠️ JSON deserialization warning for file {this.typeName}: {args.ErrorContext.Error.Message}");
                        args.ErrorContext.Handled = true;
                    }
                };
                
                var deserializedObj = JsonConvert.DeserializeObject(jsonData, this.dataType, settings);
                
                if (deserializedObj != null)
                {
                    Debug.Log($"✅ Successfully deserialized file {this.typeName} from JSON");
                    return deserializedObj;
                }
            }
            catch (JsonException jsonEx)
            {
                Debug.LogError($"❌ JSON deserialization error for file {this.typeName}: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Unexpected error deserializing file {this.typeName}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Safely serializes the current data to JSON
        /// </summary>
        private string SerializeDataSafely()
        {
            if (this.currentData == null)
            {
                return null;
            }
            
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    NullValueHandling = NullValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    Error = (sender, args) =>
                    {
                        Debug.LogError($"❌ JSON serialization error for file {this.typeName}: {args.ErrorContext.Error.Message}");
                        args.ErrorContext.Handled = true;
                    }
                };
                
                var jsonData = JsonConvert.SerializeObject(this.currentData, settings);
                
                if (!string.IsNullOrWhiteSpace(jsonData))
                {
                    Debug.Log($"✅ Successfully serialized file {this.typeName} to JSON ({jsonData.Length} characters)");
                    return jsonData;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error serializing file {this.typeName}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Updates UI with current data values
        /// </summary>
        private void UpdateUI()
        {
            if (this.currentData == null) return;
            
            var properties = this.dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = this.dataType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            
            // Update property values
            foreach (var prop in properties)
            {
                if (this.ShouldIncludeProperty(prop) && this.propertyFields.ContainsKey(prop.Name))
                {
                    var fieldElement = this.propertyFields[prop.Name];
                    var inputField = fieldElement.Q<VisualElement>("property-value") ?? fieldElement.Children().LastOrDefault();
                    
                    if (inputField != null)
                    {
                        var value = prop.GetValue(this.currentData);
                        this.SetFieldValue(inputField, value);
                    }
                }
            }
            
            // Update field values
            foreach (var field in fields)
            {
                if (this.ShouldIncludeField(field) && this.propertyFields.ContainsKey(field.Name))
                {
                    var fieldElement = this.propertyFields[field.Name];
                    var inputField = fieldElement.Q<VisualElement>("property-value") ?? fieldElement.Children().LastOrDefault();
                    
                    if (inputField != null)
                    {
                        var value = field.GetValue(this.currentData);
                        this.SetFieldValue(inputField, value);
                    }
                }
            }
        }
        
        /// <summary>
        /// Collects data from UI fields and updates current data object
        /// </summary>
        private void CollectDataFromUI()
        {
            if (this.currentData == null) return;
            
            var properties = this.dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = this.dataType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            
            // Collect property values
            foreach (var prop in properties)
            {
                if (this.ShouldIncludeProperty(prop) && this.propertyFields.ContainsKey(prop.Name) && prop.CanWrite)
                {
                    var fieldElement = this.propertyFields[prop.Name];
                    var inputField = fieldElement.Q<VisualElement>("property-value") ?? fieldElement.Children().LastOrDefault();
                    
                    if (inputField != null)
                    {
                        var value = this.GetFieldValue(inputField, prop.PropertyType);
                        if (value != null)
                        {
                            prop.SetValue(this.currentData, value);
                        }
                    }
                }
            }
            
            // Collect field values
            foreach (var field in fields)
            {
                if (this.ShouldIncludeField(field) && this.propertyFields.ContainsKey(field.Name))
                {
                    var fieldElement = this.propertyFields[field.Name];
                    var inputField = fieldElement.Q<VisualElement>("property-value") ?? fieldElement.Children().LastOrDefault();
                    
                    if (inputField != null)
                    {
                        var value = this.GetFieldValue(inputField, field.FieldType);
                        if (value != null)
                        {
                            field.SetValue(this.currentData, value);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Sets value to UI field element
        /// </summary>
        private void SetFieldValue(VisualElement fieldElement, object value)
        {
            if (value == null) return;
            
            switch (fieldElement)
            {
                case IntegerField intField:
                    intField.value = Convert.ToInt32(value);
                    break;
                case FloatField floatField:
                    floatField.value = Convert.ToSingle(value);
                    break;
                case DoubleField doubleField:
                    doubleField.value = Convert.ToDouble(value);
                    break;
                case TextField textField:
                    textField.value = value.ToString();
                    break;
                case Toggle toggle:
                    toggle.value = Convert.ToBoolean(value);
                    break;
                case Vector2Field vec2Field:
                    vec2Field.value = (Vector2)value;
                    break;
                case Vector3Field vec3Field:
                    vec3Field.value = (Vector3)value;
                    break;
                case Vector2IntField vec2IntField:
                    vec2IntField.value = (Vector2Int)value;
                    break;
                case Vector3IntField vec3IntField:
                    vec3IntField.value = (Vector3Int)value;
                    break;
                case ColorField colorField:
                    colorField.value = (Color)value;
                    break;
                case EnumField enumField:
                    enumField.value = (Enum)value;
                    break;
            }
        }
        
        /// <summary>
        /// Gets value from UI field element
        /// </summary>
        private object GetFieldValue(VisualElement fieldElement, Type targetType)
        {
            return fieldElement switch
            {
                IntegerField intField => intField.value,
                FloatField floatField => floatField.value,
                DoubleField doubleField => doubleField.value,
                TextField textField => textField.value,
                Toggle toggle => toggle.value,
                Vector2Field vec2Field => vec2Field.value,
                Vector3Field vec3Field => vec3Field.value,
                Vector2IntField vec2IntField => vec2IntField.value,
                Vector3IntField vec3IntField => vec3IntField.value,
                ColorField colorField => colorField.value,
                EnumField enumField => enumField.value,
                _ => null
            };
        }
        
        /// <summary>
        /// Updates status label with message and style
        /// </summary>
        private void UpdateStatus(string message, string cssClass)
        {
            if (this.statusLabel != null)
            {
                this.statusLabel.text = message;
                this.statusLabel.ClearClassList();
                this.statusLabel.AddToClassList("status-label");
                this.statusLabel.AddToClassList(cssClass);
            }
        }
        
        /// <summary>
        /// Formats property name for display
        /// </summary>
        private string FormatPropertyName(string name)
        {
            return System.Text.RegularExpressions.Regex.Replace(name, "(\\B[A-Z])", " $1");
        }
        
        /// <summary>
        /// Determines if a property should be included in the UI
        /// </summary>
        private bool ShouldIncludeProperty(PropertyInfo prop)
        {
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                return false;
            
            if (prop.GetIndexParameters().Length > 0)
                return false;
            
            if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string) && !prop.PropertyType.IsEnum)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Determines if a field should be included in the UI
        /// </summary>
        private bool ShouldIncludeField(FieldInfo field)
        {
            if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                return false;
            
            if (field.IsInitOnly)
                return false;
            
            if (field.FieldType.IsClass && field.FieldType != typeof(string) && !field.FieldType.IsEnum)
                return false;
            
            return true;
        }
    }
}
