# Hệ thống Bộ đếm Thời gian theo Thời gian Thực

## Tổng quan

Hệ thống Bộ đếm Thời gian theo Thời gian Thực cung cấp khả năng tạo và quản lý các bộ đếm thời gian với độ chính xác cao, tự động lưu trữ và khôi phục trạng thái. Hệ thống được thiết kế để hoạt động theo thời gian thực, đảm bảo tính chính xác ngay cả khi ứng dụng bị tắt và mở lại.

### Tính năng chính

- **Bộ đếm thời gian theo thời gian thực**: Sử dụng Unix timestamp để đảm bảo độ chính xác cao
- **Lưu trữ và khôi phục tự động**: Tự động lưu trạng thái và khôi phục khi khởi động lại ứng dụng
- **Quản lý tự động**: Tự động dừng và giải phóng bộ đếm khi hết hạn
- **Hiệu suất tối ưu**: Được tối ưu cho runtime performance với caching và efficient updates
- **Event-driven architecture**: Hỗ trợ events cho cập nhật và hoàn thành
- **Singleton service**: Dễ dàng truy cập từ mọi nơi trong ứng dụng

### Trường hợp sử dụng

- **Cooldown kỹ năng**: Quản lý thời gian hồi chiêu cho các kỹ năng trong game
- **Daily rewards**: Bộ đếm thời gian cho phần thưởng hàng ngày
- **Event timers**: Quản lý thời gian cho các sự kiện đặc biệt
- **Crafting timers**: Bộ đếm thời gian cho quá trình chế tạo
- **Energy regeneration**: Quản lý thời gian hồi phục năng lượng

### Điều kiện tiên quyết

- Unity 2021.3 hoặc cao hơn
- PracticalModules.PlayerLoopServices.Core
- PracticalModules.Patterns.Factory
- PracticalUtilities.CalculationExtensions (TimeExtensions)

## Cấu trúc thành phần

Hệ thống này tuân theo kiến trúc MVP (Model-View-Presenter) và bao gồm các thành phần sau:

### Cấu trúc thư mục

```
TimeScheduleService/
├── Data/                              # Mô hình dữ liệu và cấu hình
│   └── CountdownTimerData.cs          # Cấu trúc dữ liệu lưu trữ
├── Manager/                           # Logic nghiệp vụ và quản lý
│   ├── ICountdownTimerManager.cs      # Interface quản lý bộ đếm
│   ├── CountdownTimerManager.cs       # Triển khai quản lý bộ đếm
│   └── TimeScheduleManager.cs         # Manager tổng hợp
├── TimeSchedulerComponent/            # Thành phần bộ đếm
│   ├── ICountdownTimer.cs             # Interface bộ đếm thời gian
│   └── CountdownTimer.cs              # Triển khai bộ đếm thời gian
├── TimeFactory/                       # Factory pattern
│   └── CountdownTimerFactory.cs       # Factory tạo bộ đếm
└── Extensions/                        # Tiện ích mở rộng
    └── TimeExtensions.cs              # Tiện ích thời gian
```

### Thành phần cốt lõi

#### Data/
- **CountdownTimerData.cs**: Cấu trúc dữ liệu serializable để lưu trữ thông tin bộ đếm

#### Manager/
- **ICountdownTimerManager.cs**: Interface định nghĩa các phương thức quản lý bộ đếm
- **CountdownTimerManager.cs**: Triển khai quản lý tập trung các bộ đếm thời gian
- **TimeScheduleManager.cs**: Manager tổng hợp tích hợp với hệ thống Update

#### TimeSchedulerComponent/
- **ICountdownTimer.cs**: Interface định nghĩa hành vi của bộ đếm thời gian
- **CountdownTimer.cs**: Triển khai bộ đếm thời gian với khả năng lưu trữ

#### TimeFactory/
- **CountdownTimerFactory.cs**: Factory pattern để tạo bộ đếm từ config hoặc dữ liệu đã lưu

