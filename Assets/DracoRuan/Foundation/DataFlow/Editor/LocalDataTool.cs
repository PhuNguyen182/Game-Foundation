using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DracoRuan.Foundation.DataFlow.LocalData;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// PlayerPrefs Data Manager Tool
    /// A powerful Unity Editor tool for managing game data stored in PlayerPrefs
    /// Supports all [Serializable] classes that implement IGameData interface
    /// </summary>
    public class LocalDataTool : EditorWindow
    {
        private VisualElement root;
        private ScrollView mainScrollView;
        private VisualElement mainDataContainer;
        private ScrollView playerPrefsScrollView;
        private VisualElement playerPrefsContainer;
        private ScrollView fileScrollView;
        private VisualElement fileContainer;
        private VisualElement emptyState;
        private Button loadAllButton;
        private Button saveAllButton;
        private Button deleteAllButton;
        private Label dataCountLabel;
        private Label lastActionLabel;
        private TextField searchField;
        private Button clearSearchButton;
        private TextField prefixField;
        private Button refreshFilesButton;
        private Button openFolderButton;
        private Label folderPathLabel;
        private Label playerPrefsCountLabel;
        private Label fileCountLabel;
        
        private readonly List<PlayerPrefsDataEntry> playerPrefsEntries = new();
        private readonly Dictionary<Type, PlayerPrefsDataEntry> playerPrefsEntryMap = new();
        private readonly List<FileDataEntry> fileEntries = new();
        private readonly Dictionary<Type, FileDataEntry> fileEntryMap = new();
        
        private string currentLocalDataPrefix = "GameData";
        
        [MenuItem("Tools/Foundations/Local Data Editor/Local Data Manager", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalDataTool>();
            window.titleContent = new GUIContent("🎮 Local Data Manager");
            window.minSize = new Vector2(900, 500);
            window.Show();
        }
        
        public void CreateGUI()
        {
            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Foundations/DataFlow/Editor/PlayerPrefsDataTool.uxml");
            if (visualTree == null)
            {
                Debug.LogError("❌ Could not load PlayerPrefsDataTool.uxml");
                return;
            }
            
            this.root = visualTree.CloneTree();
            this.rootVisualElement.Add(this.root);
            
            // Cache UI elements
            this.CacheUIElements();
            
            // Bind events
            this.BindEvents();
            
            // Initialize UI
            this.UpdateUI();
            
            Debug.Log("✅ PlayerPrefs Data Manager initialized successfully");
        }
        
        /// <summary>
        /// Caches references to UI elements for better performance
        /// </summary>
        private void CacheUIElements()
        {
            // Main scroll view and container
            this.mainScrollView = this.root.Q<ScrollView>("main-scroll-view");
            this.mainDataContainer = this.root.Q<VisualElement>("main-data-container");
            
            // Dual scroll views and containers
            this.playerPrefsScrollView = this.root.Q<ScrollView>("playerprefs-scroll-view");
            this.playerPrefsContainer = this.root.Q<VisualElement>("playerprefs-container");
            this.fileScrollView = this.root.Q<ScrollView>("file-scroll-view");
            this.fileContainer = this.root.Q<VisualElement>("file-container");
            
            // General UI elements
            this.emptyState = this.root.Q<VisualElement>("empty-state");
            this.loadAllButton = this.root.Q<Button>("load-all-button");
            this.saveAllButton = this.root.Q<Button>("save-all-button");
            this.deleteAllButton = this.root.Q<Button>("delete-all-button");
            this.dataCountLabel = this.root.Q<Label>("data-count-label");
            this.lastActionLabel = this.root.Q<Label>("last-action-label");
            
            // Search functionality
            this.searchField = this.root.Q<TextField>("search-field");
            this.clearSearchButton = this.root.Q<Button>("clear-search-button");
            
            // Prefix configuration
            this.prefixField = this.root.Q<TextField>("prefix-field");
            this.refreshFilesButton = this.root.Q<Button>("refresh-files-button");
            this.openFolderButton = this.root.Q<Button>("open-folder-button");
            this.folderPathLabel = this.root.Q<Label>("folder-path-label");
            
            // Count labels
            this.playerPrefsCountLabel = this.root.Q<Label>("playerprefs-count-label");
            this.fileCountLabel = this.root.Q<Label>("file-count-label");
        }
        
        /// <summary>
        /// Binds event handlers to UI elements
        /// </summary>
        private void BindEvents()
        {
            this.loadAllButton?.RegisterCallback<ClickEvent>(_ => this.LoadAllData());
            this.saveAllButton?.RegisterCallback<ClickEvent>(_ => this.SaveAllData());
            this.deleteAllButton?.RegisterCallback<ClickEvent>(_ => this.ShowDeleteAllConfirmation());
            
            // Search functionality
            this.searchField?.RegisterValueChangedCallback(this.OnSearchChanged);
            this.clearSearchButton?.RegisterCallback<ClickEvent>(_ => this.ClearSearch());
            
            // Prefix configuration
            this.prefixField?.RegisterValueChangedCallback(this.OnPrefixChanged);
            this.refreshFilesButton?.RegisterCallback<ClickEvent>(_ => this.RefreshFileData());
            this.openFolderButton?.RegisterCallback<ClickEvent>(_ => this.OpenDataFolder());
            
            // Auto-scan for data types on first load
            EditorApplication.delayCall += () =>
            {
                this.ScanForGameDataTypes();
                this.ScanForFileData();
            };
        }
        
        /// <summary>
        /// Scans PlayerPrefs keys and matches them with IGameData types
        /// </summary>
        private void ScanForGameDataTypes()
        {
            try
            {
                this.playerPrefsEntries.Clear();
                this.playerPrefsEntryMap.Clear();
                
                // First, get all available IGameData types from assemblies
                var availableGameDataTypes = this.GetAvailableGameDataTypes();
                Debug.Log($"🔍 Found {availableGameDataTypes.Count} available IGameData types: {string.Join(", ", availableGameDataTypes.Select(t => t.Name))}");
                
                // Scan PlayerPrefs keys for JSON data
                var playerPrefsKeys = this.GetAllPlayerPrefsKeys();
                var jsonKeys = this.FilterJsonKeys(playerPrefsKeys);
                
                Debug.Log($"📋 Found {jsonKeys.Count} potential JSON keys in PlayerPrefs: {string.Join(", ", jsonKeys)}");
                
                // Create entries for available types (whether they have data or not)
                foreach (var type in availableGameDataTypes)
                {
                    var entry = new PlayerPrefsDataEntry(type);
                    entry.OnDataChanged += this.OnPlayerPrefsDataEntryChanged;
                    entry.OnEntryDeleted += this.OnPlayerPrefsEntryDeleted;
                    this.playerPrefsEntries.Add(entry);
                    this.playerPrefsEntryMap[type] = entry;
                }
                
                // Try to match JSON keys with types and auto-load data
                this.AutoMatchAndLoadData(jsonKeys, availableGameDataTypes);
                
                this.UpdateUI();
                this.UpdateLastAction($"🔍 Found {availableGameDataTypes.Count} PlayerPrefs types, {jsonKeys.Count} JSON keys");
                
                Debug.Log($"✅ PlayerPrefs scan completed: {availableGameDataTypes.Count} types, {jsonKeys.Count} JSON keys");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error scanning PlayerPrefs data: {ex.Message}");
                this.UpdateLastAction("❌ PlayerPrefs scan failed");
            }
        }
        
        /// <summary>
        /// Scans for file data in the specified folder - only creates entries for existing JSON files
        /// </summary>
        private void ScanForFileData()
        {
            try
            {
                Debug.Log($"📂 Scanning for file data with prefix: {this.currentLocalDataPrefix}");
                
                this.fileEntries.Clear();
                this.fileEntryMap.Clear();
                
                var availableGameDataTypes = this.GetAvailableGameDataTypes();
                var folderPath = Path.Combine(Application.persistentDataPath, this.currentLocalDataPrefix);
                
                this.UpdateFolderPathLabel();
                
                if (!Directory.Exists(folderPath))
                {
                    Debug.Log($"📂 Folder does not exist: {folderPath}");
                    this.UpdateUI();
                    this.UpdateLastAction($"📂 Folder does not exist - no file entries");
                    return;
                }
                
                var jsonFiles = Directory.GetFiles(folderPath, "*.json");
                Debug.Log($"📄 Found {jsonFiles.Length} JSON files in {folderPath}");
                
                if (jsonFiles.Length == 0)
                {
                    Debug.Log($"📂 No JSON files found in folder: {folderPath}");
                    this.UpdateUI();
                    this.UpdateLastAction($"📂 No JSON files found in folder");
                    return;
                }
                
                // Only create file entries for types that have corresponding JSON files
                var createdEntries = 0;
                foreach (var jsonFilePath in jsonFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(jsonFilePath);
                    Debug.Log($"🔍 Processing JSON file: {fileName}.json");
                    
                    // Try to match file name with available types
                    var matchingType = this.FindTypeForFileName(fileName, availableGameDataTypes);
                    
                    if (matchingType != null)
                    {
                        // Create entry only if file exists and type matches
                        var entry = new FileDataEntry(matchingType, this.currentLocalDataPrefix);
                        entry.OnDataChanged += this.OnFileDataEntryChanged;
                        entry.OnEntryDeleted += this.OnFileEntryDeleted;
                        this.fileEntries.Add(entry);
                        this.fileEntryMap[matchingType] = entry;
                        createdEntries++;
                        
                        Debug.Log($"✅ Created file entry for existing file: {matchingType.Name} → {fileName}.json");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Could not match JSON file '{fileName}.json' to any IGameData type");
                    }
                }
                
                this.UpdateUI();
                this.UpdateLastAction($"📂 Found {createdEntries} file entries with existing JSON files");
                
                Debug.Log($"✅ File data scan completed: {createdEntries} entries created from {jsonFiles.Length} JSON files");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error scanning file data: {ex.Message}");
                this.UpdateLastAction("❌ File scan failed");
            }
        }
        
        /// <summary>
        /// Finds the matching IGameData type for a given file name
        /// </summary>
        private Type FindTypeForFileName(string fileName, List<Type> availableTypes)
        {
            try
            {
                // Direct match: fileName == typeName
                var directMatch = availableTypes.FirstOrDefault(type => 
                    string.Equals(type.Name, fileName, StringComparison.OrdinalIgnoreCase));
                
                if (directMatch != null)
                {
                    Debug.Log($"✅ Direct match: {fileName} → {directMatch.Name}");
                    return directMatch;
                }
                
                // Try variations: TypeNameData, TypeNameConfig, etc.
                var suffixesToTry = new[] { "Data", "Config", "Settings", "Info", "State", "Save", "Profile" };
                foreach (var suffix in suffixesToTry)
                {
                    var typeWithSuffix = availableTypes.FirstOrDefault(type => 
                        string.Equals(type.Name, $"{fileName}{suffix}", StringComparison.OrdinalIgnoreCase));
                    
                    if (typeWithSuffix != null)
                    {
                        Debug.Log($"✅ Suffix match: {fileName} → {typeWithSuffix.Name}");
                        return typeWithSuffix;
                    }
                    
                    // Try removing suffix from fileName
                    if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        var baseFileName = fileName.Substring(0, fileName.Length - suffix.Length);
                        var baseMatch = availableTypes.FirstOrDefault(type => 
                            string.Equals(type.Name, baseFileName, StringComparison.OrdinalIgnoreCase));
                        
                        if (baseMatch != null)
                        {
                            Debug.Log($"✅ Base match: {fileName} → {baseMatch.Name}");
                            return baseMatch;
                        }
                    }
                }
                
                Debug.LogWarning($"❌ No matching type found for file: {fileName}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error finding type for fileName '{fileName}': {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Called when prefix field value changes
        /// </summary>
        private void OnPrefixChanged(ChangeEvent<string> evt)
        {
            try
            {
                this.currentLocalDataPrefix = evt.newValue;
                this.UpdateFolderPathLabel();
                
                Debug.Log($"📂 Prefix changed to: {this.currentLocalDataPrefix}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error changing prefix: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refreshes file data scanning
        /// </summary>
        private void RefreshFileData()
        {
            try
            {
                Debug.Log("🔄 Refreshing file data...");
                this.ScanForFileData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error refreshing file data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Opens the data folder in the file explorer
        /// </summary>
        private void OpenDataFolder()
        {
            try
            {
                string dataPath = Path.Combine(Application.persistentDataPath, this.currentLocalDataPrefix);
                
                // Create folder if it doesn't exist
                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }
                
                // Open folder in file explorer
                EditorUtility.RevealInFinder(dataPath);
                
                Debug.Log($"[PlayerPrefsDataTool] Opened folder: {dataPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPrefsDataTool] Failed to open folder: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to open folder:\n{ex.Message}", "OK");
            }
        }
        
        /// <summary>
        /// Updates the folder path label
        /// </summary>
        private void UpdateFolderPathLabel()
        {
            if (this.folderPathLabel != null)
            {
                var fullPath = Path.Combine(Application.persistentDataPath, this.currentLocalDataPrefix);
                this.folderPathLabel.text = $"📁 Path: {fullPath}";
            }
        }
        
        /// <summary>
        /// Gets all available IGameData types from loaded assemblies
        /// </summary>
        private List<Type> GetAvailableGameDataTypes()
        {
            var gameDataTypes = new List<Type>();
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(type => 
                            type.IsClass && 
                            !type.IsAbstract && 
                            typeof(IGameData).IsAssignableFrom(type) &&
                            type.GetCustomAttribute<SerializableAttribute>() != null)
                        .ToArray();
                    
                    gameDataTypes.AddRange(types);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"⚠️ Could not load types from assembly {assembly.FullName}: {ex.Message}");
                }
            }
            
            return gameDataTypes;
        }
        
        /// <summary>
        /// Gets all PlayerPrefs keys with enhanced cross-platform support
        /// </summary>
        private List<string> GetAllPlayerPrefsKeys()
        {
            var keys = new List<string>();
            
            try
            {
                #if UNITY_EDITOR_WIN
                // Windows: Read from Registry
                try
                {
                    using (var registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($"Software\\{Application.companyName}\\{Application.productName}"))
                    {
                        if (registryKey != null)
                        {
                            var registryKeys = registryKey.GetValueNames();
                            keys.AddRange(registryKeys);
                            Debug.Log($"🔍 Windows Registry scan found {registryKeys.Length} keys");
                        }
                        else
                        {
                            Debug.LogWarning("⚠️ Registry key not found, falling back to pattern matching");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"⚠️ Registry access failed: {ex.Message}, using fallback method");
                }
                #endif
                
                // Fallback: Try common patterns (works on all platforms)
                if (keys.Count == 0)
                {
                    keys.AddRange(this.GetKeysUsingPatternMatching());
                }
                
                // Additional fallback: Check for type-based keys
                var additionalKeys = this.GetTypeBasedKeys();
                foreach (var key in additionalKeys)
                {
                    if (!keys.Contains(key))
                    {
                        keys.Add(key);
                    }
                }
                
                Debug.Log($"🔍 Total PlayerPrefs keys found: {keys.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error getting PlayerPrefs keys: {ex.Message}");
                // Last resort: try type-based keys only
                keys.AddRange(this.GetTypeBasedKeys());
            }
            
            return keys;
        }
        
        /// <summary>
        /// Gets keys using pattern matching (cross-platform fallback)
        /// </summary>
        private List<string> GetKeysUsingPatternMatching()
        {
            var keys = new List<string>();
            
            try
            {
                var commonPrefixes = new[] { 
                    "GameData_", "Data_", "Save_", "Player_", "Game_", "Config_", 
                    "Settings_", "Progress_", "State_", "User_", "Session_"
                };
                
                var commonSuffixes = new[] { 
                    "Data", "Config", "Settings", "Progress", "State", "Info", 
                    "Save", "Profile", "Stats", "Inventory", "Achievement"
                };
                
                var commonNames = new[] {
                    "PlayerData", "GameData", "SaveData", "UserData", "ConfigData",
                    "Settings", "Progress", "Inventory", "Stats", "Profile"
                };
                
                // Check prefixed patterns
                foreach (var prefix in commonPrefixes)
                {
                    foreach (var suffix in commonSuffixes)
                    {
                        var testKey = prefix + suffix;
                        if (PlayerPrefs.HasKey(testKey))
                        {
                            keys.Add(testKey);
                            Debug.Log($"🎯 Found key via pattern: {testKey}");
                        }
                    }
                }
                
                // Check common names
                foreach (var commonName in commonNames)
                {
                    if (PlayerPrefs.HasKey(commonName))
                    {
                        keys.Add(commonName);
                        Debug.Log($"🎯 Found key via common name: {commonName}");
                    }
                }
                
                Debug.Log($"🔍 Pattern matching found {keys.Count} keys");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Pattern matching failed: {ex.Message}");
            }
            
            return keys;
        }
        
        /// <summary>
        /// Gets keys based on available IGameData types
        /// </summary>
        private List<string> GetTypeBasedKeys()
        {
            var keys = new List<string>();
            
            try
            {
                var gameDataTypes = this.GetAvailableGameDataTypes();
                
                foreach (var type in gameDataTypes)
                {
                    var possibleKeys = this.GeneratePossibleKeysForType(type);
                    
                    foreach (var key in possibleKeys)
                    {
                        if (PlayerPrefs.HasKey(key) && !keys.Contains(key))
                        {
                            keys.Add(key);
                            Debug.Log($"🎯 Found key for type {type.Name}: {key}");
                        }
                    }
                }
                
                Debug.Log($"🔍 Type-based search found {keys.Count} keys");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Type-based key search failed: {ex.Message}");
            }
            
            return keys;
        }
        
        /// <summary>
        /// Generates all possible keys for a specific type
        /// </summary>
        private List<string> GeneratePossibleKeysForType(Type type)
        {
            var typeName = type.Name;
            var keys = new List<string>
            {
                $"GameData_{typeName}",                    // GameData_TypeName
                typeName,                                  // TypeName
                $"Data_{typeName}",                       // Data_TypeName
                $"Save_{typeName}",                       // Save_TypeName
                $"Player_{typeName}",                     // Player_TypeName
                $"Game_{typeName}",                       // Game_TypeName
                $"Config_{typeName}",                     // Config_TypeName
                $"{typeName}Data",                        // TypeNameData
                $"{typeName}Config",                      // TypeNameConfig
                $"{typeName}Settings",                    // TypeNameSettings
            };
            
            // Add variations with common suffixes removed
            var suffixesToTry = new[] { "Data", "Config", "Settings", "Info", "State" };
            foreach (var suffix in suffixesToTry)
            {
                if (typeName.EndsWith(suffix))
                {
                    var baseName = typeName.Substring(0, typeName.Length - suffix.Length);
                    keys.Add(baseName);
                    keys.Add($"GameData_{baseName}");
                    keys.Add($"Data_{baseName}");
                    keys.Add($"Save_{baseName}");
                }
            }
            
            return keys.Distinct().ToList();
        }
        
        /// <summary>
        /// Filters keys that likely contain JSON data with enhanced validation
        /// </summary>
        private List<string> FilterJsonKeys(List<string> allKeys)
        {
            var jsonKeys = new List<string>();
            
            Debug.Log($"🔍 Filtering {allKeys.Count} keys for JSON content...");
            
            foreach (var key in allKeys)
            {
                try
                {
                    var value = PlayerPrefs.GetString(key, "");
                    
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }
                    
                    if (this.IsValidJson(value))
                    {
                        jsonKeys.Add(key);
                        Debug.Log($"🔍 Found JSON key: '{key}' ({value.Length} chars)");
                        
                        // Log a preview of the JSON content
                        var preview = value.Length > 100 ? value.Substring(0, 100) + "..." : value;
                        Debug.Log($"📄 JSON preview: {preview}");
                    }
                    else
                    {
                        Debug.Log($"⚠️ Key '{key}' contains non-JSON data: {value.Substring(0, Math.Min(50, value.Length))}...");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"⚠️ Could not read PlayerPrefs key '{key}': {ex.Message}");
                }
            }
            
            Debug.Log($"✅ Found {jsonKeys.Count} JSON keys out of {allKeys.Count} total keys");
            return jsonKeys;
        }
        
        /// <summary>
        /// Checks if a string is valid JSON with enhanced validation
        /// </summary>
        private bool IsValidJson(string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
                return false;
            
            jsonString = jsonString.Trim();
            
            // Quick format check - must start/end with appropriate brackets
            if (!((jsonString.StartsWith("{") && jsonString.EndsWith("}")) ||
                  (jsonString.StartsWith("[") && jsonString.EndsWith("]"))))
            {
                return false;
            }
            
            try
            {
                // Use JsonConvert with proper settings
                var settings = new JsonSerializerSettings
                {
                    Error = (sender, args) => args.ErrorContext.Handled = true
                };
                
                var obj = JsonConvert.DeserializeObject(jsonString, settings);
                
                // Additional validation - ensure it's not just primitive values
                if (obj == null)
                    return false;
                
                // Accept objects and arrays, but not simple primitives stored as JSON
                return obj is Newtonsoft.Json.Linq.JObject || obj is Newtonsoft.Json.Linq.JArray;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠️ Unexpected error validating JSON: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Attempts to match JSON keys with available types and auto-load compatible data
        /// </summary>
        private void AutoMatchAndLoadData(List<string> jsonKeys, List<Type> availableTypes)
        {
            foreach (var key in jsonKeys)
            {
                try
                {
                    var jsonData = PlayerPrefs.GetString(key);
                    var matchedType = this.TryMatchJsonToType(jsonData, availableTypes, key);
                    
                    if (matchedType != null && this.playerPrefsEntryMap.ContainsKey(matchedType))
                    {
                        Debug.Log($"✅ Auto-matched key '{key}' to type '{matchedType.Name}'");
                        // The PlayerPrefsDataEntry will handle loading when LoadData() is called
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Could not match JSON key '{key}' to any available IGameData type");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"⚠️ Error processing key '{key}': {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Tries to match JSON data to an available IGameData type with enhanced matching
        /// </summary>
        private Type TryMatchJsonToType(string jsonData, List<Type> availableTypes, string key)
        {
            Debug.Log($"🎯 Attempting to match key '{key}' with available types...");
            
            // First try: Match by key naming convention
            var typeByKey = this.TryMatchByKeyName(key, availableTypes);
            if (typeByKey != null)
            {
                // Verify the JSON can actually be deserialized to this type
                if (this.CanDeserializeToType(jsonData, typeByKey))
                {
                    Debug.Log($"✅ Matched by key name: '{key}' → {typeByKey.Name}");
                    return typeByKey;
                }
                else
                {
                    Debug.LogWarning($"⚠️ Key '{key}' suggests type {typeByKey.Name} but JSON doesn't match");
                }
            }
            
            // Second try: Attempt deserialization with each type
            foreach (var type in availableTypes)
            {
                if (this.CanDeserializeToType(jsonData, type))
                {
                    Debug.Log($"✅ Matched by deserialization: '{key}' → {type.Name}");
                    return type;
                }
            }
            
            Debug.LogWarning($"❌ Could not match key '{key}' to any available type");
            return null;
        }
        
        /// <summary>
        /// Tries to match a key name to a type
        /// </summary>
        private Type TryMatchByKeyName(string key, List<Type> availableTypes)
        {
            // Direct matches
            var directMatches = new Dictionary<string, Func<Type, bool>>
            {
                // Standard patterns
                ["GameData_"] = type => key.Equals($"GameData_{type.Name}", StringComparison.OrdinalIgnoreCase),
                ["Data_"] = type => key.Equals($"Data_{type.Name}", StringComparison.OrdinalIgnoreCase),
                ["Save_"] = type => key.Equals($"Save_{type.Name}", StringComparison.OrdinalIgnoreCase),
                ["Player_"] = type => key.Equals($"Player_{type.Name}", StringComparison.OrdinalIgnoreCase),
                ["Game_"] = type => key.Equals($"Game_{type.Name}", StringComparison.OrdinalIgnoreCase),
                ["Config_"] = type => key.Equals($"Config_{type.Name}", StringComparison.OrdinalIgnoreCase),
                
                // Direct type name
                [""] = type => key.Equals(type.Name, StringComparison.OrdinalIgnoreCase),
                
                // Suffix patterns
                ["Data"] = type => key.Equals($"{type.Name}Data", StringComparison.OrdinalIgnoreCase),
                ["Config"] = type => key.Equals($"{type.Name}Config", StringComparison.OrdinalIgnoreCase),
                ["Settings"] = type => key.Equals($"{type.Name}Settings", StringComparison.OrdinalIgnoreCase),
            };
            
            foreach (var pattern in directMatches)
            {
                var matchingType = availableTypes.FirstOrDefault(pattern.Value);
                if (matchingType != null)
                {
                    return matchingType;
                }
            }
            
            // Try removing common suffixes from type names
            foreach (var type in availableTypes)
            {
                var typeName = type.Name;
                var suffixesToRemove = new[] { "Data", "Config", "Settings", "Info", "State" };
                
                foreach (var suffix in suffixesToRemove)
                {
                    if (typeName.EndsWith(suffix))
                    {
                        var baseName = typeName.Substring(0, typeName.Length - suffix.Length);
                        if (key.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
                            key.Equals($"GameData_{baseName}", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals($"Data_{baseName}", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals($"Save_{baseName}", StringComparison.OrdinalIgnoreCase))
                        {
                            return type;
                        }
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Safely checks if JSON can be deserialized to a specific type
        /// </summary>
        private bool CanDeserializeToType(string jsonData, Type targetType)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Error = (sender, args) => args.ErrorContext.Handled = true,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                };
                
                var deserializedObj = JsonConvert.DeserializeObject(jsonData, targetType, settings);
                return deserializedObj != null;
            }
            catch (Exception ex)
            {
                Debug.Log($"🔍 Cannot deserialize to {targetType.Name}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Updates the UI based on current state
        /// </summary>
        private void UpdateUI()
        {
            this.playerPrefsContainer?.Clear();
            this.fileContainer?.Clear();
            
            var totalEntries = this.playerPrefsEntries.Count + this.fileEntries.Count;
            if (totalEntries == 0)
            {
                this.ShowEmptyState();
            }
            else
            {
                this.ShowBothDataSections();
            }
            
            this.UpdateDataCount();
        }
        
        /// <summary>
        /// Shows empty state when no data types are found
        /// </summary>
        private void ShowEmptyState()
        {
            if (this.emptyState != null)
            {
                this.emptyState.style.display = DisplayStyle.Flex;
            }
            
            if (this.mainScrollView != null)
            {
                this.mainScrollView.style.display = DisplayStyle.None;
            }
        }
        
        /// <summary>
        /// Shows both PlayerPrefs and File data sections
        /// </summary>
        private void ShowBothDataSections()
        {
            if (this.emptyState != null)
            {
                this.emptyState.style.display = DisplayStyle.None;
            }
            
            if (this.mainScrollView != null)
            {
                this.mainScrollView.style.display = DisplayStyle.Flex;
            }
            
            // Use current search term to filter entries
            var currentSearchTerm = this.searchField?.value ?? string.Empty;
            this.FilterDataEntries(currentSearchTerm);
        }
        
        /// <summary>
        /// Updates the data count labels for both PlayerPrefs and File data
        /// </summary>
        private void UpdateDataCount()
        {
            // Update main data count label
            if (this.dataCountLabel != null)
            {
                var playerPrefsWithData = this.playerPrefsEntries.Count(entry => entry.HasData);
                var filesWithData = this.fileEntries.Count(entry => entry.HasData);
                var totalTypes = this.playerPrefsEntries.Count + this.fileEntries.Count;
                var totalWithData = playerPrefsWithData + filesWithData;
                
                this.dataCountLabel.text = $"📊 Found {totalTypes} data types ({totalWithData} with data)";
            }
            
            // Update PlayerPrefs count label
            if (this.playerPrefsCountLabel != null)
            {
                var playerPrefsWithData = this.playerPrefsEntries.Count(entry => entry.HasData);
                this.playerPrefsCountLabel.text = $"{playerPrefsWithData}/{this.playerPrefsEntries.Count} entries";
            }
            
            // Update File count label
            if (this.fileCountLabel != null)
            {
                var filesWithData = this.fileEntries.Count(entry => entry.HasData);
                this.fileCountLabel.text = $"{filesWithData}/{this.fileEntries.Count} entries";
            }
        }
        
        /// <summary>
        /// Updates the last action status label
        /// </summary>
        private void UpdateLastAction(string message)
        {
            if (this.lastActionLabel != null)
            {
                this.lastActionLabel.text = message;
            }
        }
        
        /// <summary>
        /// Loads all data from both PlayerPrefs and File sources
        /// </summary>
        private void LoadAllData()
        {
            try
            {
                Debug.Log("🔄 Starting Load All Data operation for both PlayerPrefs and Files...");
                
                // Rescan for both data sources
                this.ScanForGameDataTypes();
                this.ScanForFileData();
                
                var playerPrefsLoadedCount = 0;
                var fileLoadedCount = 0;
                var playerPrefsErrorCount = 0;
                var fileErrorCount = 0;
                var playerPrefsFoundDataCount = 0;
                var fileFoundDataCount = 0;
                
                // Force UI refresh
                this.RefreshUI();
                
                // Load PlayerPrefs data
                Debug.Log($"📋 Loading PlayerPrefs data for {this.playerPrefsEntries.Count} types...");
                foreach (var entry in this.playerPrefsEntries)
                {
                    try
                    {
                        Debug.Log($"🔍 Loading PlayerPrefs data for {entry.TypeName}...");
                        entry.LoadData();
                        
                        if (entry.HasData)
                        {
                            playerPrefsLoadedCount++;
                            playerPrefsFoundDataCount++;
                            Debug.Log($"✅ Successfully loaded PlayerPrefs data for {entry.TypeName}");
                        }
                        else
                        {
                            Debug.Log($"⚠️ No PlayerPrefs data found for {entry.TypeName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Error loading PlayerPrefs {entry.TypeName}: {ex.Message}");
                        playerPrefsErrorCount++;
                    }
                }
                
                // Load File data
                Debug.Log($"📂 Loading File data for {this.fileEntries.Count} types...");
                foreach (var entry in this.fileEntries)
                {
                    try
                    {
                        Debug.Log($"🔍 Loading File data for {entry.TypeName}...");
                        entry.LoadData();
                        
                        if (entry.HasData)
                        {
                            fileLoadedCount++;
                            fileFoundDataCount++;
                            Debug.Log($"✅ Successfully loaded File data for {entry.TypeName}");
                        }
                        else
                        {
                            Debug.Log($"⚠️ No File data found for {entry.TypeName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Error loading File {entry.TypeName}: {ex.Message}");
                        fileErrorCount++;
                    }
                }
                
                this.UpdateDataCount();
                
                // Update status based on results
                var totalLoaded = playerPrefsLoadedCount + fileLoadedCount;
                var totalErrors = playerPrefsErrorCount + fileErrorCount;
                var totalFound = playerPrefsFoundDataCount + fileFoundDataCount;
                
                if (totalErrors > 0)
                {
                    this.UpdateLastAction($"⚠️ Loaded {totalLoaded} entries with {totalErrors} errors (PP: {playerPrefsLoadedCount}, Files: {fileLoadedCount})");
                }
                else if (totalFound > 0)
                {
                    this.UpdateLastAction($"✅ Loaded {totalLoaded} entries successfully (PlayerPrefs: {playerPrefsLoadedCount}, Files: {fileLoadedCount})");
                }
                else
                {
                    this.UpdateLastAction($"📭 No saved data found - all entries show defaults");
                }
                
                Debug.Log($"✅ Load All Data completed: PP({playerPrefsLoadedCount} loaded, {playerPrefsErrorCount} errors), Files({fileLoadedCount} loaded, {fileErrorCount} errors)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error in LoadAllData: {ex.Message}\n{ex.StackTrace}");
                this.UpdateLastAction("❌ Load all failed");
            }
        }
        
        /// <summary>
        /// Forces a complete UI refresh
        /// </summary>
        private void RefreshUI()
        {
            try
            {
                Debug.Log("🔄 Refreshing UI...");
                
                // Clear current UI
                //this.dataContainer?.Clear();
                
                // Rebuild UI with current data entries
                this.UpdateUI();
                
                Debug.Log("✅ UI refreshed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error refreshing UI: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Saves all data to both PlayerPrefs and Files
        /// </summary>
        private void SaveAllData()
        {
            try
            {
                var playerPrefsSavedCount = 0;
                var fileSavedCount = 0;
                var playerPrefsErrorCount = 0;
                var fileErrorCount = 0;
                
                // Save PlayerPrefs data
                Debug.Log("💾 Saving PlayerPrefs data...");
                foreach (var entry in this.playerPrefsEntries)
                {
                    try
                    {
                        if (entry.HasData)
                        {
                            entry.SaveData();
                            playerPrefsSavedCount++;
                            Debug.Log($"✅ Saved PlayerPrefs data for {entry.TypeName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Error saving PlayerPrefs {entry.TypeName}: {ex.Message}");
                        playerPrefsErrorCount++;
                    }
                }
                
                // Save File data
                Debug.Log("💾 Saving File data...");
                foreach (var entry in this.fileEntries)
                {
                    try
                    {
                        if (entry.HasData)
                        {
                            entry.SaveData();
                            fileSavedCount++;
                            Debug.Log($"✅ Saved File data for {entry.TypeName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Error saving File {entry.TypeName}: {ex.Message}");
                        fileErrorCount++;
                    }
                }
                
                // Update status
                var totalSaved = playerPrefsSavedCount + fileSavedCount;
                var totalErrors = playerPrefsErrorCount + fileErrorCount;
                
                if (totalErrors > 0)
                {
                    this.UpdateLastAction($"⚠️ Saved {totalSaved} entries with {totalErrors} errors (PP: {playerPrefsSavedCount}, Files: {fileSavedCount})");
                }
                else
                {
                    this.UpdateLastAction($"✅ Saved {totalSaved} entries successfully (PlayerPrefs: {playerPrefsSavedCount}, Files: {fileSavedCount})");
                }
                
                Debug.Log($"✅ Save All Data completed: PP({playerPrefsSavedCount} saved, {playerPrefsErrorCount} errors), Files({fileSavedCount} saved, {fileErrorCount} errors)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error in SaveAllData: {ex.Message}");
                this.UpdateLastAction("❌ Save all failed");
            }
        }
        
        /// <summary>
        /// Shows confirmation dialog before deleting all data
        /// </summary>
        private void ShowDeleteAllConfirmation()
        {
            var result = EditorUtility.DisplayDialog(
                "🗑️ Delete All PlayerPrefs Data",
                "Are you sure you want to delete ALL PlayerPrefs data?\n\nThis action cannot be undone!",
                "Delete All",
                "Cancel"
            );
            
            if (result)
            {
                this.DeleteAllData();
            }
        }
        
        /// <summary>
        /// Deletes all PlayerPrefs and File data
        /// </summary>
        private void DeleteAllData()
        {
            try
            {
                var playerPrefsDeletedCount = 0;
                var fileDeletedCount = 0;
                var playerPrefsErrorCount = 0;
                var fileErrorCount = 0;
                
                // Delete PlayerPrefs entries
                foreach (var entry in this.playerPrefsEntries)
                {
                    try
                    {
                        entry.DeleteData();
                        playerPrefsDeletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Error deleting PlayerPrefs {entry.TypeName}: {ex.Message}");
                        playerPrefsErrorCount++;
                    }
                }
                
                // Delete File entries
                foreach (var entry in this.fileEntries)
                {
                    try
                    {
                        entry.DeleteFile();
                        fileDeletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Error deleting File {entry.TypeName}: {ex.Message}");
                        fileErrorCount++;
                    }
                }
                
                // Also clear all PlayerPrefs as fallback
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                
                this.UpdateDataCount();
                
                var totalDeleted = playerPrefsDeletedCount + fileDeletedCount;
                var totalErrors = playerPrefsErrorCount + fileErrorCount;
                
                if (totalErrors > 0)
                {
                    this.UpdateLastAction($"⚠️ Deleted {totalDeleted} entries with {totalErrors} errors (PP: {playerPrefsDeletedCount}, Files: {fileDeletedCount})");
                }
                else
                {
                    this.UpdateLastAction($"🗑️ All data cleared successfully (PlayerPrefs: {playerPrefsDeletedCount}, Files: {fileDeletedCount})");
                }
                
                Debug.Log($"✅ Delete All Data completed: PP({playerPrefsDeletedCount} deleted, {playerPrefsErrorCount} errors), Files({fileDeletedCount} deleted, {fileErrorCount} errors)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error in DeleteAllData: {ex.Message}");
                this.UpdateLastAction("❌ Delete all failed");
            }
        }
        
        
        /// <summary>
        /// Refreshes the tool by rescanning for data types
        /// </summary>
        [MenuItem("Tools/Foundations/Local Data Editor/Refresh Local Data Manager", false, 101)]
        public static void RefreshTool()
        {
            var window = GetWindow<LocalDataTool>(false, null, false);
            if (window != null)
            {
                window.ScanForGameDataTypes();
                window.ScanForFileData();
            }
        }
        
        /// <summary>
        /// Opens PlayerPrefs in the system (Windows only)
        /// </summary>
        [MenuItem("Tools/Foundations/Local Data Editor/Open PlayerPrefs Location", false, 102)]
        public static void OpenPlayerPrefsLocation()
        {
            try
            {
                #if UNITY_EDITOR_WIN
                var companyName = Application.companyName;
                var productName = Application.productName;
                var registryPath = $"HKEY_CURRENT_USER\\Software\\{companyName}\\{productName}";
                
                EditorUtility.DisplayDialog(
                    "📂 PlayerPrefs Location", 
                    $"PlayerPrefs are stored in Windows Registry at:\n\n{registryPath}\n\nYou can open Registry Editor (regedit) to view them manually.",
                    "OK"
                );
                #elif UNITY_EDITOR_OSX
                var path = $"~/Library/Preferences/unity.{Application.companyName}.{Application.productName}.plist";
                EditorUtility.DisplayDialog(
                    "📂 PlayerPrefs Location", 
                    $"PlayerPrefs are stored at:\n\n{path}",
                    "OK"
                );
                #else
                EditorUtility.DisplayDialog(
                    "📂 PlayerPrefs Location", 
                    "PlayerPrefs location varies by platform. This feature is only available on Windows and Mac.",
                    "OK"
                );
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error showing PlayerPrefs location: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Debug tool to scan and log all PlayerPrefs keys and content
        /// </summary>
        [MenuItem("Tools/Foundations/Local Data Editor/Debug PlayerPrefs Scanner", false, 103)]
        public static void DebugPlayerPrefsScanner()
        {
            try
            {
                Debug.Log("🔍 === DEBUG PLAYERPREFS SCANNER ===");
                
                var tool = new LocalDataTool();
                
                // Scan for types
                var types = tool.GetAvailableGameDataTypes();
                Debug.Log($"📦 Found {types.Count} IGameData types: {string.Join(", ", types.Select(t => t.Name))}");
                
                // Scan for keys
                var allKeys = tool.GetAllPlayerPrefsKeys();
                Debug.Log($"🔑 Found {allKeys.Count} total PlayerPrefs keys");
                
                foreach (var key in allKeys)
                {
                    try
                    {
                        var value = PlayerPrefs.GetString(key, "");
                        var isJson = tool.IsValidJson(value);
                        var preview = value.Length > 100 ? value.Substring(0, 100) + "..." : value;
                        
                        Debug.Log($"📄 Key: '{key}' | JSON: {isJson} | Length: {value.Length} | Preview: {preview}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠️ Error reading key '{key}': {ex.Message}");
                    }
                }
                
                // Try to match JSON keys with types
                var jsonKeys = tool.FilterJsonKeys(allKeys);
                Debug.Log($"📋 Found {jsonKeys.Count} JSON keys");
                
                foreach (var jsonKey in jsonKeys)
                {
                    var jsonData = PlayerPrefs.GetString(jsonKey);
                    var matchedType = tool.TryMatchJsonToType(jsonData, types, jsonKey);
                    
                    if (matchedType != null)
                    {
                        Debug.Log($"✅ Successfully matched '{jsonKey}' → {matchedType.Name}");
                    }
                    else
                    {
                        Debug.Log($"❌ Could not match '{jsonKey}' to any type");
                    }
                }
                
                Debug.Log("🔍 === END DEBUG SCANNER ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Debug scanner failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Called when a PlayerPrefs data entry is changed by the user
        /// </summary>
        private void OnPlayerPrefsDataEntryChanged(PlayerPrefsDataEntry entry)
        {
            this.UpdateDataCount();
            this.UpdateLastAction($"✏️ Modified PlayerPrefs: {entry.TypeName}");
        }
        
        /// <summary>
        /// Called when a file data entry is changed by the user
        /// </summary>
        private void OnFileDataEntryChanged(FileDataEntry entry)
        {
            this.UpdateDataCount();
            this.UpdateLastAction($"✏️ Modified File: {entry.TypeName}");
        }
        
        /// <summary>
        /// Called when a PlayerPrefs entry is deleted by user
        /// </summary>
        private void OnPlayerPrefsEntryDeleted(PlayerPrefsDataEntry deletedEntry)
        {
            try
            {
                this.playerPrefsEntries.Remove(deletedEntry);
                this.playerPrefsEntryMap.Remove(deletedEntry.DataType);
                
                // Refresh UI to reflect the change
                var currentSearchTerm = this.searchField?.value ?? string.Empty;
                this.FilterDataEntries(currentSearchTerm);
                
                this.UpdateLastAction($"🗑️ Deleted PlayerPrefs entry: {deletedEntry.TypeName}");
                Debug.Log($"✅ Successfully removed PlayerPrefs entry: {deletedEntry.TypeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error handling PlayerPrefs entry deletion: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Called when a file entry is deleted by user
        /// </summary>
        private void OnFileEntryDeleted(FileDataEntry deletedEntry)
        {
            try
            {
                this.fileEntries.Remove(deletedEntry);
                this.fileEntryMap.Remove(deletedEntry.DataType);
                
                // Refresh UI to reflect the change
                var currentSearchTerm = this.searchField?.value ?? string.Empty;
                this.FilterDataEntries(currentSearchTerm);
                
                this.UpdateLastAction($"🗑️ Deleted file entry: {deletedEntry.TypeName}");
                Debug.Log($"✅ Successfully removed file entry: {deletedEntry.TypeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error handling file entry deletion: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Called when search field value changes
        /// </summary>
        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            this.FilterDataEntries(evt.newValue);
        }
        
        /// <summary>
        /// Filters data entries based on search term for both PlayerPrefs and File data
        /// </summary>
        private void FilterDataEntries(string searchTerm)
        {
            try
            {
                Debug.Log($"🔍 Filtering entries with search term: '{searchTerm}'");
                
                // Clear both containers
                this.playerPrefsContainer?.Clear();
                this.fileContainer?.Clear();
                
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Show all entries when no search term
                    this.ShowAllPlayerPrefsEntries();
                    this.ShowAllFileEntries();
                    
                    var totalEntries = this.playerPrefsEntries.Count + this.fileEntries.Count;
                    this.UpdateLastAction($"🔍 Showing all {totalEntries} entries");
                }
                else
                {
                    // Filter entries by search term
                    var filteredPlayerPrefs = this.GetFilteredPlayerPrefsEntries(searchTerm);
                    var filteredFiles = this.GetFilteredFileEntries(searchTerm);
                    
                    this.ShowFilteredPlayerPrefsEntries(filteredPlayerPrefs);
                    this.ShowFilteredFileEntries(filteredFiles);
                    
                    var totalFiltered = filteredPlayerPrefs.Count + filteredFiles.Count;
                    this.UpdateLastAction($"🔍 Found {totalFiltered} entries matching '{searchTerm}' ({filteredPlayerPrefs.Count} PlayerPrefs, {filteredFiles.Count} Files)");
                }
                
                this.UpdateDataCount();
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error filtering entries: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets filtered PlayerPrefs entries based on search term
        /// </summary>
        private List<PlayerPrefsDataEntry> GetFilteredPlayerPrefsEntries(string searchTerm)
        {
            var term = searchTerm.ToLowerInvariant();
            
            return this.playerPrefsEntries
                .Where(entry => 
                    entry.TypeName.ToLowerInvariant().Contains(term) ||
                    entry.PlayerPrefsKey.ToLowerInvariant().Contains(term))
                .ToList();
        }
        
        /// <summary>
        /// Gets filtered File entries based on search term
        /// </summary>
        private List<FileDataEntry> GetFilteredFileEntries(string searchTerm)
        {
            var term = searchTerm.ToLowerInvariant();
            
            return this.fileEntries
                .Where(entry => 
                    entry.TypeName.ToLowerInvariant().Contains(term) ||
                    entry.FileName.ToLowerInvariant().Contains(term))
                .ToList();
        }
        
        /// <summary>
        /// Shows all PlayerPrefs entries without filtering
        /// </summary>
        private void ShowAllPlayerPrefsEntries()
        {
            foreach (var entry in this.playerPrefsEntries)
            {
                var entryUI = entry.CreateUI();
                this.playerPrefsContainer?.Add(entryUI);
            }
        }
        
        /// <summary>
        /// Shows all File entries without filtering
        /// </summary>
        private void ShowAllFileEntries()
        {
            foreach (var entry in this.fileEntries)
            {
                var entryUI = entry.CreateUI();
                this.fileContainer?.Add(entryUI);
            }
        }
        
        /// <summary>
        /// Shows filtered PlayerPrefs entries
        /// </summary>
        private void ShowFilteredPlayerPrefsEntries(List<PlayerPrefsDataEntry> filteredEntries)
        {
            foreach (var entry in filteredEntries)
            {
                var entryUI = entry.CreateUI();
                this.playerPrefsContainer?.Add(entryUI);
            }
        }
        
        /// <summary>
        /// Shows filtered File entries
        /// </summary>
        private void ShowFilteredFileEntries(List<FileDataEntry> filteredEntries)
        {
            foreach (var entry in filteredEntries)
            {
                var entryUI = entry.CreateUI();
                this.fileContainer?.Add(entryUI);
            }
        }
        
        /// <summary>
        /// Shows filtered data entries
        /// </summary>
        private void ShowFilteredDataEntries(List<PlayerPrefsDataEntry> filteredEntries)
        {
            foreach (var entry in filteredEntries)
            {
                var entryUI = entry.CreateUI();
                //this.dataContainer?.Add(entryUI);
            }
        }
        
        /// <summary>
        /// Clears the search field and shows all entries
        /// </summary>
        private void ClearSearch()
        {
            try
            {
                if (this.searchField != null)
                {
                    this.searchField.value = string.Empty;
                }
                
                this.FilterDataEntries(string.Empty);
                Debug.Log("🔍 Search cleared, showing all entries");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Error clearing search: {ex.Message}");
            }
        }
        
        
        private void OnDisable()
        {
            // Clean up PlayerPrefs event handlers
            foreach (var entry in this.playerPrefsEntries)
            {
                entry.OnDataChanged -= this.OnPlayerPrefsDataEntryChanged;
                entry.OnEntryDeleted -= this.OnPlayerPrefsEntryDeleted;
            }
            
            // Clean up File event handlers
            foreach (var entry in this.fileEntries)
            {
                entry.OnDataChanged -= this.OnFileDataEntryChanged;
                entry.OnEntryDeleted -= this.OnFileEntryDeleted;
            }
        }
    }
}
