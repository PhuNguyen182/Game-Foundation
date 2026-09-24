# UISystem: review và plan viết lại

> Tổng hợp phiên review ngày 2026-09-24. **Chưa sửa dòng code nào.** Phiên sau bắt đầu từ mục 5 (Thứ tự triển khai), phase 1.

**Mục tiêu:** base UI tái sử dụng cho các game từ mid-core trở lên. Chạy trên **Mobile + PC/Console**, dùng **uGUI**, theo mô hình **MVVM ViewModel-first**.

---

## Tóm tắt phiên chat

**Yêu cầu:** review bộ `PrebuildServices/UISystem`, đánh giá xem đã đủ làm foundation UI tái sử dụng cho game mid-core trở lên chưa, và viết plan cải thiện nếu cần.

**Đã làm:**
- Đọc toàn bộ 32 file của UISystem: Animations, Canvases, Popups, Views, UIElements, `UIManager`.
- Đọc các phụ thuộc: `Utilities/ObjectPooling` (`ObjectPool`, `GenericObjectPoolManager`, `GameObjectPool<T>`).
- Tham chiếu các phần sau làm chuẩn:
  - `AudioSystem`: asmdef, Installer, README, Addressables lease.
  - `Foundation/Initializers`: `[AutoInstall]`, `IAsyncInitializable`.
  - `PrebuildServices/Localization`: `ILocalizationService`, `LocalizableTextMeshPro`.
  - `Assets/Temps/Resources/game-foundation-checklist.md`, mục 7 "UI Framework".
- Grep toàn repo: **không có code nào ngoài UISystem dùng bộ này**, nên được phép phá API.
- Package có sẵn:
  - UniTask 2.5.11, VContainer 1.18, MessagePipe;
  - R3 1.3 (core qua NuGet `Assets/Packages/R3.1.3.0`, `com.cysharp.r3` cho phần Unity);
  - Addressables 2.9.1, Input System 1.18, uGUI 2.0, URP 17.3;
  - DOTween, Odin, NuGetForUnity.
  - Unity 6000.3.10f1.

**Kết luận review code cũ:** **chưa dùng được cho production**, kể cả game casual.
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
  - MVVM thật;
  - asmdef, test, installer.