## Hướng dẫn sử dụng

### Thiết lập ban đầu

1. **Import tính năng**
   - Sao chép thư mục `TimeScheduleService` vào Assets của dự án
   - Đảm bảo tất cả dependencies đã được import đúng cách

2. **Cấu hình tính năng**
   - Hệ thống sẽ tự động khởi tạo khi lần đầu được sử dụng
   - Không cần cấu hình thêm trong Inspector

### Sử dụng cơ bản

#### Bắt đầu sử dụng

```csharp
using PracticalModules.PlayerLoopServices.TimeServices.TimeScheduleService.Manager;
using PracticalModules.PlayerLoopServices.TimeServices.TimeScheduleService.TimeSchedulerComponent;

// Tạo bộ đếm thời gian
var timer = TimeScheduleManager.StartCountdownTimer("my_timer", 60f); // 60 giây

// Đăng ký sự kiện
timer.OnUpdate += (remainingSeconds) => {
    Debug.Log($"Còn lại: {remainingSeconds}s");
};

timer.OnComplete += () => {
    Debug.Log("Bộ đếm đã hoàn thành!");
};
```

#### Sử dụng nâng cao

```csharp
// Tạo bộ đếm với cấu hình tùy chỉnh
var customTimer = TimeScheduleManager.StartCountdownTimer("skill_cooldown", 30f);

// Kiểm tra trạng thái bộ đếm
if (customTimer.IsActive)
{
    Debug.Log($"Thời gian còn lại: {customTimer.RemainingSeconds}s");
    Debug.Log($"Thời gian tổng cộng: {customTimer.TotalDuration}s");
}

// Làm mới bộ đếm với thời gian mới
customTimer.Reset(45f);
```

## Thiết lập Unity GameObject

### GameObjects bắt buộc

1. **TimeScheduleManager GameObject**
   - Tạo một GameObject trống trong scene
   - Đặt tên là "TimeScheduleManager"
   - Thêm component `TimeScheduleManager` vào GameObject này

2. **Canvas Setup (nếu sử dụng UI)**
   - Tạo Canvas trong scene (nếu cần UI)
   - Đặt tên là "TimerCanvas"
   - Cài đặt Canvas Scaler thành "Scale With Screen Size"
   - Reference Resolution: 1920x1080

### Cấu hình Component

#### TimeScheduleManager Component

1. **Chọn GameObject TimeScheduleManager**
2. **Trong Inspector, cấu hình các tham chiếu sau:**

**Tham chiếu bắt buộc:**
- **Auto Initialize**: Đánh dấu để tự động khởi tạo khi Start
- **Enable Persistence**: Đánh dấu để bật tính năng lưu trữ

**Tham chiếu tùy chọn:**
- **Debug Mode**: Bật để hiển thị log debug
- **Update Interval**: Khoảng thời gian cập nhật (mặc định: 0.1s)

### Quy trình thiết lập từng bước

1. **Tạo GameObject quản lý**
   ```
   Right-click trong Hierarchy → Create Empty
   Tên: "TimeScheduleManager"
   ```

2. **Thêm Manager Component**
   ```
   Chọn GameObject TimeScheduleManager
   Add Component → Scripts → TimeScheduleManager
   ```

3. **Cấu hình Manager**
   ```
   Chọn GameObject TimeScheduleManager
   Đánh dấu "Auto Initialize"
   Đánh dấu "Enable Persistence"
   Đặt Update Interval = 0.1
   ```

4. **Kiểm tra thiết lập**
   ```
   Nhấn Play trong Unity
   Kiểm tra Console không có lỗi
   Test chức năng bộ đếm thời gian
   ```

## Tài liệu API

### TimeScheduleManager Class

#### Phương thức công khai

