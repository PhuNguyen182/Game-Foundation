# Game Foundation Checklist — Unity Mid-core / Hard-core

> Tài liệu này mô tả các thành phần **điều kiện cần** và **điều kiện đủ** của một bộ Game Foundation cho dự án Unity quy mô mid-core/hard-core, bao gồm cả PC/Console và Mobile.

---

## Kiến trúc tổng quan

```
┌─────────────────────────────────────────────────────────┐
│                     Feature Layer                       │
│     Gameplay · UI · Shop · Meta · Live Ops · ...        │
└───────────────────────┬─────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────┐
│                     Service Layer                       │
│   Analytics · IAP · Ads · SaveData · Notification · ... │
└───────────────────────┬─────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────┐
│              Foundation Core  ← Điều kiện cần           │
│  ┌──────────────────┐  ┌────────────────┐  ┌──────────┐ │
│  │ Service Locator  │  │  Event System  │  │  Scene   │ │
│  │      + DI        │  │  MessageBus /  │  │  Mgmt    │ │
│  │                  │  │    Signal      │  │          │ │
│  └──────────────────┘  └────────────────┘  └──────────┘ │
│  ┌──────────────────┐  ┌────────────────┐  ┌──────────┐ │
│  │  State Machine   │  │   Resource     │  │   Data   │ │
│  │ App / Game / UI  │  │  Management    │  │Persist.  │ │
│  │      FSM         │  │ Addressables   │  │          │ │
│  └──────────────────┘  └────────────────┘  └──────────┘ │
└───────────────────────┬─────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────┐
│          Utility & Extension Layer  ← Điều kiện đủ      │
│   UI Framework · Config System · Async · Debug Logger   │
└───────────────────────┬─────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────┐
│              Platform Abstraction Layer                  │
│      Unity Engine API · iOS · Android · PC · Console    │
└─────────────────────────────────────────────────────────┘
```

---

## 🔴 Điều kiện cần — Foundation Core

> Thiếu một trong số này thì foundation không thể hoạt động, hoặc các feature/service không thể tích hợp được.

---

### 1. Service Locator / Dependency Injection

- [ ] Có interface/abstract cho từng service — không depend vào concrete class
- [ ] Runtime registration và resolution không cần biết implementation
- [ ] Support lazy initialization
- [ ] Detect circular dependency và throw lỗi rõ ràng ở môi trường dev
- [ ] Không dùng static singleton bừa bãi — mọi singleton phải đi qua locator