**Quyết định user đã chốt (Q&A):**
| Câu hỏi | Chọn |
|---|---|
| Nền tảng | **Mobile + PC/Console**: có Back/Esc, safe area, focus/gamepad navigation ngay từ đầu |
| UI tech | **Chỉ uGUI**. Ban đầu chọn uGUI + UI Toolkit, sau đó user đổi ý để tập trung tối đa vào uGUI. Không làm lớp backend abstraction |
| Phạm vi | **Viết lại theo phase**, theo khuôn AudioSystem (asmdef, Logic thuần C# có test, Installer `[AutoInstall]`, README). Giữ ý tưởng "animation recipe bằng ScriptableObject" (compose Sequential/Parallel) nhưng chuyển sang dạng stateless |
| Pattern | **MVVM, ViewModel-first**: code game chỉ biết ViewModel (`OpenAsync<ShopViewModel>(args)`). Registry map VM type → prefab view. VM là C# thuần, tạo qua VContainer, test EditMode được. Câu hỏi "key bằng Type hay id sinh code" được trả lời luôn: **key = VM type** |
| Collection binding | **Thêm ObservableCollections** (Cysharp, NuGet `ObservableCollections` + `ObservableCollections.R3`) để bind list/grid vào `UIRecycleList` |

**Lịch sử chỉnh plan:**
- **Bản 1** (commit `fb4b461`): uGUI + UI Toolkit, có lớp backend abstraction.
- **Bản 2** (không commit riêng, gộp vào bản này): bỏ UI Toolkit. Thêm mục "uGUI chuyên sâu": hide bằng `Canvas.enabled`, raycast/layout hygiene, URP camera stacking, particle sorting. Virtualized list thành phase chính thức.
- **Bản 3** (file này): tự review lại bản 2 theo hướng MVVM. Kết quả ở mục 2.0.

**Việc còn mở:**
- User chưa duyệt plan. Đọc mục 2.0 (thay đổi so với bản 2) và mục 5 (các phase) trước khi bắt đầu phase 1.
- Odin ở runtime: plan đề xuất bỏ.

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
- Tất cả popup nằm chung một Canvas `Popup`. Popup nào đổi một Graphic cũng làm cả canvas rebuild, và không có sort order riêng cho từng popup.
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

## 2. Kiến trúc mới (uGUI + MVVM ViewModel-first)

### 2.0 Review lại bản 2: bổ sung / loại bỏ
Bản 2 gọi là MVVM nhưng thực chất là **view-first/MVP**. API `OpenAsync<TView, TModel>(model)` bắt code game tham chiếu MonoBehaviour. ViewModel không được tạo qua DI, không có Command, không bind được collection, không có widget con, còn Back và kết quả lại do View xử lý.

**Bổ sung:**
| # | Tính năng | Lý do |
|---|---|---|
| B1 | **Navigation theo VM**: `OpenAsync<TViewModel>`, registry map VM → prefab, VM tạo qua VContainer (installer tự `Register` Transient mọi VM có trong registry) | Code game và VM không tham chiếu MonoBehaviour. VM inject được service (inventory, economy…) |
| B2 | Asmdef **`.MVVM` không phụ thuộc engine**: `UIViewModel`, command, `IUINavigator` (dùng `ValueTask`), cùng asmdef **`.Testing`** chứa `FakeUINavigator` | Test VM bằng EditMode mà không cần prefab hay scene. VM mở được popup khác qua `IUINavigator` |
| B3 | **Command** sync/async có `CanExecute` và `IsExecuting`. Async command bỏ qua lần bấm thứ 2 khi đang chạy | Chống double-tap đúng chỗ (theo logic), thay cho cooldown mù của button |
| B4 | **`UIBinder`**: binding bằng code, strongly-typed, không reflection. Gồm one-way, two-way (slider/toggle/input field), command, collection, widget | Refactor-safe và nhanh. Mọi binding được thu về một chỗ để dispose khi unbind |
| B5 | **Collection binding**: `ObservableList<T>` + `ISynchronizedView` → `UIRecycleList`, có Add/Remove/Move/Reset từng phần tử | Inventory, shop, leaderboard là nhu cầu bắt buộc của mid-core |
| B6 | **`UIWidget<TVM>`**: bindable component không đi qua router, dùng cho sub-view, list item, widget dùng lại (CurrencyBar, TimerLabel) | MVVM cần cây VM lồng nhau, không phải view nào cũng là screen/popup |
| B7 | **UI scope theo `LifetimeScope`**: scene scope gọi `builder.AddUIScope(sceneRegistry)`. VM của scope đó được resolve từ scope đó. Scope bị dispose thì view của nó bị đóng và release | Additive scene: VM trong scene gameplay cần service của scene đó |
| B8 | **Quy tắc concurrency**: `reopenPolicy` (BringToFront / Ignore / AllowMultiple). Open trong lúc Close thì chờ Close xong. Open trùng khi đang load thì trả về cùng task. Load lỗi thì ném exception cho caller và tự nhả input lock | Bản 2 chỉ nói "chống re-entrance" chung chung |
| B9 | **`Application.lowMemory`** → release các view KeepAlive đang ẩn | Mobile mid-core có nhiều popup nặng |
| B10 | **UI Debugger** (Editor window ở Play mode): stack, queue, input lock, VM đang active, binding count | Debug navigation phức tạp |
| B11 | **UI scale** runtime (hệ số nhân `CanvasScaler`) cho tuỳ chọn cỡ UI trên PC/Console | Yêu cầu accessibility phổ biến trên PC/Console |
| B12 | Extension point **`IUITextLocalizer`** + binder `b.Localized(text, key, args)`. Adapter tới `ILocalizationService` đặt ở phía game. `PrebuildServices/Localization` chưa có asmdef nên UISystem chưa reference được | Text tĩnh vẫn dùng `LocalizableTextMeshPro`, text động thì bind qua VM |

**Loại bỏ / đơn giản hoá:**
- Bỏ API view-first `OpenAsync<TView, TModel>`, `UIView<TModel>`, `IResultView`. Kết quả chuyển sang `IResultViewModel<TResult>`.
- `OnBackRequested` chuyển từ View sang **VM** (`BackResult HandleBack()`). Business logic quyết định việc "có chắc muốn thoát?".
- `cachePolicy` còn Destroy / KeepAlive. `preload` tách thành cờ `bool` riêng vì hai thứ này độc lập nhau.
- `Enqueue` bây giờ **trả kết quả** (`UniTask<TResult>`), không còn là `void`.
- Cooldown của `UIButton` giảm xuống vai trò phụ: mặc định 0, việc chống double-tap giao cho async command. `BindClick → Observable<Unit>` vẫn giữ, dùng cho trường hợp không cần command.
- Animator transition lùi xuống cuối phase transitions (có thể bỏ nếu không có nhu cầu). DOTween recipe là đường chính.
- Badge/red-dot (phase tuỳ chọn) được viết thành service VM thuần, không phụ thuộc engine.

### 2.1 Assemblies
| Assembly | Folder | Engine | Nội dung |
|---|---|---|---|
| `…UISystem.Logic` | `Logic/` | Không | `UIStack`, `UIPopupQueue` (priority + FIFO), `SortOrderAllocator`, `UIViewStateMachine` (Hidden→Showing→Shown→Hiding, chống re-entrance), `InputLockCounter`, `BackRouter` |
| `…UISystem.MVVM` | `MVVM/` | Không | `UIViewModel`, `UIViewModel<TArgs>`, `IResultViewModel<TResult>`, `BackResult`, `UICommand` / `AsyncUICommand`, `IUINavigator`. Chỉ reference R3 core + ObservableCollections (+ `.R3`) |
| `…UISystem.Testing` | `Testing/` | Không | `FakeUINavigator` (ghi lại các lệnh open/close, trả result giả) để game test VM |
| `…UISystem` | `Data/ Core/ Core/Loading/ Views/ Binding/ Transitions/ Components/ Focus/ Input/ Installer/` | Có | Runtime uGUI trực tiếp: `IUIService : IUINavigator`, registry, router, VM factory/scope, layer root, `UIView<TVM>`, `UIWidget<TVM>`, `UIBinder`, transitions, components, focus, back input, installer |
| `…UISystem.Editor` | `Editor/` | Editor | Registry window, VM type picker, validator + build validator, UI Debugger |
| `…UISystem.Tests` | `Tests/Editor/` | Editor | EditMode test cho Logic + MVVM |
| `…UISystem.PlayModeTests` | `Tests/Runtime/` | | Smoke test runtime |

Dependencies của runtime:
- UniTask (+ `UniTask.DOTween`, `UniTask.Addressables`), VContainer, R3 + `R3.Unity`, ObservableCollections(+`.R3`), DOTween, `Unity.ugui`, TextMeshPro.
- Input System qua `versionDefines` → `UISYSTEM_INPUT_SYSTEM`.
- Addressables sau `USE_EXTENDED_ADDRESSABLE`.
- `DracoRuan.Foundation.Initializers`.

**Bỏ** `Utilities.ObjectPooling` và Odin ở runtime. Field hiện có điều kiện thì làm bằng PropertyDrawer trong Editor asmdef.

> Lưu ý khi làm phase 2: kiểm tra DLL NuGet của R3/ObservableCollections dùng được trong asmdef `noEngineReferences: true` (cần `overrideReferences` + `precompiledReferences`). Nếu không được thì bỏ cờ này đi, nhưng vẫn giữ quy tắc "MVVM không dùng `UnityEngine`" và kiểm tra bằng validator/grep.

### 2.2 Data (ScriptableObject)
- `UILayerDefinition`: name, `baseSortOrder`, `sortStep` (khoảng trống để particle/sub-canvas của view chen vào), kind (Screen / Popup / Overlay / Toast / System / Tutorial), `applySafeArea`, `blocksInputBelow`. Có asset mặc định, game tự thêm layer được. Thay cho enum `CanvasCategory`.
- `UIRootConfig`: danh sách layer, `CanvasScaler` (reference resolution, match), render mode (Overlay hoặc Screen Space Camera với UI camera), plane distance, `pixelPerfect`, giới hạn UI scale. Được áp dụng **lúc runtime** khi dựng layer root.
- `UIViewDefinition` (entry trong `UIRegistry`):
  - **Key = ViewModel Type**, chọn qua type picker. Validator kiểm tra prefab có `UIView<TVM>` với đúng VM đó.
  - Layer, kind.
  - Prefab: direct reference hoặc `AssetReferenceGameObject`.
  - `cachePolicy` (Destroy / KeepAlive), `preload`, `hideMode` (DisableCanvas mặc định / Deactivate).
  - `reopenPolicy`.
  - `modal`, `closeOnBackdrop`, `defaultPriority`, `scope` (Screen / Global).
  - Transition in/out.
- `UIRegistry`: dictionary `Type → definition` được dựng **trong service**, không nằm trong SO. Validate lúc init. Có nhiều registry: một cho root, và mỗi `UIScope` thêm registry của riêng nó.

### 2.3 MVVM
```csharp
// .MVVM — C# thuần
public abstract class UIViewModel : IDisposable {
    protected CompositeDisposable Disposables { get; }       // reset mỗi lần activate
    protected IUINavigator Navigator { get; }                // inject
    protected virtual void OnActivated() {}  protected virtual void OnDeactivated() {}
    public virtual BackResult HandleBack() => BackResult.Close;
    protected void RequestClose();
}
public abstract class UIViewModel<TArgs> : UIViewModel { protected abstract void OnActivated(TArgs args); }
public interface IResultViewModel<TResult> { void Complete(TResult result); }   // base helper: ResultViewModel<TArgs, TResult>

public interface IUINavigator {
    ValueTask OpenAsync<TVM>(CancellationToken ct = default) where TVM : UIViewModel;
    ValueTask OpenAsync<TVM, TArgs>(TArgs args, CancellationToken ct = default) where TVM : UIViewModel<TArgs>;
    ValueTask<TResult> OpenForResultAsync<TVM, TArgs, TResult>(TArgs args, CancellationToken ct = default)
        where TVM : UIViewModel<TArgs>, IResultViewModel<TResult>;
    ValueTask<TResult> EnqueueAsync<TVM, TArgs, TResult>(TArgs args, int priority = 0) where …;
    ValueTask CloseAsync<TVM>(); ValueTask CloseAsync(UIViewModel vm);
    ValueTask PopScreenAsync(); ValueTask PopToRootAsync();
}
```
- **Vòng đời VM**:
  1. Tạo mới mỗi lần open (Transient, lấy từ resolver của scope sở hữu registry).
  2. `Activate(args)`.
  3. View `Bind`.
  4. Khi Close: View `Unbind`, VM `Deactivate`, rồi `Dispose`.
  
  KeepAlive chỉ áp dụng cho **instance prefab**, không giữ lại VM.
- **Command**: `UICommand` / `UICommand<T>` / `AsyncUICommand`. `CanExecute` là `ReadOnlyReactiveProperty<bool>`, có thể ghép từ nhiều nguồn. `IsExecuting` dùng cho spinner. Async command dùng `AwaitOperation.Drop` và hỗ trợ cancel khi VM deactivate.
- **State**: R3 `ReactiveProperty<T>` / `ReadOnlyReactiveProperty<T>`. Collection dùng `ObservableList<T>` / `ObservableDictionary<TKey,TValue>`, expose ra ngoài dưới dạng read-only.

### 2.4 View, Widget, Binder (uGUI)
```csharp
public abstract class UIView<TVM> : UIViewBase where TVM : UIViewModel {      // routed, root prefab
    protected abstract void Bind(UIBinder b, TVM vm);
    [SerializeField] Selectable defaultSelectable;                            // focus
}
public abstract class UIWidget<TVM> : MonoBehaviour where TVM : class {       // không routed: sub-view, list item
    protected abstract void Bind(UIBinder b, TVM vm);
}
// ví dụ
protected override void Bind(UIBinder b, ShopViewModel vm) {
    b.Text(goldText, vm.Gold);                          // int → TMP.SetText không alloc
    b.Command(buyButton, vm.Buy);                       // interactable = CanExecute, bấm lại khi đang chạy bị bỏ qua
    b.TwoWay(volumeSlider, vm.Volume);
    b.List(itemList, vm.Items, itemWidgetPrefab);       // ObservableList → UIRecycleList
    b.Widget(currencyBar, vm.Currency);                 // VM con
    b.Active(saleBadge, vm.IsOnSale);
}
```
- `UIViewBase` (MonoBehaviour trên root prefab, `[RequireComponent(Canvas, GraphicRaycaster, CanvasGroup)]`). Framework gọi các hook (**protected virtual**): `OnCreated`, `OnOpening`, `OnOpened`, `OnClosing`, `OnClosed`, `OnFocused` / `OnBlurred`. Hook chỉ dành cho việc thuần hiển thị như animation hay VFX. Logic đặt trong VM. Base class **không dùng Unity message virtual** (`Awake`/`OnDestroy`) cho logic framework, nhằm tránh lặp lại lỗi A10.
- `UIBinder`: một instance cho mỗi lần bind. Mỗi hàm trả `IDisposable` và được gom lại, `Unbind` dispose hết.
  - One-way: `Text`, `Active`, `Interactable`, `Fill`, `Sprite`, `Color`, `Localized`.
  - Two-way: `TwoWay(slider / toggle / TMP_InputField)`.
  - Command: `Command(UIButton)`.
  - Collection và lồng nhau: `List`, `Widget`.
  - Tự mở rộng: `Custom(observable, action)`.
- DI: instance prefab được `InjectGameObject` **một lần lúc tạo**. Widget nhận VM qua binder, không qua DI.

### 2.5 Hành vi runtime
- **Layer root**: mỗi layer là một root Canvas, dựng từ `UIRootConfig`: CanvasScaler + GraphicRaycaster, và `SafeAreaFitter` nếu `applySafeArea`. Có thể đặt sẵn trong scene hoặc để service tự tạo (`DontDestroyOnLoad`).
- **Router**:
  - Screen dùng stack. Push thì blur hoặc ẩn screen dưới (theo `hidesBelow`).
  - Popup có stack riêng theo layer. Mỗi view là **nested Canvas `overrideSorting`**, sort order cấp bởi `SortOrderAllocator`.
  - Backdrop dùng chung, luôn nằm ngay dưới modal trên cùng. Click vào backdrop thì gọi `vm.HandleBack()` nếu `closeOnBackdrop`.
  - Đổi screen thì đóng popup có `scope = Screen`.
- **Scope**:
  - `UIScope` đăng ký registry cùng resolver của `LifetimeScope`.
  - Khi mở VM, service tìm registry chứa VM đó, ưu tiên scope trong cùng trước, rồi resolve VM từ resolver của scope ấy.
  - Scope bị dispose thì đóng ngay (không chạy transition) các view thuộc scope đó và release asset.
- **Queue**: `EnqueueAsync` chờ tới khi không còn modal nào và input không bị lock thì mở item priority cao nhất. Có `Pause/Resume` cho tutorial và cutscene.
- **Back**: `IUIBackInputSource`. Mặc định là `InputSystemBackInputSource` dùng action `UI/Cancel` (Esc, gamepad B/Circle, Android back). `BackRouter` duyệt từ layer cao xuống: System > Popup > Screen, rồi gọi `vm.HandleBack()` để nhận về Close / Consume / PassThrough. Khi đang transition hoặc lock thì Back bị nuốt. Ở root screen thì bắn `BackAtRoot`.
- **Result**: `OpenForResultAsync` trả kết quả khi VM gọi `Complete(result)`. Nếu view đóng theo đường khác (Back, backdrop, CloseAll, scope dispose) thì trả `default`, hoặc giá trị VM override.
- **Focus (PC/Console)**:
  - Khi blur thì nhớ `EventSystem.currentSelectedGameObject`, khi lộ lại thì khôi phục.
  - Chỉ select khi thiết bị cuối cùng là gamepad hoặc bàn phím. Dùng chuột/touch thì bỏ select.
  - Chặn navigation thoát ra khỏi view modal.
- **Transition**: mọi tween và delay dùng **unscaled time** (`SetUpdate(true)`, `DelayType.UnscaledDeltaTime`). Trong transition, input bị lock và `CanvasGroup.blocksRaycasts = false`. Hủy qua `CancellationToken` của view.
- **Animation recipe**:
  - `UITransition` SO **không có state**, chỉ có `Tween Create(in UITransitionTarget t)` với `t` = (RectTransform, CanvasGroup, rest pose).
  - Base class xử lý delay/loop/ease một chỗ.
  - Mọi tween được `SetLink(go)` + `SetTarget` rồi trả về để host giữ và kill.
  - Sequential/Parallel compose bằng cách tạo tween mới mỗi lần.
  - Move dùng offset `anchoredPosition`.
- **Loading**: `IUIAssetProvider` có 2 cách load là Direct và Addressables. Addressables dùng lease + refcount, theo mẫu `AudioSystem/Core/Loading/AudioClipLibrary.cs` + `AudioClipLease.cs`. Instantiate thẳng vào parent với `worldPositionStays = false` (sửa A5). Preload chạy trong `IAsyncInitializable` của service. `Application.lowMemory` → release view KeepAlive đang ẩn.
- **Components**:
  - `UIButton` là class concrete, không kế thừa view. Cooldown tuỳ chọn (mặc định 0), dùng unscaled time. Trạng thái `interactable` tách khỏi cooldown lock (AND hai cờ). Có hook `IUIClickFeedback` để game cắm AudioSystem/MobileVibration.
  - `UISlider`, `UIToggle`.
  - `SafeAreaFitter`, `UIBackdrop`, `UIParticleSortingBinder`.
  - `UIRecycleList`.
- **Events**: `Observable<UIViewEvent>` (Opened / Closed / Focused / BackAtRoot, kèm VM type) cho analytics và tutorial.
- **Installer**: `UIInstaller` (SO, `[AutoInstall]`) + `builder.AddUIService(rootConfig, registry)` + `builder.AddUIScope(registry)`, theo mẫu `AudioSystem/Installer/`. Installer tự `Register` VM trong registry với lifetime Transient.

### 2.6 uGUI chuyên sâu (hiệu năng và best practice cho mid-core)
- **Hide không rebuild**: `hideMode = DisableCanvas` (mặc định) tắt `Canvas.enabled` + `GraphicRaycaster.enabled` và giữ GameObject active, để tránh `OnDisable/OnEnable`, layout rebuild và TMP regenerate. `Deactivate` chỉ dùng cho view có logic Update cần dừng hẳn.
- **Cô lập rebuild**: mỗi view có Canvas riêng. Phần đổi liên tục như timer, currency, progress bar thì tách **sub-canvas**. Sample có minh hoạ.
- **Raycast hygiene**: validator cảnh báo `raycastTarget` thừa trên Image/TMP không có component tương tác. Mỗi Canvas view có đúng một `GraphicRaycaster`. Layer không tương tác thì tắt raycaster.
- **Layout hygiene**: validator cảnh báo `LayoutGroup` lồng sâu, và `ContentSizeFitter` nằm trong `LayoutGroup`. Khuyến nghị dùng `RectMask2D` thay cho `Mask`.
- **URP camera stacking**: hỗ trợ `Screen Space - Camera` với UI camera dạng Overlay trong stack của main camera, dùng cho particle và model 3D trong UI.
- **Particle trong UI**: `UIParticleSortingBinder` gán `sortingOrder` của renderer = `canvas.sortingOrder + offset`, nằm trong khoảng `sortStep`.
- **Không alloc khi bind**: TMP dùng `SetText`. Collection binding cập nhật theo từng phần tử, không rebuild cả list.
- (Ngoài scope, chỉ để link) Sprite atlas, TMP font fallback.

---

## 3. Layout folder đích
```
UISystem/
  REWRITE_PLAN.md  README.md
  Logic/        Navigation/ Queue/ Layers/ Lifecycle/ Input/                 (.Logic, no engine)
  MVVM/         ViewModels/ Commands/ Navigation/                            (.MVVM, no engine)
  Testing/                                                                  (.Testing, no engine)
  Data/ Core/ Core/Loading/ Views/ Binding/ Transitions/
  Components/ Focus/ Input/ Installer/                                       (runtime asmdef ở root UISystem)
  Editor/       Windows/ Drawers/ Validation/ Debugger/                      (.Editor)
  Tests/Editor/  Tests/Runtime/
  Samples/      Confirm (result), Toast, Loading, 2 screen, Inventory (list + widget), Settings (two-way)
```
Code cũ (`Animations/ Canvases/ Popups/ Views/ UIElements/ UIManager.cs`) sẽ bị `git rm` ở phase 9, sau khi sample mới chạy được. Folder `Views/` mới thay cho folder cũ cùng tên. Khi viết thì dùng scratchpad + `cp` để tránh lỗi Rider hook.

---

## 4. Tái sử dụng từ repo
- Mẫu asmdef, installer, README, tests: `PrebuildServices/AudioSystem/**`.
- Mẫu Addressables lease/refcount + `USE_EXTENDED_ADDRESSABLE`: `AudioSystem/Core/Loading/AudioClipLibrary.cs`, `AudioClipLease.cs`.
- `[AutoInstall]`, `IAsyncInitializable`: `Foundation/Initializers/`.
- Localization: `PrebuildServices/Localization/ILocalizationService.cs`. Adapter `IUITextLocalizer` viết phía game. `LocalizableTextMeshPro` vẫn dùng cho text tĩnh.
- Ý tưởng recipe (Fade/Scale/Move/Sequential/Parallel): chuyển sang dạng stateless.
- **Không** dùng `Utilities/ObjectPooling` (có các lỗi ở A5/A11). Một view chỉ cần một instance cache. `UIRecycleList` tự quản lý item pool của riêng nó.

---

## 5. Thứ tự triển khai (mỗi phase kết thúc bằng compile-check, và chạy test nếu phase đó có test)
0. ~~Lưu review + plan thành `UISystem/REWRITE_PLAN.md`~~ (**đã xong**: chính là file này).
1. **Logic**: asmdef `.Logic` + `UIStack`, `UIPopupQueue`, `SortOrderAllocator`, `UIViewStateMachine`, `InputLockCounter`, `BackRouter`. Viết EditMode test cho từng class: thứ tự priority, re-entrance, back khi đang lock, cấp lại sort order sau khi pop.
2. **MVVM**: cài ObservableCollections + ObservableCollections.R3 qua NuGetForUnity. Tạo asmdef `.MVVM` và `.Testing`: `UIViewModel`, command, `IUINavigator`, `FakeUINavigator`. Viết EditMode test cho vòng đời VM (activate/deactivate/dispose, reset `Disposables`), async command (drop, cancel, `CanExecute`) và `ResultViewModel`.
3. **Core runtime**:
   - Data SO, layer root theo `UIRootConfig`.
   - `UIViewBase`, `UIView<TVM>`, `UIWidget<TVM>`, `UIBinder` (one-way, two-way, command, widget).
   - `UIService : IUINavigator`, router, VM factory + `UIScope`, cache, `IUIAssetProvider` (Direct), backdrop, input lock.
   - Installer (tự register VM).
4. **Transitions + Components**: viết lại Fade/Scale/Move/Sequential/Parallel ở dạng stateless, unscaled time (Animator để cuối phase, có thể bỏ). Thêm `UIButton`, `UISlider`, `UIToggle`, `SafeAreaFitter`, `UIParticleSortingBinder`. PlayMode smoke test gồm open/close, CloseAll, queue, `timeScale = 0`, popup đã đóng không còn chặn raycast, và hai view dùng chung một transition.
5. **Input + Focus**: `InputSystemBackInputSource`, `BackRouter` → `vm.HandleBack()`, focus controller. PlayMode test cho Back và focus.
6. **Addressables provider** (lease/refcount, preload, release, lowMemory) sau `USE_EXTENDED_ADDRESSABLE`.
7. **`UIRecycleList` + collection binding**: dọc/ngang/grid, item có kích thước cố định (bản đầu), `ScrollTo(index)`, bind `ObservableList` qua `ISynchronizedView` → `UIWidget<TItemVM>`.
8. **Editor**: Registry window, VM type picker, validator (key, prefab ↔ VM, raycast/layout hygiene, VM không dùng `UnityEngine`) + build validator + UI Debugger.
9. **Samples + README**: Confirm, Toast, Loading, 2 screen, Inventory, Settings. Sau đó `git rm` code cũ.
10. (Tuỳ chọn) `UITabGroup`, Badge/red-dot service (VM thuần, cây key → count), hook tutorial highlight (mask lỗ + chặn input ngoài vùng), UI scale setting.

---

## 6. Verification
- **Compile-check ngoài Unity** sau mỗi phase, theo công thức trong memory `rider-hook-stale-buffer`: Roslyn của Unity (`Editor/Data/NetCoreRuntime/dotnet.exe` + `DotNetSdkRoslyn/csc.dll`) với `Library/ScriptAssemblies/*.dll` + `UnityEngine/*.dll` + netstandard 2.1. Chạy với define `UNITY_EDITOR`, `UNITY_ANDROID`, `UNITY_IOS`, có và không có `USE_EXTENDED_ADDRESSABLE`. Lưu ý: file vừa `git mv` thì viết qua scratchpad rồi `cp`, vì Rider hook có thể ghi đè bằng buffer cũ.
- **EditMode tests** (`Tests/Editor`) cho Logic + MVVM, chạy bằng Unity Test Runner hoặc `Unity -runTests -testPlatform EditMode`. Sample VM có test dùng `FakeUINavigator`: ví dụ Buy → mở Confirm → trả `true` → Gold giảm.
- **PlayMode smoke tests** (`Tests/Runtime`). Phải đạt các kịch bản đúng như bug cũ:
  - (a) mở rồi đóng popup, click xuyên được xuống dưới;
  - (b) mở cùng VM 2 lần với `BringToFront`, chỉ có 1 instance;
  - (c) `CloseAllAsync` không throw, và result đang chờ trả `default`;
  - (d) `timeScale = 0` vẫn mở/đóng được;
  - (e) animation Parallel chạy đúng ở lần mở thứ 2;
  - (f) hai view dùng chung một transition asset không ảnh hưởng nhau;
  - (g) Back gọi `HandleBack` của VM trên cùng, ở root thì bắn `BackAtRoot`;
  - (h) `OpenForResultAsync` trả đúng kết quả, và đóng bằng Back thì trả `default`;
  - (i) view KeepAlive mở lại không bị layout rebuild toàn bộ (kiểm tra bằng Profiler marker `Canvas.BuildBatch` / `Layout`);
  - (j) focus không thoát khỏi modal;
  - (k) bấm liên tiếp nút gắn async command chỉ chạy 1 lần;
  - (l) `ObservableList` Add/Remove/Move được phản ánh đúng vào `UIRecycleList`;
  - (m) dispose scene `LifetimeScope` thì đóng view của scope đó, và VM được `Dispose`;
  - (n) sau Close không còn subscription nào (đếm binding bằng UI Debugger/test hook).
- **Thủ công trong Editor**:
  - Chạy sample scene trên Device Simulator (safe area, notch, đổi orientation).
  - Thử gamepad/bàn phím (PC).
  - Dùng Frame Debugger và Profiler để kiểm tra batch và rebuild khi timer/currency cập nhật liên tục.