**StartCountdownTimer(string key, float durationSeconds)**
- Tạo hoặc lấy bộ đếm thời gian với khóa và thời gian đã cho
- Tham số: `key` - Khóa định danh duy nhất, `durationSeconds` - Thời gian đếm ngược (giây)
- Trả về: `ICountdownTimer` - Instance bộ đếm thời gian
- Ví dụ:
  ```csharp
  var timer = TimeScheduleManager.StartCountdownTimer("skill_1", 30f);
  ```

**GetCountdownTimer(string key)**
- Lấy bộ đếm thời gian theo khóa
- Tham số: `key` - Khóa định danh
- Trả về: `ICountdownTimer` hoặc null nếu không tồn tại
- Ví dụ:
  ```csharp
  var timer = TimeScheduleManager.GetCountdownTimer("skill_1");
  if (timer != null) {
      Debug.Log($"Thời gian còn lại: {timer.RemainingSeconds}s");
  }
  ```

**HasCountdownTimer(string key)**
- Kiểm tra bộ đếm có tồn tại không
- Tham số: `key` - Khóa định danh
- Trả về: `bool` - True nếu tồn tại
- Ví dụ:
  ```csharp
  if (TimeScheduleManager.HasCountdownTimer("skill_1")) {
      // Bộ đếm đang tồn tại
  }
  ```

**RemoveCountdownTimer(string key)**
- Xóa bộ đếm thời gian
- Tham số: `key` - Khóa định danh
- Trả về: `bool` - True nếu xóa thành công
- Ví dụ:
  ```csharp
  bool removed = TimeScheduleManager.RemoveCountdownTimer("skill_1");
  ```

#### Thuộc tính công khai

**ActiveTimerCount**
- Lấy số lượng bộ đếm đang hoạt động
- Kiểu: `int`
- Ví dụ:
  ```csharp
  int count = TimeScheduleManager.ActiveTimerCount;
  Debug.Log($"Có {count} bộ đếm đang hoạt động");
  ```

### ICountdownTimer Interface

#### Thuộc tính

**Key**
- Khóa định danh của bộ đếm
- Kiểu: `string`
- Ví dụ: `string key = timer.Key;`

**RemainingSeconds**
- Thời gian còn lại tính bằng giây
- Kiểu: `float`
- Ví dụ: `float remaining = timer.RemainingSeconds;`

**RemainingTime**
- Thời gian còn lại dưới dạng TimeSpan
- Kiểu: `TimeSpan`
- Ví dụ: `TimeSpan time = timer.RemainingTime;`

**TotalDuration**
- Tổng thời gian ban đầu (giây)
- Kiểu: `float`
- Ví dụ: `float total = timer.TotalDuration;`

**IsActive**
- Kiểm tra bộ đếm có đang hoạt động không
- Kiểu: `bool`
- Ví dụ: `if (timer.IsActive) { /* Đang hoạt động */ }`

**IsExpired**
- Kiểm tra bộ đếm đã kết thúc chưa
- Kiểu: `bool`
- Ví dụ: `if (timer.IsExpired) { /* Đã kết thúc */ }`

#### Sự kiện

**OnUpdate**
- Được kích hoạt khi bộ đếm được cập nhật
- Kiểu sự kiện: `Action<float>`
- Ví dụ:
  ```csharp
  timer.OnUpdate += (remainingSeconds) => {
      Debug.Log($"Còn lại: {remainingSeconds}s");
  };
  ```

**OnComplete**
- Được kích hoạt khi bộ đếm hoàn thành
- Kiểu sự kiện: `Action`
- Ví dụ:
  ```csharp
  timer.OnComplete += () => {
      Debug.Log("Bộ đếm đã hoàn thành!");
  };
  ```

#### Phương thức

**UpdateRealTime()**
- Cập nhật trạng thái bộ đếm theo thời gian thực
- Ví dụ: `timer.UpdateRealTime();`

**GetSaveData()**
- Lấy dữ liệu để lưu trữ
- Trả về: `CountdownTimerData`
- Ví dụ: `var data = timer.GetSaveData();`

