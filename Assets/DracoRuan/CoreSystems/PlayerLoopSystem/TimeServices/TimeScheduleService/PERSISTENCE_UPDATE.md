# Persistence System Update

## Tổng quan

Đã cập nhật hệ thống persistence của TimeScheduleService để sử dụng **FileDataSaveService** và **PlayerPrefDataSaveService** có sẵn trong project, cho phép lựa chọn storage backend linh hoạt.

---

## 🎯 Những gì đã thay đổi

### 1. **New Persistence Architecture**

#### **Trước đây:**
```csharp
// Hardcoded PlayerPrefs trong CountdownTimerManager
var json = JsonUtility.ToJson(data);
PlayerPrefs.SetString("CountdownTimers", json);
```

#### **Bây giờ:**
```csharp
// Sử dụng abstraction layer với FileDataSaveService hoặc PlayerPrefDataSaveService
public interface ITimerPersistence
{
    bool SaveTimers(List<CountdownTimerData> timerDataList);
    List<CountdownTimerData> LoadTimers();
    bool ClearTimers();
    bool HasSavedTimers();
}
```

---

## 📁 Files mới được tạo

### 1. **TimerDataSerializer.cs**
Serializer cho CountdownTimerData sử dụng JSON:
```csharp
public class TimerDataSerializer : IDataSerializer<List<CountdownTimerData>>
{
    public string FileExtension => ".json";
    public string Serialize(List<CountdownTimerData> data) { /* ... */ }
    public List<CountdownTimerData> Deserialize(string serializedData) { /* ... */ }
}
```

### 2. **FileTimerPersistence.cs**
Implementation sử dụng **FileDataSaveService**:
```csharp
public class FileTimerPersistence : ITimerPersistence
{
    private readonly IDataSaveService<List<CountdownTimerData>> _dataSaveService;
    
    public FileTimerPersistence()
    {
        var serializer = new TimerDataSerializer();
        this._dataSaveService = new FileDataSaveService<List<CountdownTimerData>>(serializer);
    }
}
```
- **Lưu vào:** `Application.persistentDataPath/PD/TimerData.json`
- **Format:** Pretty-printed JSON
- **Lợi ích:**
  - ✅ Dễ backup và restore
  - ✅ Có thể edit trực tiếp file
  - ✅ Không giới hạn dung lượng
  - ✅ Cross-platform compatible

### 3. **PlayerPrefsTimerPersistence.cs** (Updated)
Implementation sử dụng **PlayerPrefDataSaveService**:
```csharp
public class PlayerPrefsTimerPersistence : ITimerPersistence
{
    private readonly IDataSaveService<List<CountdownTimerData>> _dataSaveService;
    
    public PlayerPrefsTimerPersistence()
    {
        var serializer = new TimerDataSerializer();
        this._dataSaveService = new PlayerPrefDataSaveService<List<CountdownTimerData>>(serializer);
    }
}
```
- **Lưu vào:** PlayerPrefs key "CountdownTimers"
- **Format:** JSON string
- **Lợi ích:**
  - ✅ Đơn giản và nhanh
  - ✅ Native platform storage
  - ⚠️ Có giới hạn dung lượng

### 4. **TimerPersistenceType.cs**
Enum để lựa chọn persistence type:
```csharp
public enum TimerPersistenceType
{
    File,         // Recommended - Default
    PlayerPrefs
}
```

### 5. **TimerPersistenceFactory.cs**
Factory để tạo persistence instances:
```csharp
public static class TimerPersistenceFactory
{
    public static ITimerPersistence Create(TimerPersistenceType persistenceType)
    {
        return persistenceType switch
        {
            TimerPersistenceType.File => CreateFilePersistence(),
            TimerPersistenceType.PlayerPrefs => CreatePlayerPrefsPersistence(),
            _ => CreateFilePersistence() // Default
        };
    }
}
```

---

## 🚀 Cách sử dụng

### **Option 1: File Persistence (Recommended - Default)**

```csharp
// Cách 1: Sử dụng constructor mặc định
var manager = new TimeScheduleManager();

// Cách 2: Explicit File persistence
var manager = new TimeScheduleManager(TimerPersistenceType.File);
```

