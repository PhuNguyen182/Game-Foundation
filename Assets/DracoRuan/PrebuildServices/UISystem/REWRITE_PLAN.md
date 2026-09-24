# UISystem: review và plan viết lại

> Tổng hợp phiên review ngày 2026-09-24. **Chưa sửa dòng code nào.** Phiên sau bắt đầu từ [mục 5: Thứ tự triển khai](#5-thứ-tự-triển-khai-mỗi-phase-kết-thúc-bằng-compile-check-và-chạy-test-nếu-phase-đó-có-test), phase 1.

**Mục tiêu:** base UI tái sử dụng cho các game từ mid-core trở lên, chạy trên **Mobile + PC/Console**, hỗ trợ cả **uGUI và UI Toolkit**.

---

## Tóm tắt phiên chat

**Yêu cầu:** review bộ `PrebuildServices/UISystem`, đánh giá xem đã đủ làm foundation UI tái sử dụng cho game mid-core trở lên chưa, và viết plan cải thiện nếu cần.

**Đã làm:**
- Đọc toàn bộ 32 file của UISystem: Animations, Canvases, Popups, Views, UIElements, `UIManager`.
- Đọc các phụ thuộc: `Utilities/ObjectPooling` (`ObjectPool`, `GenericObjectPoolManager`, `GameObjectPool<T>`).
- Tham chiếu các phần sau làm chuẩn:
  - `AudioSystem`: asmdef, Installer, README, Addressables lease.
  - `Foundation/Initializers`: `[AutoInstall]`, `IAsyncInitializable`.
  - `Assets/Temps/Resources/game-foundation-checklist.md`, mục 7 "UI Framework".
- Grep toàn repo: **không có code nào ngoài UISystem dùng bộ này**, nên được phép phá API.
- Package có sẵn: UniTask 2.5.11, VContainer 1.18, R3 1.3.1, MessagePipe, Addressables 2.9.1, Input System 1.18, DOTween, Odin. Unity 6000.3.10f1.

**Kết luận:** **chưa dùng được cho production**, kể cả game casual.
- Có 12 lỗi nghiêm trọng (mục 1.1). Ba lỗi nặng nhất:
  - Popup đóng rồi vẫn chặn click.
  - `CloseAllPopups` crash.
  - Animation Parallel chỉ chạy được một lần.
- Animation SO giữ runtime state.
- Mọi animation và cooldown phụ thuộc `timeScale`, nên pause menu bị kẹt.
- Kiến trúc thiếu hầu hết những gì mid-core cần:
  - screen stack;
  - Back/Esc;
  - popup queue;
  - Addressables;
  - focus/gamepad;
  - safe area;
  - popup trả kết quả;
  - asmdef, test, installer.

**Quyết định user đã chốt (Q&A):**
| Câu hỏi | Chọn |
|---|---|
| Nền tảng | **Mobile + PC/Console**: có Back/Esc, safe area, focus/gamepad navigation ngay từ đầu |
| UI tech | **uGUI + UI Toolkit**: core không phụ thuộc backend, có 2 adapter |
| Phạm vi | **Viết lại theo phase**, theo khuôn AudioSystem (asmdef, Logic thuần C# có test, Installer `[AutoInstall]`, README). Giữ ý tưởng "animation recipe bằng ScriptableObject" (compose Sequential/Parallel) nhưng chuyển sang dạng stateless |

**Việc còn mở cho phiên sau:**
- User chưa duyệt plan. Hãy đọc lại mục 2 (kiến trúc) và mục 5 (các phase), chỉnh nếu cần rồi mới bắt đầu phase 1.
- Có thể cần chốt thêm:
  - Key của view dùng **Type**, như plan đề xuất, hay dùng id sinh code (kiểu `AudioId`).
  - Có giữ Odin ở runtime không. Plan đề xuất bỏ.

---

## 1. Review code hiện tại

### 1.1 Lỗi nghiêm trọng (bug runtime)
| # | Lỗi | Vị trí |
|---|---|---|
| A1 | Popup lấy từ pool (`ObjectPool.Spawn`) nhưng **không bao giờ được Despawn**. `Hide()` chỉ chạy animation. Object vẫn active, `CanvasGroup.blocksRaycasts` vẫn true, nên popup đã đóng (alpha 0) **vẫn chặn mọi click** bên dưới | `BaseUIView.cs:21-30`, `AnimationMachine` không đụng tới `blocksRaycasts` |
| A2 | Show cùng một popup 2 lần sẽ spawn instance thứ 2 và ghi đè entry trong dictionary. Instance cũ bị mồ côi, manager không đóng được nữa | `UIPopupManager.cs:54-55,73-74` |
| A3 | `CloseAllPopups` duyệt `foreach` trên dictionary, nhưng `ClosePopup` lại `Remove` phần tử trong lúc duyệt, gây `InvalidOperationException` | `UIPopupManager.cs:131-137` |
| A4 | `UIManager.Show<T>()` có tham số mặc định `popupName = null`, dẫn tới `Dictionary.GetValueOrDefault(null)` ném `ArgumentNullException`. API mặc định crash | `UIManager.cs:22,27`, `PopupCollection.cs:27` |
| A5 | Spawn đặt world position = 0 rồi `SetParent(parent)` với `worldPositionStays=true`. `Instantiate` cũng diễn ra ngoài canvas. RectTransform bị lệch hoặc sai scale dưới CanvasScaler | `UIPopupManager.cs:50,69`, `GenericObjectPoolManager.cs:69-75` |
| A6 | `ParallelAnimation` cache `_sequence`, còn `TryKillAnimation` Kill nhưng không set null. Từ lần Show thứ 2 trở đi sẽ `Play()` một tween đã chết, **animation không chạy** | `ParallelAnimation.cs:24-25,40` |
| A7 | Animation config là **ScriptableObject nhưng giữ runtime state** (`Target`, `_sequence`, `_rectTransform`). Hai view dùng chung một asset sẽ ghi đè target của nhau. State còn sót lại giữa các lần Play khi tắt domain reload | `AnimationConfig.cs:8`, `Sequential/ParallelAnimation`, `MoveAnchorAnimation.cs:30` |
| A8 | Background animation DOTween chạy trên `animatableSubject` thay vì `animatableBackground` (lỗi copy-paste) | `AnimationMachine.cs:99,173` |
| A9 | Mọi animation và cooldown đều phụ thuộc `Time.timeScale` (DOTween mặc định, `UniTask.WaitForSeconds`, `UniTask.Delay`). Mở pause menu khi `timeScale = 0` thì animation không kết thúc, popup không bao giờ interactable, button kẹt ở trạng thái disabled | `AnimationMachine.cs:76…`, `BaseUIButton.cs:58` |
| A10 | Unity message bị che (hide) giữa lớp cha và lớp con. `BaseUIPopup` dùng `new virtual OnDestroy`. `BaseUIView<T>`, `BaseUIButton`, `BaseUISlider` khai báo lại `virtual OnDestroy` (warning CS0114). Kết quả là `BaseUIView.OnDestroy` (kill tween) **không bao giờ chạy**. `OnValidate` private ở lớp con cũng che `BaseUIView.OnValidate`, nên `animationMachine` của button/slider không được gán | `BaseUIPopup.cs:44`, `BaseUIViewWithModel.cs:35`, `BaseUIButton.cs:72,78`, `BaseUISlider.cs:37,44` |
| A11 | Preload không có tác dụng: `PreloadObjectPool<BaseUIPopup>` tạo pool trong `ObjectPoolManager<BaseUIPopup>`, còn `Spawn<TPopup>` tìm trong `ObjectPoolManager<TPopup>` (static generic khác nhau). `UnityEngine.Pool` cũng không prewarm | `UIPopupManager.cs:38`, `GenericObjectPoolManager.cs` |
| A12 | Nhiều chỗ có thể NullReference khi không cấu hình đủ. `animatableBackground` bị gán không kiểm tra (`AnimationMachine.cs:133`). `tweenAnimationConfig` null khi type = DOTween (`ViewAnimationConfig.cs:40`). `MoveAnchorAnimation` không check Target | |

### 1.2 Lỗi trung bình
- `PlayHideBackground` set `interactable = true` khi đang hide (`AnimationMachine.cs:133`).
- `SingleAnimation.TryKillAnimation` chỉ kill tween có target là CanvasGroup. Tween Scale (target là transform) và Move (target là RectTransform) không bị kill, nên leak tween khi destroy.
- Các field `delay`/`loop` của `SingleAnimation` chỉ được `FadeAnimation` dùng. Scale và Move bỏ qua.
- `MoveAnchorAnimation` tween **anchor** thay vì `anchoredPosition`, làm hỏng layout của các con.
- Animator transition chờ theo `animationDuration` nhập tay, không chờ state kết thúc.
- `BaseUIButton`: gọi `SetInteractable(false)` từ bên ngoài trong lúc cooldown sẽ bị bỏ qua, rồi bị cooldown bật lại true. Button kế thừa `BaseUIView` nên **mỗi button bắt buộc có `AnimationMachine`** (RequireComponent kế thừa). Class lại `abstract` nên muốn dùng phải tạo subclass.
- `Show`/`Hide` không có state machine. Gọi `Show` trong lúc `Hide` chưa xong thì continuation của Hide vẫn chạy (có thể `Destroy`). `forceDestroyOnClose` cũng xung đột với pooling.
- `IObjectResolver.Inject(result)` chạy lại **mỗi lần Show** và chỉ inject component gốc, không inject children (nên dùng `InjectGameObject` một lần lúc tạo). `[Inject] Initialize` trùng chức năng với `SetPopupManager`.
- `IUIModel` để `OnModelDataBound/Unbound` là public. `GeneralPopupWithModel` copy nguyên `BaseUIView<T>` và không unbind khi destroy. `OnModelUpdated` chỉ bắn lúc bind nên không phải model reactive thật.
- `PopupCollection.Initialize` dùng `Add`, trùng tên là throw và không có validator. Dictionary runtime lại nằm trong SO.
- `UIPopupManager` làm việc nặng trong constructor, có finalizer không cần thiết, và `Dispose` không dọn instance nào.
- `ClosePopupByType` so sánh `GetType()` tuyệt đối nên bỏ qua subclass.
- `BaseUIPopup.PoolHashKey` có nhánh `#else int`. Nhánh này không thoả `IPoolableObject` (chỉ khai báo `EntityId`) nên là code chết.
- (Ngoài scope) `GenericObjectPoolManager.cs:89` gọi `Object.Destroy(instance)`, tức là destroy **component** chứ không phải GameObject.

### 1.3 Kiến trúc / tái sử dụng
- **Không có asmdef**, nên code nằm trong Assembly-CSharp và không đóng gói được. Code còn phụ thuộc `DracoRuan.Utilities.ObjectPooling`, cũng không có asmdef.
- Không có interface cho `UIManager` nên không mock được. Không có Installer, README hay test.
- Key popup bằng **string** (checklist yêu cầu strongly-typed).
- `CanvasCategory` là enum hardcode, game không mở rộng layer được. `CanvasConfig` chỉ được áp dụng trong `OnValidate` ở Editor qua `Resources.Load` (path hardcode), không có tác dụng lúc runtime. Không cấu hình CanvasScaler hay worldCamera.
- Prefab được tham chiếu trực tiếp trong SO, nên load collection là **load toàn bộ popup vào RAM**. Không dùng Addressables (checklist bắt buộc).
- Runtime code phụ thuộc Odin (`ShowIf`), là plugin trả phí.

### 1.4 Thiếu cho mid-core
Chưa có:
- Screen/page stack (push/pop/replace/popToRoot).
- Nút Back/Esc đi theo stack.
- Popup queue theo priority (chuỗi reward, level-up…).
- Modal backdrop dùng chung, click-outside-to-close.
- Sort order động theo stack.
- Khoá input trong lúc transition.
- Popup trả kết quả (`await` confirm dialog).
- Toast, loading overlay.
- Safe area.
- Focus/gamepad navigation.
- Event mở/đóng cho analytics và tutorial.
- Pattern MVVM thật: View không chứa logic.
- Virtualized list.

**Những điểm nên giữ:** tách animation thành recipe SO có thể compose (Sequential/Parallel), dùng UniTask + DOTween + VContainer, và ý tưởng Canvas theo layer.

---

## 2. Kiến trúc mới

### 2.1 Assemblies (theo khuôn AudioSystem)
| Assembly | Folder | Nội dung |
|---|---|---|
| `…UISystem.Logic` | `Logic/` | C# thuần, `noEngineReferences: true`: `UIStack` (screen/popup stack), `UIPopupQueue` (priority + FIFO trong cùng priority), `SortOrderAllocator`, `UIViewStateMachine` (Hidden→Showing→Shown→Hiding, chống re-entrance), `InputLockCounter`, `BackRouter` (chọn view nhận Back) |
| `…UISystem` | `Data/ Interfaces/ Core/ MVVM/ Installer/` | Core **không phụ thuộc backend**: `IUIService`, registry, loader, router, cache, MVVM base, installer |
| `…UISystem.UGUI` | `UGUI/` | Backend uGUI: layer root (Canvas + CanvasScaler theo config), view host, DOTween transitions, `UIButton`, `UISlider`, `SafeAreaFitter`, backdrop, focus qua EventSystem |
| `…UISystem.Toolkit` | `Toolkit/` | Backend UI Toolkit: layer = `UIDocument` + `PanelSettings.sortingOrder`, view = `VisualTreeAsset` + controller C#, transition bằng USS class + `TransitionEndEvent` (có timeout), safe area bằng padding, focus bằng `Focusable.Focus()` |
| `…UISystem.Editor` | `Editor/` | UI Registry window, type picker drawer, validator (trùng key, thiếu asset, sai backend) + `IPreprocessBuildWithReport` |
| `…UISystem.Tests` | `Tests/Editor/` | EditMode test cho `Logic` |
| `…UISystem.PlayModeTests` | `Tests/Runtime/` | Smoke test uGUI: open/close/back/queue/timeScale 0 |

Dependencies: UniTask (+ `UniTask.DOTween` define, `UniTask.Addressables`), VContainer, **R3** (đã có sẵn trong manifest), DOTween (chỉ UGUI asmdef), Input System (`versionDefines` cho back source), Addressables (sau define `USE_EXTENDED_ADDRESSABLE` giống Audio), `DracoRuan.Foundation.Initializers`. **Bỏ** phụ thuộc `Utilities.ObjectPooling` và Odin ở runtime. Muốn ẩn/hiện field có điều kiện thì dùng PropertyDrawer trong Editor asmdef.

### 2.2 Data (ScriptableObject)
- `UILayerDefinition`: name, `baseSortOrder`, `sortStep`, kind (Screen / Popup / Overlay / Toast / System / Tutorial), `applySafeArea`, `blocksInputBelow`. Có asset mặc định, game tự thêm layer được. Thay cho enum `CanvasCategory`.
- `UIRootConfig`: danh sách layer, reference resolution, match, render mode, camera mode (uGUI); `PanelSettings` template (UITK).
- `UIViewDefinition` (entry trong `UIRegistry`):
  - Key: **view Type** (strongly-typed, không cần codegen; editor chọn qua type picker và validate).
  - `backend` (UGUI/Toolkit), layer, kind.
  - Asset: prefab hoặc VisualTreeAsset. Direct reference hoặc `AssetReference`.
  - `cachePolicy`: Destroy / KeepAlive / Preload.
  - `modal`, `closeOnBackdrop`, `backBehaviour` (Close / Consume / PassThrough), `defaultPriority`.
  - Transition in/out.
- `UIRegistry`: dựng dictionary `Type → definition` **trong service**, không nằm trong SO. Validate lúc init, trùng key thì báo lỗi rõ ràng.

### 2.3 API công khai (`IUIService`)
```csharp
UniTask<TView> OpenAsync<TView>(CancellationToken ct = default) where TView : class, IUIView;
UniTask<TView> OpenAsync<TView, TModel>(TModel model, CancellationToken ct = default) where TView : class, IUIView<TModel>;
UniTask<TResult> OpenForResultAsync<TView, TModel, TResult>(TModel model, CancellationToken ct = default) where TView : class, IUIView<TModel>, IResultView<TResult>;
void Enqueue<TView, TModel>(TModel model, int priority = 0);   // popup queue
UniTask CloseAsync<TView>(); UniTask CloseAsync(IUIView view); UniTask CloseAllAsync(UILayerId? layer = null);
UniTask PopScreenAsync(); UniTask PopToRootAsync();
bool IsOpen<TView>(); IUIView Top { get; }
IDisposable LockInput();                                          // counter, dùng using
UniTask PreloadAsync(params Type[] views); void Release(Type view);
Observable<UIViewEvent> Events { get; }                           // R3: Opened/Closed/Focused/BackAtRoot
```
- `IUIView` (lifecycle do framework gọi, **protected virtual** ở base class): `OnCreated`, `OnOpening`, `OnOpened`, `OnClosing`, `OnClosed`, `OnFocused` / `OnBlurred` (khi bị view khác che hoặc lộ lại), `OnBackRequested → bool`.
- `IResultView<TResult>`: view gọi `Complete(result)`. Service đóng view rồi trả `UniTask<TResult>`. Đóng bằng Back hoặc backdrop thì trả `default` (hoặc giá trị cấu hình).
- **MVVM**: `UIViewModel` base (plain C#, `CompositeDisposable`, R3 `ReactiveProperty`). View bind vào ViewModel. Subscription được dispose khi Close, và khi Release nếu KeepAlive. Có binding helper cho mỗi backend (`BindText`, `BindInteractable`, `BindClick`…).
- DI: instance được inject **một lần lúc tạo** (`InjectGameObject` với uGUI, `resolver.Inject(controller)` với UITK).

### 2.4 Hành vi runtime
- **Router**: screen dùng stack. Push thì blur hoặc ẩn screen dưới (theo `hidesBelow`). Popup có stack riêng theo layer, sort order cấp bởi `SortOrderAllocator`. Mỗi uGUI view có Canvas `overrideSorting` + `GraphicRaycaster` riêng để cô lập canvas rebuild. Backdrop dùng chung, luôn nằm ngay dưới modal trên cùng. Đổi screen thì đóng các popup gắn với screen đó (popup có `scope: Screen | Global`).
- **Queue**: `Enqueue` chờ tới khi không còn modal nào và input không bị lock thì mở item priority cao nhất. Có `Pause/Resume` cho tutorial và cutscene.
- **Back**: `IUIBackInputSource`. Mặc định là action `UI/Cancel` của Input System (Esc, gamepad B, Android back). `BackRouter` duyệt từ layer cao xuống: System > Popup > Screen. Khi đang transition hoặc lock thì Back bị nuốt. Ở root screen thì bắn `BackAtRoot` để game tự hiện hộp thoại quit.
- **Focus**: mỗi view khai báo default selectable (uGUI) hoặc focusable element (UITK). Khi blur thì nhớ phần tử đang chọn, khi lộ lại thì khôi phục. `IUIFocusController` do từng backend implement.
- **Transition**: mọi tween và delay dùng **unscaled time** (`SetUpdate(true)`, `DelayType.UnscaledDeltaTime`). Trong transition, input bị lock và `blocksRaycasts`/`picking` tắt. Khi Hidden thì object bị deactivate (KeepAlive) hoặc destroy + release asset. Cancel qua `CancellationToken` của view.
- **Animation recipe (uGUI)**: `UITransition` SO **không có state**, chỉ có `Tween Create(in UITransitionTarget t)` với `t` = (RectTransform, CanvasGroup). Base class xử lý delay/loop/ease một chỗ. Mọi tween được `SetLink(go)` + `SetTarget` rồi trả về để host giữ và kill. Sequential/Parallel compose bằng cách tạo tween mới mỗi lần. Move dùng offset `anchoredPosition` (tính theo tỉ lệ kích thước parent). Animator transition chờ state kết thúc với `Animator.updateMode = UnscaledTime`.
- **Loading**: `IUIAssetProvider` có 2 cách load là Direct và Addressables. Addressables dùng lease + refcount, theo mẫu `AudioSystem/Core/Loading/AudioClipLibrary.cs` + `AudioClipLease.cs`. Preload chạy trong `IAsyncInitializable` của service.
- **Components (uGUI)**: `UIButton` là class concrete và **không kế thừa view**. Cooldown dùng unscaled time. Trạng thái `interactable` tách khỏi cooldown lock (AND hai cờ). Có hook `IUIClickFeedback` để game cắm AudioSystem/MobileVibration mà foundation không phụ thuộc chúng. Ngoài ra có `UISlider`, `SafeAreaFitter` (bắt thay đổi orientation/resolution), `UIBackdrop`.
- **Installer**: `UIInstaller` (SO, `[AutoInstall]`) + extension `builder.AddUIService(rootConfig, registry)`, theo mẫu `AudioSystem/Installer/AudioInstaller.cs` + `AudioServiceRegistration.cs`. Backend đăng ký qua `builder.AddUGUIBackend()` / `AddToolkitBackend()`.

---

## 3. Layout folder đích
```
UISystem/
  REWRITE_PLAN.md  README.md
  Logic/     Navigation/ Queue/ Layers/ Lifecycle/ Input/        (.Logic asmdef)
  Data/ Interfaces/ Core/ Core/Loading/ MVVM/ Installer/         (core asmdef ở root UISystem)
  UGUI/      Hosting/ Transitions/ Components/ Focus/ Bindings/  (.UGUI asmdef)
  Toolkit/   Hosting/ Transitions/ Focus/ Bindings/              (.Toolkit asmdef)
  Editor/    Windows/ Drawers/ Validation/                       (.Editor asmdef)
  Tests/Editor/  Tests/Runtime/
  Samples/   ConfirmPopup (result), Toast, LoadingOverlay, 2 screen — mỗi backend 1 bộ
```
Code cũ (`Animations/ Canvases/ Popups/ Views/ UIElements/ UIManager.cs`) sẽ bị `git rm` ở phase cuối, sau khi sample mới chạy được.

---

## 4. Tái sử dụng từ repo
- Mẫu asmdef, installer, README, tests: `PrebuildServices/AudioSystem/**`.
- Mẫu Addressables lease/refcount + `USE_EXTENDED_ADDRESSABLE`: `AudioSystem/Core/Loading/AudioClipLibrary.cs`, `AudioClipLease.cs`.
- `[AutoInstall]`, `IAsyncInitializable`: `Foundation/Initializers/`.
- Ý tưởng recipe (Fade/Scale/Move/Sequential/Parallel): chuyển sang dạng stateless.
- **Không** dùng `Utilities/ObjectPooling` (có các lỗi ở A5/A11). Một view chỉ cần một instance cache, đơn giản hơn pool.

---

## 5. Thứ tự triển khai (mỗi phase kết thúc bằng compile-check, và chạy test nếu phase đó có test)
0. ~~Lưu review + plan thành `UISystem/REWRITE_PLAN.md`~~ (**đã xong**: chính là file này).
1. **Logic**: asmdef `.Logic` + `UIStack`, `UIPopupQueue`, `SortOrderAllocator`, `UIViewStateMachine`, `InputLockCounter`, `BackRouter`. Viết EditMode test cho từng class: thứ tự priority, re-entrance, back khi đang lock, cấp lại sort order sau khi pop.
2. **Core runtime**: Data SO, Interfaces, `UIService` + router + cache + `IUIAssetProvider` (Direct trước), MVVM base (R3), Installer. Backend lúc này mới là interface.
3. **Backend uGUI**: layer root theo `UIRootConfig`, view host, transition stateless (viết lại Fade/Scale/Move/Sequential/Parallel/Animator), backdrop, `UIButton`/`UISlider`/`SafeAreaFitter`, focus EventSystem, binding helpers. PlayMode smoke test gồm open/close, CloseAll, Back, queue, `timeScale = 0`, và popup đã đóng không còn chặn raycast.
4. **Input**: `InputSystemBackInputSource` (Esc, gamepad, Android back) + focus restore.
5. **Addressables provider** (lease/refcount, preload, release) sau `USE_EXTENDED_ADDRESSABLE`.
6. **Backend UI Toolkit**: layer = UIDocument/PanelSettings, controller view, USS transition, safe area, focus, bindings. PlayMode smoke test tương tự phase 3.
7. **Editor**: Registry window, type picker, validator + build validator.
8. **Samples + README**: Confirm (result), Toast, Loading, 2 screen, cho cả 2 backend. Sau đó `git rm` code cũ.
9. (Tuỳ chọn) Virtualized list cho uGUI (UITK dùng `ListView` có sẵn), hook cho tutorial highlight.

---

## 6. Verification
- **Compile-check ngoài Unity** sau mỗi phase, theo công thức trong memory `rider-hook-stale-buffer`: Roslyn của Unity (`Editor/Data/NetCoreRuntime/dotnet.exe` + `DotNetSdkRoslyn/csc.dll`) với `Library/ScriptAssemblies/*.dll` + `UnityEngine/*.dll` + netstandard 2.1. Chạy với define `UNITY_EDITOR`, `UNITY_ANDROID`, `UNITY_IOS`, có và không có `USE_EXTENDED_ADDRESSABLE`. Lưu ý: file vừa `git mv` thì viết qua scratchpad rồi `cp`, vì Rider hook có thể ghi đè bằng buffer cũ.
- **EditMode tests** (`Tests/Editor`) cho toàn bộ Logic, chạy bằng Unity Test Runner hoặc `Unity -runTests -testPlatform EditMode`.
- **PlayMode smoke tests** (`Tests/Runtime`) cho từng backend. Phải đạt các kịch bản đúng như bug cũ:
  - (a) mở rồi đóng popup, click xuyên được xuống dưới;
  - (b) mở cùng popup 2 lần, chỉ có 1 instance;
  - (c) `CloseAllAsync` không throw;
  - (d) `timeScale = 0` vẫn mở/đóng được và button hết cooldown;
  - (e) animation Parallel chạy đúng ở lần mở thứ 2;
  - (f) hai view dùng chung một transition asset không ảnh hưởng nhau;
  - (g) Back đóng đúng view trên cùng, ở root thì bắn `BackAtRoot`;
  - (h) `OpenForResultAsync` trả đúng kết quả, và đóng bằng Back thì trả `default`.
- **Thủ công trong Editor**: chạy sample scene trên Device Simulator (safe area, notch) và thử gamepad navigation (PC).