**Tech-stack đề xuất:**
- [VContainer](https://github.com/hadashiA/VContainer) — nhẹ, fast, được cộng đồng Unity tin dùng nhất hiện nay, hỗ trợ Scope/Lifetime rõ ràng
- Hoặc tự viết `ServiceLocator` đơn giản nếu muốn zero-dependency

---

### 2. Event System / Message Bus

- [ ] Strongly-typed event — không dùng string làm key
- [ ] Subscribe/Unsubscribe sạch, không leak listener
- [ ] Có priority order nếu cần (một số event cần xử lý theo thứ tự)
- [ ] Support one-shot listener (subscribe 1 lần rồi auto-unsubscribe)
- [ ] Không block main thread — event fire phải synchronous và nhẹ

**Tech-stack đề xuất:**
- [MessagePipe](https://github.com/Cysharp/MessagePipe) — strongly-typed, tích hợp DI tốt, hỗ trợ async
- Hoặc [UniRx](https://github.com/neuecc/UniRx) nếu project dùng Reactive

---

### 3. Scene Management

- [ ] Additive scene loading — không dùng `LoadScene` đơn độc
- [ ] Async load/unload với progress callback
- [ ] Scene dependency graph — scene A cần scene B phải được khai báo, không hardcode
- [ ] Transition handler (fade, loading screen) tách khỏi logic load
- [ ] Không leak object giữa các scene — lifecycle phải clear

**Tech-stack đề xuất:**
- Unity Addressables + custom `SceneManager` wrapper
- [UniTask](https://github.com/Cysharp/UniTask) cho async operations

---

### 4. State Machine — App/Game FSM

- [ ] Tối thiểu 2 cấp: `AppState` (Boot → MainMenu → Gameplay → ...) và `GameState` (Playing → Paused → GameOver)
- [ ] Enter/Exit/Tick rõ ràng cho mỗi state
- [ ] Transition guard — không cho phép transition không hợp lệ
- [ ] State có thể push/pop (Stack FSM) cho trường hợp overlay UI

**Tech-stack đề xuất:**
- Tự viết hoặc dùng [Stateless](https://github.com/dotnet-state-machine/stateless) port cho Unity
- Với game phức tạp hơn: Hierarchical State Machine tự implement, không cần lib

---

### 5. Resource Management

- [ ] **Tất cả asset đều qua Addressables** — không dùng `Resources.Load` trong production
- [ ] Object Pooling tích hợp sẵn — không `Instantiate`/`Destroy` raw trong gameplay
- [ ] Reference counting cho asset đang được sử dụng
- [ ] Async load với cancellation token
- [ ] Memory budget per category (texture, audio, prefab)

**Tech-stack đề xuất:**
- [Unity Addressables](https://docs.unity3d.com/Manual/com.unity.addressables.html) — bắt buộc
- [UniTask](https://github.com/Cysharp/UniTask) cho async
- Custom `GenericObjectPool` dựa trên `IObjectPool<T>` của Unity

---

### 6. Data Persistence (Save/Load)

- [ ] Abstraction layer — code gameplay không gọi trực tiếp `PlayerPrefs` hay `File.Write`
- [ ] Versioning schema — khi update game, data cũ không bị corrupt
- [ ] Serialize/Deserialize rõ ràng — không dùng `BinaryFormatter` (deprecated + unsafe)
- [ ] Encryption cho data nhạy cảm (currency, progress)
- [ ] Cloud save interface — dù chưa implement, phải có chỗ để plug vào

**Tech-stack đề xuất:**
- [Newtonsoft Json.NET](https://www.newtonsoft.com/json) — stable, feature-rich
- Hoặc [MemoryPack](https://github.com/Cysharp/MemoryPack) nếu cần performance cao (binary)
- Lưu file: `Application.persistentDataPath` + custom `ISaveStorage` interface

---

## 🟡 Điều kiện đủ — Utility & Extension Layer

> Không có những phần này foundation vẫn chạy, nhưng sẽ khó scale và team sẽ viết lại code liên tục.

---

### 7. UI Framework

- [ ] MVVM hoặc MVP pattern — View không chứa business logic
- [ ] Panel/Screen manager — quản lý stack UI (push/pop screen)
- [ ] Safe area support (notch, rounded corner mobile)
- [ ] Tween/Animation đi qua abstraction — không hardcode trong từng component
- [ ] UI pooling cho list/grid có nhiều item

**Tech-stack đề xuất:**
- [UI Toolkit](https://docs.unity3d.com/Manual/UIElements.html) (Unity 2022+) — cho PC/Console
- uGUI + custom framework — cho mobile nếu cần tương thích rộng
- [DOTween](https://github.com/Demigiant/dotween) — standard cho tween/animation UI

---

### 8. Config System / Data-driven Design

- [ ] Game data khai báo bằng ScriptableObject hoặc external data (JSON/CSV)
- [ ] Hot-reload config trong Editor
- [ ] Remote config interface — A/B testing và Live Ops cần plug vào đây
- [ ] Validate data ở load time — không để bug runtime vì thiếu config

**Tech-stack đề xuất:**
- ScriptableObject cho static data
- Custom Google Sheets → CSV exporter cho game designer
- [Unity Remote Config](https://unity.com/products/remote-config) hoặc Firebase Remote Config

---

### 9. Async / Coroutine Management

- [ ] **Thay toàn bộ Coroutine bằng UniTask** — cancellation, exception handling, performance tốt hơn nhiều
- [ ] Tất cả I/O operations là async
- [ ] CancellationToken propagation — destroy object phải cancel task của nó
- [ ] Không await trên main thread cho heavy computation — dùng `UniTask.RunOnThreadPool`

**Tech-stack đề xuất:**
- [UniTask](https://github.com/Cysharp/UniTask) — không có lý do gì để không dùng

---

### 10. Debug & Logging System

- [ ] Custom logger wrap `UnityEngine.Debug` — strip hoàn toàn trong Release build
- [ ] Log level: Verbose / Info / Warning / Error
- [ ] Tag/Category cho log — filter theo module khi debug
- [ ] Remote logging interface (crash reporting plug vào đây)
- [ ] In-game console (optional nhưng rất hữu ích cho QA)

**Tech-stack đề xuất:**
- Custom `ILogger` implementation + `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- [Firebase Crashlytics](https://firebase.google.com/docs/crashlytics) cho crash report (mobile)
- [Serilog](https://serilog.net/) nếu cần structured logging

---

### 11. Audio System

- [ ] Abstraction layer — gameplay không gọi trực tiếp `AudioSource`
- [ ] 2D/3D audio tách biệt
- [ ] Volume channel (Master/Music/SFX/Voice) lưu vào persistence
- [ ] Audio pooling
- [ ] Fade in/out và crossfade

**Tech-stack đề xuất:**
- [FMOD](https://www.fmod.com/) — standard cho hardcore/mid-core, nhất là PC/Console
- [Wwise](https://www.audiokinetic.com/products/wwise/) — lựa chọn thay thế tương đương
- Unity Audio + custom wrapper nếu game nhỏ hơn hoặc budget không cho phép

---

### 12. Localization

- [ ] String key → translated value — không hardcode string trong code
- [ ] Font switching theo ngôn ngữ
- [ ] RTL language support (nếu cần thị trường Arab)
- [ ] Dynamic content injection (`{0} coins` → `5 coins`)

**Tech-stack đề xuất:**
- [Unity Localization Package](https://docs.unity3d.com/Packages/com.unity.localization@latest) — official, đủ dùng

---

## 🟢 Phần bổ sung theo nền tảng

### PC / Console

| Hạng mục | Nội dung | Tech-stack |
|---|---|---|
| Input System | Rebindable, multi-device, gamepad/KB-M | Unity New Input System |
| Platform HAL | Abstraction cho PS5/Xbox/Switch API | Custom `IPlatformService` |
| Achievement/Trophy | Interface chung, implementation per-platform | Custom + platform SDK |
| Build Pipeline | Automated build, versioning, signing | [GameCI](https://game.ci/) + GitHub Actions |
| Performance Profiling | CPU/GPU markers tích hợp sẵn | Unity Profiler Markers + custom wrapper |

---

### Mobile

| Hạng mục | Nội dung | Tech-stack |
|---|---|---|
| IAP | Interface → implementation per-store | Unity IAP |
| Ads | Interface → mediation layer | [IronSource](https://www.is.com/) hoặc [MAX (AppLovin)](https://www.applovin.com/max/) |
| Push Notification | Interface + Firebase implement | Firebase Cloud Messaging |
| App Lifecycle | Foreground/Background/Kill handling | Custom `AppLifecycleService` |
| Battery/Thermal | Frame rate throttle khi device nóng | iOS Thermal API / Android PowerManager |
| Store Review | Trigger đúng lúc | Unity Player Reviews hoặc native |
| Deep Link | Schema routing cho campaign | Custom + Firebase Dynamic Links |

---

## ⚠️ Những điều tuyệt đối không nên làm trong Foundation

- Dùng `FindObjectOfType` ở bất cứ đâu trong foundation core — anti-pattern làm chậm và tạo coupling ngầm
- Có circular dependency giữa các service
- Foundation biết về gameplay logic — foundation chỉ cung cấp infrastructure, không biết game đang làm gì
- Hardcode platform check (`#if UNITY_ANDROID`) trong business logic — phải qua abstraction layer
- Dùng `Resources.Load` trong production code
- Dùng `BinaryFormatter` cho serialization

---

## 📋 Thứ tự xây dựng đề xuất

```
1. Platform Abstraction + Logger
       ↓
2. Service Locator / DI
       ↓
3. Event System
       ↓
4. Resource Management + Addressables
       ↓
5. Data Persistence
       ↓
6. Scene Management + State Machine
       ↓
7. UI Framework
       ↓
8. Các utility còn lại theo ưu tiên dự án
```

---

## 📦 Tổng hợp tech-stack

| Package | Mục đích | Link |
|---|---|---|
| VContainer | Dependency Injection | https://github.com/hadashiA/VContainer |
| UniTask | Async/Await thay Coroutine | https://github.com/Cysharp/UniTask |
| MessagePipe | Event System / Message Bus | https://github.com/Cysharp/MessagePipe |
| Unity Addressables | Resource Management | Unity Package Manager |
| MemoryPack | High-performance binary serializer | https://github.com/Cysharp/MemoryPack |
| Newtonsoft Json.NET | JSON serialization | Unity Package Manager |
| DOTween | Tween / Animation | https://github.com/Demigiant/dotween |
| FMOD | Audio System | https://www.fmod.com/ |
| Unity Localization | Localization | Unity Package Manager |
| Unity New Input System | Input | Unity Package Manager |
| Unity Remote Config | Remote Config | Unity Package Manager |
| Unity IAP | In-App Purchase | Unity Package Manager |
| Firebase Crashlytics | Crash Reporting | Firebase SDK |
| GameCI | Build Pipeline | https://game.ci/ |
| MAX (AppLovin) / IronSource | Ad Mediation | SDK riêng |

> **Lưu ý:** Tất cả package của Cysharp (VContainer, UniTask, MessagePipe, MemoryPack) đều dùng chung triết lý thiết kế, tích hợp tốt với nhau và đều thuộc nhóm được cộng đồng Unity tin dùng nhất hiện nay.