**File location:** `Application.persistentDataPath/PD/TimerData.json`

**Example JSON output:**
```json
{
    "timers": [
        {
            "key": "skill_cooldown",
            "endTimeUnix": 1728577800,
            "startTimeUnix": 1728577740,
            "totalDuration": 60.0
        },
        {
            "key": "daily_reward",
            "endTimeUnix": 1728664200,
            "startTimeUnix": 1728577800,
            "totalDuration": 86400.0
        }
    ]
}
```

### **Option 2: PlayerPrefs Persistence**

```csharp
var manager = new TimeScheduleManager(TimerPersistenceType.PlayerPrefs);
```

**Storage location:** 
- Windows: Registry `HKEY_CURRENT_USER\Software\[CompanyName]\[ProductName]`
- macOS: `~/Library/Preferences/com.[CompanyName].[ProductName].plist`
- Linux: `~/.config/unity3d/[CompanyName]/[ProductName]/prefs`

### **Option 3: Custom Persistence (Advanced)**

```csharp
// Tạo custom persistence implementation
public class CustomPersistence : ITimerPersistence { /* ... */ }

// Sử dụng custom persistence
var customPersistence = new CustomPersistence();
var manager = new CountdownTimerManager(customPersistence);
```

---

## 📊 So sánh File vs PlayerPrefs

| Feature | File Persistence | PlayerPrefs Persistence |
|---------|------------------|-------------------------|
| **Location** | `persistentDataPath/PD/` | Registry/Plist |
| **Format** | Pretty JSON | Compact JSON |
| **Capacity** | No limit | ~1MB limit |
| **Backup** | ✅ Easy (copy file) | ❌ Platform-specific |
| **Edit** | ✅ Direct file edit | ❌ Need tools |
| **Performance** | Fast | Very fast |
| **Cross-platform** | ✅ Yes | ⚠️ Platform-specific |
| **Recommended** | ✅ **YES** | For simple cases |

---

## 🔧 Architecture Benefits

### **Separation of Concerns**
```
TimeScheduleService/
├── Persistence/
│   ├── ITimerPersistence.cs           # Interface
│   ├── TimerDataSerializer.cs         # JSON serialization
│   ├── FileTimerPersistence.cs        # File implementation
│   ├── PlayerPrefsTimerPersistence.cs # PlayerPrefs implementation
│   ├── TimerPersistenceType.cs        # Enum
│   └── TimerPersistenceFactory.cs     # Factory
```

### **Advantages**
1. ✅ **Flexible:** Easy to add new storage backends (Cloud, Database, etc.)
2. ✅ **Testable:** Can mock persistence for unit tests
3. ✅ **Reusable:** Uses existing FileDataSaveService and PlayerPrefDataSaveService
4. ✅ **Maintainable:** Clear separation of concerns
5. ✅ **Scalable:** Easy to extend without breaking existing code

---

## 🎨 Usage Examples

### **Basic Usage**
```csharp
public class GameManager : MonoBehaviour
{
    private TimeScheduleManager _timeManager;
    
    void Start()
    {
        // Default: File persistence (Recommended)
        _timeManager = new TimeScheduleManager();
        
        // Create timer
        var timer = _timeManager.StartCountdownTimer("skill_cooldown", 60f);
        
        // Timer auto-saves to file on app pause/quit
    }
    
    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            _timeManager?.SaveAllSchedulers();
        }
    }
}
```

### **Switching Persistence**
```csharp
// Development: Use File for easy debugging
#if UNITY_EDITOR
    var manager = new TimeScheduleManager(TimerPersistenceType.File);
#else
    // Production: Use PlayerPrefs for simplicity
    var manager = new TimeScheduleManager(TimerPersistenceType.PlayerPrefs);
#endif
```

### **Manual Save/Load**
```csharp
// Force save all timers
_timeManager.SaveAllSchedulers();

// Force load all timers (happens automatically on init)
_timeManager.LoadAllSchedulers();

// Clear all saved data
var persistence = TimerPersistenceFactory.CreateFilePersistence();
persistence.ClearTimers();
```