**InitializeFromSaveData(CountdownTimerData data)**
- Khởi tạo bộ đếm từ dữ liệu đã lưu
- Tham số: `data` - Dữ liệu đã lưu
- Trả về: `bool` - True nếu khởi tạo thành công
- Ví dụ: `bool success = timer.InitializeFromSaveData(savedData);`

**Complete()**
- Dừng bộ đếm và kích hoạt sự kiện hoàn thành
- Ví dụ: `timer.Complete();`

**Reset(float newDuration)**
- Làm mới bộ đếm với thời gian mới
- Tham số: `newDuration` - Thời gian mới (giây)
- Ví dụ: `timer.Reset(60f);`

## Tùy chọn cấu hình

### Cài đặt TimeScheduleManager

#### Cài đặt chung

**Auto Initialize** (bool)
- Tự động khởi tạo khi Start
- Mặc định: true
- Ví dụ: Đặt thành false để khởi tạo thủ công

**Enable Persistence** (bool)
- Bật tính năng lưu trữ và khôi phục
- Mặc định: true
- Ví dụ: Đặt thành false để tắt persistence

**Update Interval** (float)
- Khoảng thời gian cập nhật bộ đếm (giây)
- Phạm vi: 0.01f - 1.0f
- Mặc định: 0.1f
- Ví dụ: Đặt thành 0.05f cho cập nhật nhanh hơn

#### Cài đặt Debug

**Debug Mode** (bool)
- Bật log debug
- Mặc định: false
- Ví dụ: Đặt thành true trong quá trình phát triển

**Log Level** (enum)
- Mức độ log (None, Error, Warning, Info, Debug)
- Mặc định: Warning
- Ví dụ: Đặt thành Debug để xem tất cả log

### Cấu hình Runtime

Bạn có thể thay đổi cấu hình trong runtime:

```csharp
// Lấy manager instance
var manager = FindObjectOfType<TimeScheduleManager>();

// Cập nhật cấu hình
manager.UpdateInterval = 0.05f;
manager.EnableDebugMode = true;
```

## Khắc phục sự cố

### Vấn đề thường gặp

#### Vấn đề: "Bộ đếm không khởi tạo"
**Triệu chứng:**
- TimeScheduleManager không khởi tạo
- Không phản hồi khi gọi phương thức bộ đếm

**Giải pháp:**
1. Kiểm tra TimeScheduleManager GameObject đã được tạo chưa
2. Xác minh component TimeScheduleManager đã được thêm chưa
3. Kiểm tra Console có thông báo lỗi không
4. Đảm bảo tất cả dependencies đã được import đúng

#### Vấn đề: "Bộ đếm không persist sau khi restart app"
**Triệu chứng:**
- Bộ đếm bị mất khi đóng và mở lại ứng dụng
- Không khôi phục được trạng thái đã lưu

**Giải pháp:**
1. Kiểm tra Enable Persistence đã được bật chưa
2. Xác minh PlayerPrefs có quyền ghi không
3. Kiểm tra SaveAllTimers() được gọi khi app pause/close
4. Kiểm tra LoadAllTimers() được gọi khi app start/resume

#### Vấn đề: "Bộ đếm không cập nhật"
**Triệu chứng:**
- Thời gian còn lại không thay đổi
- Sự kiện OnUpdate không được kích hoạt

**Giải pháp:**
1. Kiểm tra UpdateServiceManager hoạt động đúng
2. Xác minh không có exception trong timer logic
3. Kiểm tra TimeExtensions.GetCurrentUtcTimestampInSeconds() hoạt động
4. Đảm bảo bộ đếm chưa expired

#### Vấn đề: "Memory leaks"
**Triệu chứng:**
- Memory usage tăng liên tục
- Performance giảm theo thời gian

