# CompleteTimer: review và plan viết lại

> Tổng hợp phiên review ngày 2026-09-23/24. **Chưa sửa dòng code nào.** Phiên sau bắt đầu từ mục [Bắt đầu phiên sau](#0-bắt-đầu-phiên-sau).

**Mục tiêu:** dùng cho game mid-core trở lên (nông trại, RPG, puzzle có Lives). Yêu cầu: hàng nghìn timer chạy cùng lúc, tiến trình offline đúng, không mất dữ liệu, khó gian lận. Các use case chính:
- Cây trồng sinh trưởng qua nhiều giai đoạn.
- Nhà máy sản xuất theo hàng đợi.
- Lives/Energy hồi theo thời gian.

---

## 0. Bắt đầu phiên sau
Thứ tự triển khai đề xuất. Mỗi bước kết thúc bằng một lần compile-check (và chạy test nếu bước đó có test):
1. Tạo asmdef core `CompleteTimer/` và `Clock/` (`ITimeProvider`, `TimerClock`). Viết `TimerClockTests`.
2. Viết `Scheduling/` (heap, record, scheduler, snapshot, delivery buffer). Viết `TimerSchedulerTests`, `MultiStageTierTests`, `TimerSnapshotTests`, `RealTimeElapsedTests`, `DeliveryGuaranteeTests`, `TimerSchedulerPerfTests`.
3. Viết `Production/` và `ProductionQueueTests`.
4. Viết `Regeneration/` và `RegenerationCounterTests`.
5. Thêm hook `OnBeforeSave` vào `DynamicGameDataController`, rồi viết `CompleteTimerIntegration/` (save data, controller, runtime, registration).
6. `git rm` code cũ trong `Core/` và `Models/`. Wiring vào `SampleProjectLifetimeScope`. Chạy verification (mục 8).

---

## 1. Tóm tắt review code cũ

**Kết luận:** chưa dùng được, kể cả cho game casual. Code cũ không có caller nào và file save cũ vốn đã hỏng, nên chọn **viết lại hoàn toàn**.

### 1.1 Lỗi nghiêm trọng
| # | Lỗi | Vị trí |
|---|---|---|
| A1 | `TimeSpan.FromSeconds(x).Milliseconds` chỉ trả **phần lẻ** 0..999, nên timer 60s thành 0ms | `TimerOrchestrator.cs:45,67`, `TimerCounterUnit.cs:106` |
| A2 | Mỗi frame, `TimerUpdate()` lấy `now − StartUnixTime` (tổng thời gian đã trôi) trừ vào `TicksByTier` rồi **ghi đè** kết quả, nên thời gian bị trừ lặp và timer về 0 sớm hơn nhiều. Multi-tier cũng bị trừ lặp phần thời gian của tier trước. Save/load mang theo dữ liệu đã bị trừ | `TimerCounterUnit.TimerUpdate` |
| A3 | Timer mới không bao giờ được thêm vào registry và không được `Activate()`, nên không tick, không save. Gọi lại với cùng id sẽ tạo thêm instance mới. Timer load từ file không được gắn `OnTimerRemoved` | `TimerOrchestrator.RegisterTimer` |
| A4 | Xoá timer trong callback `OnTimerCompleted` làm thay đổi `Dictionary` đang được `foreach`, gây `InvalidOperationException` | `TimerRegistry.Tick` |
| A5 | `TimerSaveUnitModel` thiếu `[MemoryPackable]`, save/load hỏng mà không báo gì (exception bị catch). Không có version-tolerant | `Models/` |
| A6 | Dữ liệu save và `TimerModel` dùng chung một `List`. `Dispose` gọi `Clear()` làm mất luôn dữ liệu save | `TimerDataController.cs:50,62` |

### 1.2 Lỗi trung bình
- `Dispose` không set null cho `OnTimerRemoved`.
- `TimerRegistry` tự `new TimeValidator()` thay vì dùng instance được inject.
- `DisposableBag` không có Remove, nên rò rỉ bộ nhớ theo thời gian chơi.
- Có finalizer mà không cần.
- Không deregister khỏi `UpdateServiceManager`.
- Mỗi lần xoá timer ghi file đồng bộ một lần.
- Ghi file không atomic.
- File thiếu thì log Error, dù đó là trường hợp bình thường ở lần chạy đầu.
- Callback là `public Action` field thay vì `event`.
- `OnTimerCompleted` bắn trong `Activate()` trước khi caller kịp subscribe.
- Event hoàn thành trễ 1 frame.
- `DateTimeOffset(DateTime Unspecified)` bị hiểu là giờ local.
- `IsNewDay` có thứ tự tham số ngược.
- Có code không ai dùng.

### 1.3 Hiệu suất
Code cũ mỗi frame, cho **từng** timer, gọi `DateTime.UtcNow` + chạy vòng lặp tier + bắn callback `OnTimerUpdate`. Tổng chi phí là O(N) mỗi frame và UI bị cập nhật liên tục. Cách khắc phục:
- Lấy `now` 1 lần/frame.
- **Min-heap** theo deadline: mỗi frame chỉ xem phần tử đầu, O(1).
- **Pull model** cho UI.
- Handle dạng struct.
- Save theo cơ chế dirty + debounce.

### 1.4 Thiếu cho mid-core
- Chống chỉnh giờ.
- Tiến trình offline đúng thứ tự.
- Pause/Resume, SpeedUp/Skip.
- Hàng đợi sản xuất.
- Lives/Energy.
- Lưu chung transaction với save chính (tránh lệch dữ liệu gây dupe).
- Event hoàn thành bền vững.
- Test được.

---

## 2. Quyết định đã chốt
| Câu hỏi | Chọn |
|---|---|
| Hướng xử lý | **Viết lại** |
| Nguồn thời gian | **Pluggable** `ITimeProvider`: mặc định dùng giờ thiết bị + monotonic, game cắm server time khi có |
| Tương thích API và save cũ | **Không cần**: xoá code cũ |
| Persistence | **Qua DataFlow** (`IDataController` + `SaveScheduler`) |
| Tính năng bản đầu | **Pause/Resume, SpeedUp/Skip/Delay, Production queue, Multi-stage** + (bổ sung sau Q&A) **RegenerationCounter** cho Lives |

## 3. Hỏi đáp quan trọng trong phiên

**Q1: Thời gian được lưu có bị trừ đúng theo thời gian thực không?**
Có. File lưu **mốc tuyệt đối** theo UTC ms, không lưu thời gian còn lại. Remaining luôn = `StartMs + DurationMs − now`.
- Kill app bao lâu, save lúc nào cũng không ảnh hưởng: mở lại thì remaining giảm đúng bằng thời gian offline.
- Không bị trừ lặp.
- Không phụ thuộc `timeScale`/`deltaTime`.
- Múi giờ và DST không ảnh hưởng.

Các trường hợp biên đã xử lý: máy deep sleep, chỉnh giờ lùi, chỉnh giờ tới, thao tác tốn gem. Chi tiết ở mục 5.1.

**Q2: Kill app lâu hơn thời gian timer thì lúc mở lại có chắc nhận được callback không?**
Có, nhờ 3 lớp:
- (1) Buffer giữ các event chưa có listener, giao ngay khi có listener đăng ký, đúng thứ tự `AtMs`.
- (2) `Completed` là trạng thái: được báo lại mỗi phiên (`IsReplay`) cho đến khi `Release`.
- (3) Luôn hỏi trực tiếp được state.

Ngữ nghĩa là **at-least-once**, nên **trao thưởng khi Release/Claim, không trao trong callback**. Chi tiết ở mục 5.2.

**Q3: Lives (5 lượt, 30 phút hồi 1) có chính xác không?**
Không làm bằng multi-stage, vì người chơi tiêu lives ngay lúc đang hồi. Dùng `RegenerationCounter` tính bằng công thức số nguyên, **giữ nguyên phần dư** (`AnchorMs += n × Interval`), nên sai số tích luỹ bằng 0.
- Offline 70 phút với 1/5 lives → 3/5, lượt kế tiếp còn đúng 20 phút.
- Callback đến trễ tối đa ~1 frame, nhưng các mốc vẫn tính từ mốc thật.

Chi tiết ở mục 5.3.

**Q4: Timer đã đếm xong và xử lý hết thì có bị xoá khỏi data không?**
Có, nhưng **phải có người gọi `Release`**. Khi đó timer được xoá khỏi bộ nhớ và, ở lần save kế tiếp, xoá luôn khỏi file save. Cụ thể:
- **Vòng đời:** `Running` → `Completed` (vẫn nằm trong data, chờ gameplay xử lý) → `Release(h)` → slot trở về `Free`: key được giải phóng, `Version` tăng nên handle cũ vô hiệu, slot quay về free list để tái sử dụng.
- **Xoá khỏi file:** `Release` bắn `OnStructureChanged`, controller gọi `MarkDirty()`. Ở lần flush kế tiếp, `CaptureSnapshot` **chỉ lấy các slot khác `Free`**, nên file mới không còn timer đó. `AtomicFileStore` ghi đè toàn bộ payload, không có bản ghi mồ côi.
- **Timer tự dọn, không cần gameplay gọi:**
  - `ProductionQueue` release timer ngay khi item xong. Item chuyển sang `Outputs` và được xoá khi `ClaimOutputs`.
  - `RegenerationCounter` release timer của lượt hồi sau mỗi lần bắn.
- **Timer "bắn xong là thôi"** (buff, cooldown, quest hết hạn...): thêm `TimerSpec.AutoRelease = true`. Scheduler tự `Release` **sau khi event `Completed` đã được giao** cho ít nhất 1 listener. Nếu chưa giao được (chưa có listener, hoặc app bị kill trước đó) thì timer vẫn giữ `Completed` trong data và được replay ở phiên sau, nên không mất event.
- **Chống phình data do quên `Release`:** có `CompletedCount`, `GetCompletedOlderThan(ms, List<TimerHandle> into)` và cảnh báo qua `OnWarning` khi số timer Completed vượt `CompletedWarningThreshold` (mặc định 1000). Thêm test tương ứng.

**Q5: Timer theo tier. Ví dụ: cây từ gieo hạt đến trưởng thành qua 3 tier, A = 1 phút, B = 3 phút, C = 8 phút.**
Plan hỗ trợ trường hợp này bằng **multi-stage**. Trong plan này, "stage" chính là "tier" của code cũ. Chỉ cần 1 timer với `StageDurationsMs = [60 000, 180 000, 480 000]`. Scheduler lưu các mốc cộng dồn `StageEnds = [1', 4', 12']` tính từ `StartMs`.

| Thời điểm | Tier hiện tại | Event | Còn lại (tier / tổng) |
|---|---|---|---|
| 0' (gieo) | 0 (A) | — | 1' / 12' |
| 1' | 1 (B) | `StageChanged(Stage = 1, AtMs = 1')` | 3' / 11' |
| 4' | 2 (C) | `StageChanged(Stage = 2, AtMs = 4')` | 8' / 8' |
| 12' | Trưởng thành | `Completed(Stage = 3, AtMs = 12')` | 0 / 0 |

- **Offline 5' rồi mở game:**
  - Trước Tick đầu, `GetCurrentStage` đã trả 2 và tier C còn 7'. Có được điều này nhờ truy vấn tính từ `now`, xem 6.1.2.
  - Tick đầu bắn `StageChanged(1)` rồi `StageChanged(2)`, đúng `AtMs`, `IsLate = true`, nên UI có thể bỏ animation.
- **Offline 20':** giống trên, thêm `Completed(AtMs = 12')`. Cây ở trạng thái `Completed` cho đến khi thu hoạch (`Release`), xem Q4.
- **Hiệu năng:** mỗi cây chỉ có **1 entry** trong heap, là deadline của tier kế tiếp. 1000 cây × 3 tier vẫn chỉ là heap 1000 phần tử.

**Cách tier cũ có phù hợp không?** Ý tưởng thì đúng, cách lưu thì sai.
- **Ý tưởng được giữ lại:** 1 timer mang danh sách thời lượng có thứ tự. Mô hình này hợp với cây trồng, công trình nâng cấp nhiều bước, trứng ấp..., vì trình tự cố định và không bị "tiêu" giữa chừng như Lives (Q3).
- **Cách lưu phải thay:**
  - `TicksByTier` lưu thời gian **còn lại** của từng tier, rồi mỗi frame bị trừ và ghi đè (A2). Cây chín sớm hơn rất nhiều, và save mang theo dữ liệu đã hỏng.
  - Thời lượng qua `TimeSpan.Milliseconds` luôn nhỏ hơn 1 giây (A1).
  - `OnTimerTierChanged(i)` báo tier *vừa xong* nhưng không có mốc thời gian, và không sắp thứ tự được giữa nhiều cây khi xử lý offline.
  - `AddTimestamp` cộng thêm vào từng tier, tức là làm chậm chứ không tăng tốc. Không có cách bỏ qua một tier.
- **Bản mới:** lưu `StartMs` + `StageEnds` cộng dồn, không đổi sau khi tạo. Tier hiện tại và thời gian còn lại luôn được **tính** từ `now`.

**Bổ sung vào plan cho timer theo tier:**
- Ngữ nghĩa tier: xem mục 4.
- API truy vấn theo tier và `SkipCurrentStage` (bón phân): xem 6.1.2.
- `DispatchedStage` thay cho `CurrentStage` trong record/snapshot: xem 6.1.
- Test `MultiStageTierTests`: xem 6.5.
- **Mở rộng sau, không làm ở bản đầu:** tier cần thao tác mới qua được, ví dụ tưới nước. Thêm `TimerSpec.PauseAtStageEnd`: khi vào tier mới, timer tự chuyển `Paused` với `PausedAtMs = AtMs` (mốc lý thuyết, không phải `now`), nên thời gian offline sau mốc không bị tính. Người chơi tưới thì gọi `Resume`. Việc này phải làm trong scheduler, vì gameplay không thể pause tại một mốc đã nằm trong quá khứ.

---

## 4. Nguyên tắc thiết kế
- **Lưu mốc bất biến, tính remaining khi cần.** Không bao giờ trừ dồn vào dữ liệu.
- **Một scheduler duy nhất và min-heap** theo deadline kế tiếp. Mỗi frame chỉ lấy `now` 1 lần và xem heap top, O(1). Timer đến hạn thì pop, O(log N). **Không allocate** trên hot path.
- **Pull model cho UI**: `GetRemainingMs/GetProgress01` chỉ cho các ô đang hiển thị. Không có callback mỗi frame.
- **State là nguồn sự thật, event là thông báo.**
- **Ngữ nghĩa tier (stage)** (xem Q5):
  - N thời lượng tạo ra các tier `0..N−1`. `StageEnds[k]` là mốc cộng dồn tính từ `StartMs`.
  - Khi vào tier k (k ≥ 1): `StageChanged(Stage = k, AtMs = StartMs + StageEnds[k−1])`.
  - Hết tier cuối thì bắn `Completed(Stage = N)`, và "trưởng thành" chính là `Completed`.
  - Timer 1 tier (`DurationMs`) là trường hợp N = 1.
  - `TryStart` từ chối thời lượng ≤ 0.
  - **Thời lượng được copy vào timer lúc tạo**, nên designer đổi config về sau thì cây đã gieo không bị nhảy giờ. Muốn áp dụng hồi tố thì làm bằng migration.
- **Core thuần C#** (`noEngineReferences`) để test EditMode với fake clock. Phần tích hợp Unity, VContainer và DataFlow ở Assembly-CSharp, vì DataFlow Runtime (`DynamicGameDataController`, `SaveScheduler`, `DataFlowRegistration`) không có asmdef.

## 5. Các đảm bảo chính

### 5.1 Thời gian trôi theo đúng thời gian thực
- **Tắt app hay bị kill**: `StartMs` không đổi, `now` tăng đúng bằng thời gian offline.
- **Pause khi offline**: `Resume` dời mốc đi `now − PausedAtMs`, nên timer đứng yên suốt thời gian pause.
- **Hàng đợi khi offline**: item kế tiếp bắt đầu tại `AtMs` của item trước, **không phải tại now**.
- **Máy deep sleep khi app ở background**: `Stopwatch` (CLOCK_MONOTONIC Android) ngừng chạy. Xử lý bằng:
  - (a) `ReanchorFromDevice()` khi `Application.focusChanged(true)`.
  - (b) Kiểm tra drift mỗi Tick: đọc `DateTime.UtcNow` **1 lần/frame cho cả hệ**. Nếu giờ thiết bị vượt đồng hồ neo quá `DriftToleranceMs` (mặc định 2000) thì re-anchor.
  - Nguồn monotonic pluggable, sau này có thể dùng `SystemClock.elapsedRealtime()`.
- **Chỉnh giờ lùi**: `now` không nhỏ hơn giá trị cao nhất từng thấy (`ApplyFloor(LastSeenUtcMs)` lúc load và kẹp trong phiên). Timer đứng yên thay vì chạy lùi, bắn `OnAnomaly(Backward)`.
- **Chỉnh giờ tới**: không phát hiện được nếu không có server; ghi rõ trong doc. Khi dùng server: sau khi quay lại từ background thì `IsServerSynced = false` và bắn `OnResyncRequired`. Trong lúc chờ, tạm tính bằng delta giờ thiết bị; game có thể chặn thu hoạch hoặc speed-up.
- **Thay đổi sau lần save cuối**: `SaveScheduler` flush khi suspend hoặc quit. Thao tác tốn gem thì gọi `CompleteTimerDataController.SaveNow()` cùng lúc với save ví tiền.

### 5.2 Giao callback sau khi kill app
1. **Buffer `_undelivered`**: event được coi là đã giao khi có ít nhất 1 listener nhận (listener riêng của timer hoặc của channel). Nếu chưa có, event được giữ lại theo thứ tự `AtMs`. `AddChannelListener` và `SetListener` giao ngay các event đang chờ khớp với listener đó. Áp dụng cho cả `StageChanged` và `Completed`.
2. **Replay Completed**: khi `Restore`, timer `Completed` chưa `Release` sẽ phát lại `Completed` với `IsLate = true`, `IsReplay = true`. Timer Running đã hết hạn khi offline thì được xử lý ở Tick đầu tiên, có đủ các `StageChanged` bị vượt qua, rồi `Completed`.
3. **Pull state**: `GetState`, `GetCurrentStage`, `GetEndUtcMs` luôn đúng.

`ProcessingEnabled` chỉ là tuỳ chọn, mặc định true.

### 5.3 RegenerationCounter (Lives / Energy / Stamina)
- State: `Stored`, `Max`, `IntervalMs`, `AnchorMs` (mốc bắt đầu lượt hồi đang chạy).
- `GetCurrent(now) = min(Max, Stored + (now − AnchorMs) / IntervalMs)`, chia nguyên long.
- `Normalize(now)`: `Stored += n`, **`AnchorMs += n × IntervalMs`** (giữ phần dư). Nếu đầy thì `AnchorMs = now`.
- `GetNextRegenRemainingMs(now) = IntervalMs − (now − AnchorMs) % IntervalMs` (trả 0 nếu đầy). `GetFullRemainingMs(now)`.
- `TryConsume(count)`: gọi Normalize trước. Nếu đang đầy thì `AnchorMs = now`, rồi `Stored -= count`. Tiêu lives khi đang hồi dở **không** làm lượt đó bị reset.
- `Refill()`, `Add(count, allowOverMax)` (vượt Max thì ngừng hồi), `SetMax()`.
- Chỉ dùng **1 timer** trong scheduler cho lượt hồi kế tiếp. Khi timer bắn: Normalize, bắn `OnRegenerated(gained, current, lastAtMs)`, rồi đặt timer cho lượt sau nếu chưa đầy.
- Offline 2 giờ với 1/5 → một callback với `gained = 4`, O(1).

---

## 6. Cấu trúc file

### 6.1 Core: asmdef mới `DracoRuan.PrebuildServices.PlayerLoopSystem.TimeServices.CompleteTimer`
Thư mục là chính folder này (`CompleteTimer/`).
- asmdef tham chiếu `DracoRuan.PrebuildServices.PlayerLoopSystem.Handlers`.
- `noEngineReferences: true`, `autoReferenced: true`.
- **Xoá** `Core/*` và `Models/*` cũ (10 file + .meta) bằng `git rm`.

| File | Nội dung |
|---|---|
| `Clock/ITimeProvider.cs` | `long UtcNowMs { get; }` |
| `Clock/TimerClock.cs` | Xem 6.1.1 |
| `Clock/ClockAnomaly.cs` | enum `Backward`, `ForwardJump` + struct chứa độ lệch |
| `Scheduling/TimerHandle.cs` | `readonly struct { int Index; int Version; }`, `IEquatable`, `IsValid`; dùng version để chặn handle cũ |
| `Scheduling/TimerState.cs` | `Free, Running, Paused, Completed` |
| `Scheduling/TimerSpec.cs` | `Key` (duy nhất), `Channel` (int), `DurationMs` **hoặc** `StageDurationsMs` (long[]), `StartUtcMs?` (null = now; dùng khi nối chuỗi trong queue), `AutoRelease` (bool, tự Release sau khi Completed đã được giao, xem Q4) |
| `Scheduling/TimerEvent.cs` | `readonly struct { Handle, Key, Channel, Type (StageChanged/Completed), Stage, AtMs, IsLate, IsReplay }`. `AtMs` là thời điểm lý thuyết của sự kiện. `IsLate = now − AtMs > 1000` |
| `Scheduling/ITimerListener.cs` | `void OnTimerEvent(in TimerEvent e)` |
| `Scheduling/TimerRecord.cs` (internal struct) | `Key, Channel, State, StartMs, DurationMs, PausedAtMs, long[] StageEnds, DispatchedStage, NextDeadlineMs, Sequence, HeapIndex, Version, ITimerListener Listener`. `DispatchedStage` là tier cuối cùng đã bắn event, chỉ dùng cho dispatch. Tier hiện tại để hiển thị được tính từ `now` (xem 6.1.2) |
| `Scheduling/TimerMinHeap.cs` (internal) | Heap chứa index record. So sánh theo `(NextDeadlineMs, Sequence)`. Có `Push/Pop/Remove(at)/Update(at)`; vị trí heap lưu trong `record.HeapIndex` |
| `Scheduling/TimerScheduler.cs` | Xem 6.1.2 |
| `Scheduling/TimerSnapshot.cs` | `TimerEntrySnapshot { Key, Channel, State, StartMs, DurationMs, PausedAtMs, long[] StageEnds, DispatchedStage }` |
| `Production/ProductionQueue.cs` | Xem 6.1.3 |
| `Production/ProductionQueueRegistry.cs` | `Create/TryGet(queueKey)`, `Capture/Restore` |
| `Production/ProductionQueueSnapshot.cs` | `QueueKey, Capacity, Channel, Items[(ItemId, DurationMs)], Outputs[(ItemId, AtMs)], ActiveTimerKey, Sequence, IsPaused` |
| `Regeneration/RegenerationCounter.cs`, `RegenerationCounterRegistry.cs`, `RegenerationSnapshot.cs` | Xem 5.3. Snapshot gồm `{ Key, Stored, Max, IntervalMs, AnchorMs }` |

#### 6.1.1 `TimerClock : ITimeProvider`
- Nhận vào `Func<long> deviceUtcMs` và `Func<long> monotonicMs`. Mặc định là `DateTime.UtcNow` và `Stopwatch`; test inject bản giả.
- `UtcNowMs = anchorUtc + (monotonic − anchorMono)`. Chỉnh giờ máy trong phiên không làm timer nhảy.
- `SyncWithServer(long serverUtcMs)`: re-anchor theo giờ server, set `IsServerSynced = true`.
- `ReanchorFromDevice()`: gọi khi có lại focus. Giờ thiết bị lùi thì giữ nguyên và bắn `OnAnomaly(Backward)`. Nếu đang ở chế độ server thì set `IsServerSynced = false` và bắn `OnResyncRequired`.
- Kiểm tra drift mỗi Tick (xem 5.1).
- `ApplyFloor(long lastSeenUtcMs)`: gọi lúc load.

#### 6.1.2 `TimerScheduler : IUpdateHandler`
- Khởi tạo: `TimerScheduler(ITimeProvider clock, int initialCapacity = 256, int maxEventsPerTick = 10000)`.
- Tạo timer:
  - `bool TryStart(in TimerSpec spec, out TimerHandle handle)`: trả false nếu key đã tồn tại.
  - `bool TryGetHandle(string key, out TimerHandle h)`.
- Điều khiển:
  - `Pause(h)`: `PausedAtMs = now`, gỡ khỏi heap.
  - `Resume(h)`: `shift = now − PausedAtMs`, cộng vào `StartMs` và `NextDeadlineMs`, đưa lại vào heap.
  - `SpeedUp(h, ms)`: trừ khỏi `StartMs` và `NextDeadlineMs`, rồi `heap.Update`. `Delay(h, ms)` làm ngược lại.
    - `ms` được trừ vào **tổng** thời gian. Mọi mốc tier phía sau dời đều nhau, nên thời lượng các tier còn lại giữ nguyên. Thời gian bù có thể vượt qua nhiều tier.
    - Muốn tăng tốc theo % thì game tự tính `ms` từ `GetRemainingMs` hoặc `GetStageRemainingMs`.
  - `SkipCurrentStage(h)` (bón phân, dùng gem): dời `StartMs` để tier hiện tại kết thúc tại `now`. Xử lý **đồng bộ** như `CompleteNow`: bắn `StageChanged` ngay, hoặc `Completed` nếu đó là tier cuối.
  - `CompleteNow(h)`: xử lý đồng bộ, bắn các `StageChanged` còn lại rồi `Completed`.
  - `Cancel(h)`: xoá timer, không bắn Completed.
  - `Release(h)`: giải phóng timer đã Completed. Giải phóng key, tăng `Version`, trả slot vào free list.
- Truy vấn: `GetState`, `GetRemainingMs`, `GetElapsedMs`, `GetProgress01`, `GetCurrentStage`, `GetEndUtcMs`, `NowMs` (cache theo frame).
  - Khi đang pause, tính theo `PausedAtMs`.
  - Handle cũ trả giá trị mặc định, không ném exception.
  - **Theo tier:** `GetStageCount`, `GetStageRemainingMs`, `GetStageElapsedMs`, `GetStageProgress01`, `GetStageEndUtcMs(h, stage)`.
    - `GetCurrentStage` và các hàm này **tính từ `now`** (duyệt `StageEnds`, O(số tier), không allocate). Chúng không đọc `DispatchedStage`, nên vẫn đúng trong khoảng từ Restore đến Tick đầu, hoặc khi `ProcessingEnabled = false`. Sprite cây luôn hiển thị đúng tier.
- Listener: `SetListener(h, ITimerListener)`, `AddChannelListener(int channel, ITimerListener)`, `RemoveChannelListener(...)`. Khi gắn listener, các event đang chờ trong `_undelivered` được giao ngay.
- Persistence:
  - `CaptureSnapshot(List<TimerEntrySnapshot> into)`.
  - `Restore(IReadOnlyList<TimerEntrySnapshot>)`: bỏ qua bản ghi hỏng, báo qua `OnWarning`. Timer Completed được replay.
  - `event Action OnStructureChanged`: bắn khi create, cancel, pause, resume, speedup, complete, release. Không bắn mỗi frame.
- **Tick** (không allocate):
  - Lấy `now` 1 lần.
  - Lặp chừng nào `heap.Top.NextDeadlineMs <= now` và chưa vượt `maxEventsPerTick`:
    - Pop timer ra.
    - Nếu còn stage: tăng `DispatchedStage`, deadline mới = `StartMs + StageEnds[stage]`, push lại, dispatch `StageChanged(AtMs = deadline cũ)`.
    - Nếu hết stage: `State = Completed`, dispatch `Completed`.
  - **Dispatch ngay sau mỗi lần pop** (listener riêng trước, rồi tới listener của channel, hoặc vào buffer nếu chưa có listener). Nhờ vậy thứ tự offline đúng trên toàn cục. Callback được phép tạo, huỷ, release timer an toàn.
  - Cuối Tick, nếu có thay đổi cấu trúc thì bắn `OnStructureChanged`.

#### 6.1.3 `ProductionQueue : ITimerListener`
- Khởi tạo: `ProductionQueue(TimerScheduler, string queueKey, int capacity, int channel)`.
- `TryEnqueue(itemId, durationMs)`: queue rảnh thì chạy luôn.
- `RemoveAt(i)`: huỷ item đang chạy thì item kế tiếp bắt đầu tại `now`.
- `Pause/Resume/SpeedUpActive/CompleteActiveNow`.
- Truy vấn: `Items`, `Outputs`, `ActiveHandle`, `GetTotalRemainingMs`.
- `ClaimOutputs(List<...> into)`.
- `event OnItemCompleted(queue, itemId, atMs)`.
- Khi nhận `Completed(AtMs)`: release timer, đưa item sang `Outputs`, start item kế tiếp với **`StartUtcMs = e.AtMs`**. Deadline mới nằm trong quá khứ nên được pop ngay trong cùng Tick; toàn bộ chuỗi offline chạy xong trong 1 frame, đúng mốc.
- Key timer là `"{queueKey}/{sequence}"`. Khi restore thì `TryGetHandle(ActiveTimerKey)` rồi `SetListener`.

### 6.2 Tích hợp: Assembly-CSharp
Thư mục mới `TimeServices/CompleteTimerIntegration/`, **không có asmdef**, nằm cạnh folder này.

| File | Nội dung |
|---|---|
| `TimerSaveDataV1.cs` | `[Serializable][MessagePackObject] sealed class : IGameData`, theo mẫu `Assets/Temps/Scripts/TestRiseProgressData/RiseProgressDataV1.cs`. Gồm `[Key(0)] List<TimerEntryV1> Timers`, `[Key(1)] List<ProductionQueueEntryV1> Queues`, `[Key(2)] long LastSeenUtcMs`, `[Key(3)] List<RegenerationEntryV1> Regenerations`, cùng các class entry `[MessagePackObject]` |
| `CompleteTimerDataController.cs` | `[DynamicGameDataController(Id)] sealed class : DynamicGameDataController<TimerSaveDataV1>`, `Id = "complete_timer"`, `SchemaVersion = 1`. Inject clock, scheduler và 2 registry. `OnAfterLoad`: `ApplyFloor` → restore scheduler → restore queues/regens, rồi subscribe `OnStructureChanged → MarkDirty()`. `OnBeforeSave`: map snapshot vào `Data` (**copy mảng**), cập nhật `LastSeenUtcMs`, tái sử dụng buffer. Có `SaveNow()` |
| `CompleteTimerRuntime.cs` | `IStartable, IDisposable`. `Start`: `UpdateServiceManager.RegisterUpdateHandler(scheduler)`, `Application.focusChanged → clock.ReanchorFromDevice()`, nối `OnWarning` sang `Debug.LogWarning`. `Dispose`: gỡ hết |
| `CompleteTimerRegistration.cs` | `AddCompleteTimer(this IContainerBuilder, DataFlowScope)`: đăng ký `TimerClock` (`.As<ITimeProvider>().AsSelf()`), `TimerScheduler`, `ProductionQueueRegistry`, `RegenerationCounterRegistry`, `RegisterDataController<CompleteTimerDataController, TimerSaveDataV1>(dataFlow, Id, 1)`, `RegisterEntryPoint<CompleteTimerRuntime>`. Theo mẫu `Foundation/DataFlow/Runtime/DataFlowRegistration.cs` |

### 6.3 Sửa nhỏ ở Foundation
`Assets/DracoRuan/Foundation/DataFlow/Runtime/DynamicGameDataController.cs`: thêm `protected virtual void OnBeforeSave() { }` và gọi nó trong `Save()` sau đoạn kiểm tra gate, trước khi serialize. Nhờ vậy snapshot chỉ được tạo lúc flush.

### 6.4 Wiring mẫu
`Assets/Scripts/Test/SampleProjectLifetimeScope.cs`: gọi `builder.AddCompleteTimer(dataFlow);` sau `AddDataFlow()`. `AppInitializationPipelineEntryPoint` sẽ tự `InitializeAsync` controller.

### 6.5 Tests EditMode
Thư mục `CompleteTimer/Tests/Editor/`, asmdef `...TimeServices.CompleteTimer.Tests`, cấu hình giống `MobileVibration.Tests`: Editor-only, `UNITY_INCLUDE_TESTS`, nunit precompiled, `autoReferenced: false`. Dùng `FakeClock` (set tay cả device time lẫn monotonic).
- **TimerSchedulerTests**:
  - Timer 60s còn đúng 30s tại t=30s.
  - Completed đúng mốc và chỉ bắn 1 lần.
  - Multi-stage bắn đúng thứ tự và `AtMs`.
  - Pause/Resume.
  - SpeedUp vượt nhiều stage.
  - CompleteNow.
  - Cancel/Release trong callback không lỗi.
  - Handle cũ bị từ chối.
  - Key trùng thì `TryStart` trả false.
  - Offline catch-up theo thứ tự `AtMs`.
  - `maxEventsPerTick` giới hạn đúng.
- **MultiStageTierTests** (cây 3 tier A = 1', B = 3', C = 8', xem Q5):
  - t = 30s: tier 0, `GetStageProgress01 = 0.5`, tổng còn 11'30".
  - Tại 1' và 4' bắn `StageChanged(1)` và `StageChanged(2)`, `AtMs` chính xác. Tại 12' bắn `Completed(Stage = 3)`.
  - Restore sau khi offline 5': trước Tick, `GetCurrentStage = 2` và tier còn 7'. Sau Tick có 2 event đúng thứ tự.
  - Offline 20' → có thêm `Completed(AtMs = 12', IsLate)`.
  - `SkipCurrentStage` tại 2' (đang ở B) → C bắt đầu lúc 2', trưởng thành lúc 10'.
  - `SpeedUp(2')` tại 30s → đang ở B, B còn 1'30".
  - Pause tại 2', offline 10', Resume → B còn đúng 2'.
  - Đổi mảng config sau `TryStart` → timer không bị ảnh hưởng.
  - Thời lượng ≤ 0 → `TryStart` trả false.
- **TimerSnapshotTests**: Capture → Restore trên scheduler mới giữ nguyên remaining, stage, state. Bản ghi hỏng bị bỏ qua.
- **ProductionQueueTests**:
  - 3 item, offline 2.5 lần thời lượng → 2 output với AtMs đúng mốc, item 3 chạy được một nửa.
  - Pause queue.
  - Huỷ item đang chạy.
  - Restore và gắn lại listener.
- **TimerClockTests**:
  - Giờ máy lùi → now không lùi.
  - `ApplyFloor`.
  - `SyncWithServer` và `OnResyncRequired`.
  - Sleep: monotonic đứng yên, giờ máy tăng 10 phút → remaining giảm đúng 10 phút.
- **RealTimeElapsedTests**:
  - Timer 1 giờ, tắt app 25 phút → còn 35 phút ±0 ms.
  - Save ở phút 1 hay phút 20 cho kết quả như nhau.
  - Offline 2 giờ → Completed với `AtMs = start + 1h`, `IsLate`.
  - Pause phút 10, offline 1 giờ → còn 50 phút.
  - 3 stage, offline qua 2 mốc → nhận 2 StageChanged đúng mốc.
  - `deltaTime` bất kỳ không ảnh hưởng.
- **DeliveryGuaranteeTests**:
  - Tick khi chưa có listener, sau đó mới `AddChannelListener` → nhận đúng 1 Completed.
  - Stage bị vượt khi offline, listener đăng ký muộn → nhận đủ event, đúng thứ tự.
  - Completed chưa Release → Restore → `IsReplay`.
  - Đã Release thì không replay.
- **ReleaseLifecycleTests**:
  - `Release` → `CaptureSnapshot` không còn entry đó, key tạo lại được, handle cũ vô hiệu, slot được tái sử dụng.
  - `AutoRelease` khi có listener → tự release sau khi giao event.
  - `AutoRelease` khi chưa có listener → vẫn Completed trong snapshot, sau khi Restore thì replay rồi mới release.
  - Queue/Regen không để lại timer Completed trong snapshot.
  - Vượt `CompletedWarningThreshold` thì bắn `OnWarning`.
- **RegenerationCounterTests** (5 lượt, 30 phút):
  - Tiêu khi đầy → sau 30 phút lại đầy.
  - 1/5, offline 70 phút → 3/5, lượt kế tiếp còn 20 phút.
  - Offline 2 giờ → 1 callback với `gained = 4`.
  - Tiêu khi đang hồi dở không reset lượt.
  - Trễ 500 ms mỗi lượt qua 5 lượt → mốc cuối vẫn đúng tuyệt đối.
  - Refill, Add vượt Max, Capture/Restore.
- **TimerSchedulerPerfTests**: 10.000 timer, 1.000 lần Tick không có timer đến hạn → `GC.GetAllocatedBytesForCurrentThread()` delta = 0, ghi lại thời gian.

---

## 7. Lưu ý khi triển khai
- **Rider hook** (memory `rider-hook-stale-buffer`): sau mỗi lần Write file .cs, grep lại nội dung. Nếu thấy nội dung cũ quay lại thì ghi qua scratchpad rồi `cp`.
- Giữ nguyên `SimpleTimer/` và `Extensions/TimeExtensions.cs` (ngoài phạm vi, vẫn ở Assembly-CSharp).
- Phong cách repo: dùng `this.`, `_camelCase` cho field private, XML doc giải thích *tại sao*, không dùng LINQ trên hot path.
- Class `Debug` global (`Utilities/DebugUtils`) chỉ có ở Assembly-CSharp. Core dùng callback `Action<string> OnWarning`.
- `System.Collections.Generic.PriorityQueue` **không có** trong .NET Standard 2.1 (Unity 6000.3), nên phải tự viết heap.
- DataFlow dùng **MessagePack** (không phải MemoryPack), với `IGameData` và `[MessagePackObject]`/`[Key(n)]`. Schema mới thì tạo class `V2` + `DataMigrator`.
- `SaveScheduler` mặc định `autoSaveIntervalSeconds = 0`, tức chỉ flush khi suspend/quit, nên thao tác tốn gem cần gọi `SaveNow()`.
- `UpdateServiceManager`: handler mới đăng ký chỉ bắt đầu tick từ **frame sau**. Scheduler chạy ở `PlayerLoop.Update` index 0, trước `MonoBehaviour.Update`.

## 8. Verification
1. **Compile-check**: mở Unity và kiểm tra Console sạch, hoặc dùng recipe csc trong memory cho từng bộ define (Editor, `UNITY_ANDROID`, `UNITY_IOS`). Kiểm tra MessagePack resolver sinh được cho `TimerSaveDataV1`.
2. **EditMode tests**: chạy bằng Test Runner, hoặc `Unity.exe -batchmode -runTests -testPlatform EditMode -projectPath . -testResults <path>/results.xml` khi editor đang đóng. Tất cả phải pass.
3. **Play mode** với `SampleProjectLifetimeScope`:
   - Tạo timer, queue và lives rồi thoát.
   - Kiểm tra `persistentDataPath/GameData/complete_timer/*.sav` đã được tạo.
   - Mở lại sau khoảng thời gian dài hơn thời lượng timer: event được bắn với `IsLate`/`IsReplay` và state đúng.
4. **Profiler** với 10.000 timer: `TimerScheduler.Tick` ≈ 0 ms, GC Alloc = 0 B/frame.
