using System;
using System.Collections.Generic;
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
    /// Represents a single data entry in the PlayerPrefs Data Tool
    /// Handles UI creation, data binding, and individual save/load operations
    /// </summary>
    public class PlayerPrefsDataEntry
    {
        private readonly Type dataType;
        private readonly string typeName;
        private readonly string playerPrefsKey;
        private object currentData;
        private Foldout foldoutElement;
        private VisualElement contentContainer;
        private Label statusLabel;
        private readonly Dictionary<string, VisualElement> propertyFields;
        
        public Type DataType => this.dataType;
        public string TypeName => this.typeName;
        public string PlayerPrefsKey => this.playerPrefsKey;
        public object CurrentData => this.currentData;
        public bool HasData => this.currentData != null;
        
        public event System.Action<PlayerPrefsDataEntry> OnDataChanged;
        public event System.Action<PlayerPrefsDataEntry> OnEntryDeleted;
        
        public PlayerPrefsDataEntry(Type dataType)
        {
            this.dataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            this.typeName = dataType.Name;
            this.playerPrefsKey = $"{this.typeName}";
            this.propertyFields = new Dictionary<string, VisualElement>();
        }
        
        /// <summary>
        /// Creates the UI element for this data entry with enhanced debugging
        /// </summary>
        public VisualElement CreateUI()
        {
            try
            {
                Debug.Log($"🔨 Creating UI for data entry: {this.typeName}");
                
                var container = new VisualElement();
                container.AddToClassList("data-entry");
                container.name = $"entry-{this.typeName}";
                
                // Create header container
                var headerContainer = this.CreateSimpleHeader();
                container.Add(headerContainer);
                
                // Create content container  
                this.contentContainer = new VisualElement();
                this.contentContainer.AddToClassList("data-entry-content");
                this.contentContainer.style.display = DisplayStyle.None;
                this.contentContainer.name = $"content-{this.typeName}";
                
                container.Add(this.contentContainer);
                
                // Create property fields
                this.CreatePropertyFields();
                
                Debug.Log($"✅ Successfully created UI for {this.typeName}");
                return container;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error creating UI for {this.typeName}: {ex.Message}\n{ex.StackTrace}");
                
                // Return a simple error UI  
                var errorContainer = new VisualElement();
                errorContainer.AddToClassList("data-entry");
                
                var errorLabel = new Label($"❌ Error creating UI for {this.typeName}: {ex.Message}")
                {
                    style = { color = Color.red }
                };
                
                errorContainer.Add(errorLabel);
                return errorContainer;
            }
        }
        
        /// <summary>
        /// Creates a simple header without complex foldout manipulation
        /// </summary>
        private VisualElement CreateSimpleHeader()
        {
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("data-entry-header");
            headerContainer.name = $"header-{this.typeName}";
            
            // Title and expand/collapse toggle
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            
            var titleLabel = new Label($"📦 {this.typeName}");
            titleLabel.AddToClassList("data-entry-title");
            
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
            
            // Status label
            this.statusLabel = new Label("No Data");
            this.statusLabel.AddToClassList("status-label");
            
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
            
            controlsRow.Add(this.statusLabel);
            controlsRow.Add(buttonContainer);
            
            headerContainer.Add(titleRow);
            headerContainer.Add(controlsRow);
            
            Debug.Log($"✅ Created simple header for {this.typeName}");
            return headerContainer;
        }
        
        /// <summary>
        /// Toggles the expansion of the data entry
        /// </summary>
        private void ToggleExpansion()
        {
            try
            {
                if (this.contentContainer != null)
                {
                    var isVisible = this.contentContainer.style.display == DisplayStyle.Flex;
                    this.contentContainer.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
                    
                    // Update expand button text - find it in the parent container
                    var parentContainer = this.contentContainer.parent;
                    if (parentContainer != null)
                    {
                        var expandButton = parentContainer.Q<Button>("expand-button");
                        if (expandButton != null)
                        {
                            expandButton.text = isVisible ? "▶" : "▼";
                            Debug.Log($"🔄 Updated expand button: {expandButton.text}");
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ Could not find expand button for {this.typeName}");
                        }
                    }
                    
                    Debug.Log($"🔄 Toggled expansion for {this.typeName}: {(isVisible ? "Collapsed" : "Expanded")}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ Content container is null for {this.typeName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error toggling expansion for {this.typeName}: {ex.Message}");
            }
        }
        
        
        /// <summary>
        /// Creates property fields for all serializable properties with enhanced debugging
        /// </summary>
        private void CreatePropertyFields()
        {
            try
            {
                Debug.Log($"🔨 Creating property fields for {this.typeName}...");
                
                this.propertyFields.Clear();
                var fieldCount = 0;
                
                var properties = this.dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var fields = this.dataType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                
                Debug.Log($"🔍 Found {properties.Length} properties and {fields.Length} fields for {this.typeName}");
                
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
                                Debug.Log($"✅ Created property field: {prop.Name} ({prop.PropertyType.Name})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ Error creating property field {prop.Name}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.Log($"⚠️ Skipping property: {prop.Name} ({prop.PropertyType.Name})");
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
                                Debug.Log($"✅ Created field: {field.Name} ({field.FieldType.Name})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ Error creating field {field.Name}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.Log($"⚠️ Skipping field: {field.Name} ({field.FieldType.Name})");
                    }
                }
                
                Debug.Log($"✅ Created {fieldCount} property fields for {this.typeName}");
                
                if (fieldCount == 0)
                {
                    // Add a placeholder message
                    var noFieldsLabel = new Label($"⚠️ No editable fields found for {this.typeName}")
                    {
                        style = { color = Color.yellow }
                    };
                    this.contentContainer.Add(noFieldsLabel);
                    Debug.Log($"⚠️ No fields created for {this.typeName} - added placeholder");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error creating property fields for {this.typeName}: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Creates a property field UI element with enhanced type support
        /// </summary>
        private VisualElement CreatePropertyField(string name, Type type, MemberInfo memberInfo)
        {
            try
            {
                Debug.Log($"🔨 Creating property field: {name} ({type.Name})");
                
                var container = new VisualElement();
                container.AddToClassList("property-field");
                container.name = $"field-{name}";
                
                var label = new Label(this.FormatPropertyName(name));
                label.AddToClassList("property-label");
                container.Add(label);
                
                VisualElement inputField = this.CreateInputFieldForType(type);
                
                if (inputField != null)
                {
                    inputField.AddToClassList("property-value");
                    inputField.name = $"input-{name}";
                    container.Add(inputField);
                    
                    // Bind change events with proper type handling
                    this.BindFieldChangeEvent(inputField, name);
                    
                    Debug.Log($"✅ Successfully created field UI for {name}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ Could not create input field for {name} ({type.Name})");
                    
                    // Add error placeholder
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
                Debug.LogError($"❌ Error creating property field {name}: {ex.Message}");
                
                // Return error container
                var errorContainer = new VisualElement();
                errorContainer.Add(new Label($"❌ Error: {name}") { style = { color = Color.red } });
                return errorContainer;
            }
        }
        
        /// <summary>
        /// Creates the appropriate input field for the given type
        /// </summary>
        private VisualElement CreateInputFieldForType(Type type)
        {
            // Handle nullable types
            var actualType = Nullable.GetUnderlyingType(type) ?? type;
            
            if (actualType == typeof(int))
            {
                return new IntegerField();
            }
            else if (actualType == typeof(float))
            {
                return new FloatField();
            }
            else if (actualType == typeof(double))
            {
                var doubleField = new DoubleField();
                return doubleField;
            }
            else if (actualType == typeof(string))
            {
                return new TextField();
            }
            else if (actualType == typeof(bool))
            {
                return new Toggle();
            }
            else if (actualType == typeof(Vector2))
            {
                return new Vector2Field();
            }
            else if (actualType == typeof(Vector3))
            {
                return new Vector3Field();
            }
            else if (actualType == typeof(Vector2Int))
            {
                return new Vector2IntField();
            }
            else if (actualType == typeof(Vector3Int))
            {
                return new Vector3IntField();
            }
            else if (actualType == typeof(Color))
            {
                return new ColorField();
            }
            else if (actualType.IsEnum)
            {
                try
                {
                    var enumValue = Activator.CreateInstance(actualType);
                    return new EnumField((Enum)enumValue);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"⚠️ Could not create enum field for {actualType.Name}: {ex.Message}");
                    return new TextField(); // Fallback
                }
            }
            else
            {
                // Fallback to text field for complex types with JSON serialization
                Debug.Log($"🔄 Using TextField fallback for complex type: {actualType.Name}");
                return new TextField();
            }
        }
        
        /// <summary>
        /// Binds change events to input fields with proper type handling
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
                
                Debug.Log($"✅ Bound change event for field: {fieldName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error binding change event for {fieldName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Called when a field value is changed
        /// </summary>
        private void OnFieldChanged(string fieldName)
        {
            try
            {
                Debug.Log($"🔄 Field changed: {fieldName} in {this.typeName}");
                this.OnDataChanged?.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error handling field change for {fieldName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads data from PlayerPrefs with enhanced JSON support
        /// </summary>
        public void LoadData()
        {
            try
            {
                var loadedFromKey = this.TryLoadFromSpecificKey();
                if (loadedFromKey)
                {
                    return;
                }
                
                // Try to find any key that might contain data for this type
                var foundKey = this.FindMatchingPlayerPrefsKey();
                if (!string.IsNullOrEmpty(foundKey))
                {
                    var jsonData = PlayerPrefs.GetString(foundKey);
                    this.currentData = this.DeserializeJsonSafely(jsonData);
                    
                    if (this.currentData != null)
                    {
                        this.UpdateUI();
                        this.UpdateStatus($"✅ Loaded from '{foundKey}'", "status-success");
                        Debug.Log($"✅ Successfully loaded {this.typeName} from key: {foundKey}");
                        return;
                    }
                }
                
                // No data found, create default instance
                this.CreateDefaultInstance();
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error loading data for {this.typeName}: {ex.Message}\n{ex.StackTrace}");
                this.UpdateStatus("❌ Load failed", "status-error");
                this.CreateDefaultInstance();
            }
        }
        
        /// <summary>
        /// Tries to load data from the default PlayerPrefs key for this type
        /// </summary>
        private bool TryLoadFromSpecificKey()
        {
            if (!PlayerPrefs.HasKey(this.playerPrefsKey))
            {
                return false;
            }
            
            try
            {
                var jsonData = PlayerPrefs.GetString(this.playerPrefsKey);
                if (this.IsValidJsonData(jsonData))
                {
                    this.currentData = this.DeserializeJsonSafely(jsonData);
                    
                    if (this.currentData != null)
                    {
                        this.UpdateUI();
                        this.UpdateStatus("✅ Loaded successfully", "status-success");
                        Debug.Log($"✅ Loaded {this.typeName} from primary key: {this.playerPrefsKey}");
                        return true;
                    }
                }
                else
                {
                    Debug.LogWarning($"⚠️ Invalid JSON data found in key: {this.playerPrefsKey}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error loading from primary key {this.playerPrefsKey}: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Finds any PlayerPrefs key that might contain data compatible with this type
        /// </summary>
        private string FindMatchingPlayerPrefsKey()
        {
            try
            {
                var possibleKeys = this.GeneratePossibleKeys();
                
                foreach (var key in possibleKeys)
                {
                    if (PlayerPrefs.HasKey(key))
                    {
                        var jsonData = PlayerPrefs.GetString(key);
                        if (this.IsValidJsonData(jsonData) && this.CanDeserializeToType(jsonData))
                        {
                            Debug.Log($"🎯 Found matching data for {this.typeName} in key: {key}");
                            return key;
                        }
                    }
                }
                
                // Last resort: scan for any keys that contain compatible JSON
                return this.ScanAllKeysForCompatibleData();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Error finding matching key for {this.typeName}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Generates possible PlayerPrefs keys for this data type
        /// </summary>
        private List<string> GeneratePossibleKeys()
        {
            var keys = new List<string>
            {
                this.playerPrefsKey,                    // GameData_TypeName
                this.typeName,                          // TypeName
                $"Data_{this.typeName}",               // Data_TypeName
                $"Save_{this.typeName}",               // Save_TypeName
                $"Player_{this.typeName}",             // Player_TypeName
                $"Game_{this.typeName}",               // Game_TypeName
                $"{this.typeName}Data",                // TypeNameData
                $"{this.typeName}Config",              // TypeNameConfig
                $"{this.typeName}Settings",            // TypeNameSettings
                this.typeName.Replace("Data", ""),     // Remove "Data" suffix if exists
                this.typeName.Replace("Config", ""),   // Remove "Config" suffix if exists
                this.typeName.Replace("Settings", ""), // Remove "Settings" suffix if exists
            };
            
            return keys.Distinct().ToList();
        }
        
        /// <summary>
        /// Scans all available PlayerPrefs keys for compatible JSON data (fallback method)
        /// </summary>
        private string ScanAllKeysForCompatibleData()
        {
            try
            {
                #if UNITY_EDITOR_WIN
                // Windows: Read from Registry
                using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($"Software\\{Application.companyName}\\{Application.productName}"))
                {
                    if (registryKey != null)
                    {
                        var allKeys = registryKey.GetValueNames();
                        foreach (var key in allKeys)
                        {
                            try
                            {
                                var jsonData = PlayerPrefs.GetString(key, "");
                                if (this.IsValidJsonData(jsonData) && this.CanDeserializeToType(jsonData))
                                {
                                    Debug.Log($"🔍 Found compatible data for {this.typeName} in unexpected key: {key}");
                                    return key;
                                }
                            }
                            catch
                            {
                                // Continue scanning
                            }
                        }
                    }
                }
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Could not scan registry for {this.typeName}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Checks if the given JSON data can be deserialized to this type
        /// </summary>
        private bool CanDeserializeToType(string jsonData)
        {
            try
            {
                var deserializedObj = this.DeserializeJsonSafely(jsonData);
                return deserializedObj != null;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Safely deserializes JSON data to the target type with enhanced error handling
        /// </summary>
        private object DeserializeJsonSafely(string jsonData)
        {
            if (string.IsNullOrWhiteSpace(jsonData))
            {
                return null;
            }
            
            try
            {
                // Configure JsonSerializer settings for better compatibility
                var settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Include,
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    Error = (sender, args) =>
                    {
                        Debug.LogWarning($"⚠️ JSON deserialization warning for {this.typeName}: {args.ErrorContext.Error.Message}");
                        args.ErrorContext.Handled = true;
                    }
                };
                
                var deserializedObj = JsonConvert.DeserializeObject(jsonData, this.dataType, settings);
                
                if (deserializedObj != null)
                {
                    Debug.Log($"✅ Successfully deserialized {this.typeName} from JSON");
                    return deserializedObj;
                }
            }
            catch (JsonException jsonEx)
            {
                Debug.LogError($"❌ JSON deserialization error for {this.typeName}: {jsonEx.Message}");
                Debug.LogError($"📄 Problematic JSON: {jsonData}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Unexpected error deserializing {this.typeName}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Validates if the given string contains valid JSON data
        /// </summary>
        private bool IsValidJsonData(string jsonData)
        {
            if (string.IsNullOrWhiteSpace(jsonData))
            {
                return false;
            }
            
            jsonData = jsonData.Trim();
            if (!((jsonData.StartsWith("{") && jsonData.EndsWith("}")) ||
                  (jsonData.StartsWith("[") && jsonData.EndsWith("]"))))
            {
                return false;
            }
            
            try
            {
                var testObj = JsonConvert.DeserializeObject(jsonData);
                return testObj != null;
            }
            catch
            {
                return false;
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
                this.UpdateStatus("⚠️ No saved data, using defaults", "status-warning");
                Debug.Log($"📦 Created default instance for {this.typeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Could not create default instance for {this.typeName}: {ex.Message}");
                this.UpdateStatus("❌ Failed to create default", "status-error");
            }
        }
        
        /// <summary>
        /// Saves current data to PlayerPrefs with enhanced JSON serialization
        /// </summary>
        public void SaveData()
        {
            try
            {
                if (this.currentData == null)
                {
                    this.UpdateStatus("⚠️ No data to save", "status-warning");
                    return;
                }
                
                // Collect data from UI first
                this.CollectDataFromUI();
                
                // Serialize with enhanced settings
                var jsonData = this.SerializeDataSafely();
                if (string.IsNullOrEmpty(jsonData))
                {
                    this.UpdateStatus("❌ Serialization failed", "status-error");
                    return;
                }
                
                // Validate JSON before saving
                if (!this.IsValidJsonData(jsonData))
                {
                    Debug.LogError($"❌ Generated invalid JSON for {this.typeName}");
                    this.UpdateStatus("❌ Invalid JSON generated", "status-error");
                    return;
                }
                
                // Save to PlayerPrefs
                PlayerPrefs.SetString(this.playerPrefsKey, jsonData);
                PlayerPrefs.Save();
                
                this.UpdateStatus("✅ Saved successfully", "status-success");
                Debug.Log($"✅ Successfully saved {this.typeName} to PlayerPrefs key: {this.playerPrefsKey}");
                Debug.Log($"📄 Saved JSON ({jsonData.Length} chars): {(jsonData.Length > 200 ? jsonData.Substring(0, 200) + "..." : jsonData)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error saving data for {this.typeName}: {ex.Message}\n{ex.StackTrace}");
                this.UpdateStatus("❌ Save failed", "status-error");
            }
        }
        
        /// <summary>
        /// Safely serializes the current data to JSON with proper formatting and error handling
        /// </summary>
        private string SerializeDataSafely()
        {
            if (this.currentData == null)
            {
                return null;
            }
            
            try
            {
                // Configure JsonSerializer settings for optimal output
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,                    // Pretty formatting for readability
                    DefaultValueHandling = DefaultValueHandling.Include, // Include default values
                    NullValueHandling = NullValueHandling.Include,       // Include null values
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore, // Avoid circular references
                    DateFormatHandling = DateFormatHandling.IsoDateFormat, // Standard date format
                    Error = (sender, args) =>
                    {
                        Debug.LogError($"❌ JSON serialization error for {this.typeName}: {args.ErrorContext.Error.Message}");
                        args.ErrorContext.Handled = true;
                    }
                };
                
                var jsonData = JsonConvert.SerializeObject(this.currentData, settings);
                
                if (!string.IsNullOrWhiteSpace(jsonData))
                {
                    Debug.Log($"✅ Successfully serialized {this.typeName} to JSON ({jsonData.Length} characters)");
                    return jsonData;
                }
                else
                {
                    Debug.LogError($"❌ Serialization returned empty result for {this.typeName}");
                }
            }
            catch (JsonException jsonEx)
            {
                Debug.LogError($"❌ JSON serialization error for {this.typeName}: {jsonEx.Message}");
                Debug.LogError($"🔍 Data causing error: {this.currentData}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Unexpected error serializing {this.typeName}: {ex.Message}");
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
            switch (fieldElement)
            {
                case IntegerField intField:
                    return intField.value;
                case FloatField floatField:
                    return floatField.value;
                case TextField textField:
                    return textField.value;
                case Toggle toggle:
                    return toggle.value;
                case Vector2Field vec2Field:
                    return vec2Field.value;
                case Vector3Field vec3Field:
                    return vec3Field.value;
                case ColorField colorField:
                    return colorField.value;
                case EnumField enumField:
                    return enumField.value;
                default:
                    return null;
            }
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
            // Convert PascalCase to readable format
            return System.Text.RegularExpressions.Regex.Replace(name, "(\\B[A-Z])", " $1");
        }
        
        /// <summary>
        /// Determines if a property should be included in the UI
        /// </summary>
        private bool ShouldIncludeProperty(PropertyInfo prop)
        {
            // Skip JsonIgnore properties
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                return false;
            
            // Skip indexer properties
            if (prop.GetIndexParameters().Length > 0)
                return false;
            
            // Skip complex object properties that don't have proper serialization
            if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string) && !prop.PropertyType.IsEnum)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Determines if a field should be included in the UI
        /// </summary>
        private bool ShouldIncludeField(FieldInfo field)
        {
            // Skip JsonIgnore fields
            if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                return false;
            
            // Skip readonly fields
            if (field.IsInitOnly)
                return false;
            
            // Skip complex object fields that don't have proper serialization
            if (field.FieldType.IsClass && field.FieldType != typeof(string) && !field.FieldType.IsEnum)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Shows confirmation dialog before deleting this entry
        /// </summary>
        private void ShowDeleteConfirmation()
        {
            try
            {
                var result = EditorUtility.DisplayDialog(
                    $"🗑️ Delete {this.typeName}",
                    $"Are you sure you want to delete the PlayerPrefs data for '{this.typeName}'?\n\nThis action cannot be undone!",
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
                Debug.LogError($"❌ Error showing delete confirmation for {this.typeName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Deletes this entire data entry and notifies parent
        /// </summary>
        private void DeleteEntry()
        {
            try
            {
                // Delete from PlayerPrefs
                this.DeleteData();
                
                // Notify parent tool that this entry was deleted
                this.OnEntryDeleted?.Invoke(this);
                
                Debug.Log($"✅ Successfully deleted entry: {this.typeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error deleting entry {this.typeName}: {ex.Message}");
                this.UpdateStatus("❌ Delete failed", "status-error");
            }
        }
        
        /// <summary>
        /// Deletes this data entry from PlayerPrefs
        /// </summary>
        public void DeleteData()
        {
            try
            {
                if (PlayerPrefs.HasKey(this.playerPrefsKey))
                {
                    PlayerPrefs.DeleteKey(this.playerPrefsKey);
                    PlayerPrefs.Save();
                    this.UpdateStatus("🗑️ Deleted", "status-info");
                    Debug.Log($"✅ Deleted PlayerPrefs key: {this.playerPrefsKey}");
                }
                else
                {
                    this.UpdateStatus("⚠️ No data to delete", "status-warning");
                    Debug.Log($"⚠️ No PlayerPrefs key found: {this.playerPrefsKey}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error deleting data for {this.typeName}: {ex.Message}");
                this.UpdateStatus("❌ Delete failed", "status-error");
            }
        }
    }
}