**Giải pháp:**
1. Luôn unsubscribe events khi không cần thiết
2. Sử dụng Dispose() cho custom timers
3. Kiểm tra không có circular references
4. Sử dụng WeakReference cho cross-object references

### Mẹo Debug

**Bật Debug Mode:**
```csharp
var manager = FindObjectOfType<TimeScheduleManager>();
manager.EnableDebugMode = true;
```

**Kiểm tra trạng thái bộ đếm:**
```csharp
Debug.Log($"Số bộ đếm đang hoạt động: {TimeScheduleManager.ActiveTimerCount}");
Debug.Log($"Bộ đếm tồn tại: {TimeScheduleManager.HasCountdownTimer("my_timer")}");
```

**Kiểm tra dữ liệu đã lưu:**
```csharp
string savedData = PlayerPrefs.GetString("CountdownTimers", "");
Debug.Log($"Dữ liệu đã lưu: {savedData}");
```

## Ví dụ thực tế

### Ví dụ 1: Bộ đếm cooldown kỹ năng

```csharp
public class SkillCooldown : MonoBehaviour
{
    [SerializeField] private float cooldownDuration = 30f;
    [SerializeField] private Button skillButton;
    [SerializeField] private Image cooldownFill;
    
    private void Start()
    {
        // Kiểm tra cooldown còn lại khi khởi động
        var timer = TimeScheduleManager.GetCountdownTimer("skill_cooldown");
        if (timer != null && timer.IsActive)
        {
            this.skillButton.interactable = false;
            this.StartCooldownUI(timer);
        }
    }
    
    public void UseSkill()
    {
        // Tạo bộ đếm cooldown
        var timer = TimeScheduleManager.StartCountdownTimer("skill_cooldown", this.cooldownDuration);
        
        this.skillButton.interactable = false;
        this.StartCooldownUI(timer);
        
        Debug.Log("Đã sử dụng kỹ năng!");
    }
    
    private void StartCooldownUI(ICountdownTimer timer)
    {
        timer.OnUpdate += (remaining) => {
            float progress = remaining / timer.TotalDuration;
            this.cooldownFill.fillAmount = progress;
        };
        
        timer.OnComplete += () => {
            this.skillButton.interactable = true;
            this.cooldownFill.fillAmount = 1f;
            Debug.Log("Kỹ năng đã sẵn sàng!");
        };
    }
}
```

### Ví dụ 2: Bộ đếm thời gian daily reward

```csharp
public class DailyRewardManager : MonoBehaviour
{
    private const string DAILY_REWARD_TIMER = "daily_reward_timer";
    private const float DAILY_REWARD_COOLDOWN = 24 * 60 * 60f; // 24 giờ
    
    [SerializeField] private Button claimButton;
    [SerializeField] private Text timeRemainingText;
    
    private void Start()
    {
        this.UpdateUI();
    }
    
    public void ClaimDailyReward()
    {
        if (this.CanClaimDailyReward())
        {
            this.GiveReward();
            this.StartDailyRewardTimer();
            this.UpdateUI();
        }
    }
    
    private bool CanClaimDailyReward()
    {
        var timer = TimeScheduleManager.GetCountdownTimer(DAILY_REWARD_TIMER);
        return timer == null || !timer.IsActive;
    }
    
    private void StartDailyRewardTimer()
    {
        var timer = TimeScheduleManager.StartCountdownTimer(DAILY_REWARD_TIMER, DAILY_REWARD_COOLDOWN);
        
        timer.OnUpdate += (remaining) => {
            TimeSpan timeSpan = TimeSpan.FromSeconds(remaining);
            this.timeRemainingText.text = $"Còn lại: {timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        };
        
        timer.OnComplete += () => {
            this.UpdateUI();
            Debug.Log("Daily reward đã sẵn sàng!");
        };
    }
    
    private void UpdateUI()
    {
        bool canClaim = this.CanClaimDailyReward();
        this.claimButton.interactable = canClaim;
        this.timeRemainingText.gameObject.SetActive(!canClaim);
        
        if (!canClaim)
        {
            var timer = TimeScheduleManager.GetCountdownTimer(DAILY_REWARD_TIMER);
            if (timer != null)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(timer.RemainingSeconds);
                this.timeRemainingText.text = $"Còn lại: {timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
        }
    }
}
```