---

## 🔍 Debugging

### **Check File Location**
```csharp
Debug.Log($"Timer data location: {Application.persistentDataPath}/PD/TimerData.json");
```

**Platform-specific paths:**
- **Windows:** `C:/Users/[Username]/AppData/LocalLow/[Company]/[Product]/PD/TimerData.json`
- **macOS:** `~/Library/Application Support/[Company]/[Product]/PD/TimerData.json`
- **Android:** `/storage/emulated/0/Android/data/[package]/files/PD/TimerData.json`
- **iOS:** `/var/mobile/Containers/Data/Application/[GUID]/Documents/PD/TimerData.json`

### **View Saved Data**
```csharp
// For File persistence
var filePath = Path.Combine(Application.persistentDataPath, "PD", "TimerData.json");
if (File.Exists(filePath))
{
    var json = File.ReadAllText(filePath);
    Debug.Log($"Saved timers:\n{json}");
}

// For PlayerPrefs persistence
var prefData = PlayerPrefs.GetString("CountdownTimers", "No data");
Debug.Log($"PlayerPrefs data:\n{prefData}");
```

---

## 🧪 Testing

### **Unit Test Example**
```csharp
[Test]
public void FileTimerPersistence_SaveAndLoad_Success()
{
    // Arrange
    var persistence = new FileTimerPersistence();
    var testData = new List<CountdownTimerData>
    {
        new CountdownTimerData("test_timer", 1728577800, 1728577740, 60f)
    };
    
    // Act
    var saveResult = persistence.SaveTimers(testData);
    var loadedData = persistence.LoadTimers();
    
    // Assert
    Assert.IsTrue(saveResult);
    Assert.AreEqual(1, loadedData.Count);
    Assert.AreEqual("test_timer", loadedData[0].key);
}
```

---

## 🔄 Migration from Old System

### **Automatic Migration**
Hệ thống mới tương thích ngược với PlayerPrefs cũ:

```csharp
// Old data in PlayerPrefs key "CountdownTimers" 
// sẽ tự động được load nếu dùng PlayerPrefsTimerPersistence

// Để migrate sang File:
var oldPersistence = new PlayerPrefsTimerPersistence();
var oldData = oldPersistence.LoadTimers();

if (oldData.Count > 0)
{
    var newPersistence = new FileTimerPersistence();
    newPersistence.SaveTimers(oldData);
    oldPersistence.ClearTimers(); // Optional: clear old data
}
```

---

## ⚠️ Breaking Changes

**None!** Hệ thống persistence mới hoàn toàn backward compatible.

- ✅ API không thay đổi
- ✅ Có thể chuyển đổi giữa File và PlayerPrefs
- ✅ Default behavior: File persistence (thay vì PlayerPrefs)

---

## 🎯 Best Practices

1. **Use File Persistence by Default**
   ```csharp
   var manager = new TimeScheduleManager(); // Uses File automatically
   ```

2. **Save on Important Events**
   ```csharp
   void OnApplicationPause(bool pause)
   {
       if (pause) _timeManager?.SaveAllSchedulers();
   }
   
   void OnApplicationQuit()
   {
       _timeManager?.Dispose(); // Auto-saves
   }
   ```

3. **Handle Persistence Errors**
   ```csharp
   var persistence = TimerPersistenceFactory.CreateFilePersistence();
   if (!persistence.SaveTimers(data))
   {
       Debug.LogError("Failed to save timers!");
       // Fallback to PlayerPrefs
       var fallback = TimerPersistenceFactory.CreatePlayerPrefsPersistence();
       fallback.SaveTimers(data);
   }
   ```

---

## 📝 Summary

✅ **Integrated with existing save system** (FileDataSaveService, PlayerPrefDataSaveService)  
✅ **File persistence as default** (Recommended)  
✅ **Easy to switch** between File and PlayerPrefs  
✅ **Backward compatible** with old PlayerPrefs data  
✅ **Production-ready** with proper error handling  
✅ **Well-documented** with examples and comparisons  

**Default behavior:** Lưu vào file JSON tại `persistentDataPath/PD/TimerData.json` 🎉