### Ví dụ 3: Bộ đếm thời gian sự kiện

```csharp
public class EventTimer : MonoBehaviour
{
    [SerializeField] private string eventKey = "special_event";
    [SerializeField] private float eventDuration = 3600f; // 1 giờ
    [SerializeField] private Text eventStatusText;
    [SerializeField] private Slider eventProgressSlider;
    
    private void Start()
    {
        this.InitializeEvent();
    }
    
    private void InitializeEvent()
    {
        if (!TimeScheduleManager.HasCountdownTimer(this.eventKey))
        {
            this.StartEvent();
        }
        else
        {
            this.ContinueEvent();
        }
    }
    
    private void StartEvent()
    {
        var timer = TimeScheduleManager.StartCountdownTimer(this.eventKey, this.eventDuration);
        this.SetupEventUI(timer);
        this.eventStatusText.text = "Sự kiện đã bắt đầu!";
        Debug.Log("Sự kiện đã bắt đầu!");
    }
    
    private void ContinueEvent()
    {
        var timer = TimeScheduleManager.GetCountdownTimer(this.eventKey);
        if (timer != null && timer.IsActive)
        {
            this.SetupEventUI(timer);
            this.eventStatusText.text = "Tiếp tục sự kiện từ trạng thái đã lưu!";
            Debug.Log("Tiếp tục sự kiện từ trạng thái đã lưu!");
        }
        else
        {
            this.EndEvent();
        }
    }
    
    private void SetupEventUI(ICountdownTimer timer)
    {
        timer.OnUpdate += (remaining) => {
            float progress = (timer.TotalDuration - remaining) / timer.TotalDuration;
            this.eventProgressSlider.value = progress;
            
            TimeSpan timeSpan = TimeSpan.FromSeconds(remaining);
            this.eventStatusText.text = $"Sự kiện còn lại: {timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        };
        
        timer.OnComplete += () => {
            this.EndEvent();
        };
    }
    
    private void EndEvent()
    {
        this.eventStatusText.text = "Sự kiện đã kết thúc!";
        this.eventProgressSlider.value = 1f;
        Debug.Log("Sự kiện đã kết thúc!");
    }
}
```

## Tối ưu hiệu suất

### Caching và Efficient Updates
- Sử dụng Unix timestamp để tránh tính toán phức tạp
- Cache `RemainingSeconds` để giảm tính toán
- Chỉ cập nhật khi cần thiết với Update Interval

### Memory Management
- Tự động dispose timers khi hoàn thành
- Weak references để tránh memory leaks
- Efficient collection management với Dictionary

### Persistence Optimization
- Lưu trữ dạng JSON để tiết kiệm space
- Chỉ lưu timers đang hoạt động
- Lazy loading khi cần thiết

## Lưu ý quan trọng

1. **Thời gian UTC**: Hệ thống sử dụng UTC time để đảm bảo consistency across timezones
2. **Automatic Cleanup**: Timers tự động bị xóa khi hết hạn
3. **Thread Safety**: Không thread-safe, chỉ sử dụng trên main thread
4. **Memory**: Timers được cache trong memory, dispose khi không cần thiết
5. **Performance**: Sử dụng Update Interval để kiểm soát tần suất cập nhật

## Changelog

- **v1.0.0**: Phiên bản đầu tiên với real-time countdown timers và persistence
- Hỗ trợ Unix timestamp cho accuracy cao
- Tích hợp với TimeExtensions utility
- Tối ưu performance với caching và efficient updates
- Event-driven architecture với OnUpdate và OnComplete
- Singleton service pattern cho dễ sử dụng
