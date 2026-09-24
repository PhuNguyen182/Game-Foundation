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
| Phạm vi | **Viết lại theo phase**, theo khuôn AudioSystem (asmdef, Logic thuần C# có test, Installer `[AutoInstall]`, README). Animation cũ (nhiều SO, mỗi SO là một loại component) được thay bằng component `UIMotion` (xem dòng Animation) |
| Pattern | **MVVM, ViewModel-first**: code game chỉ biết ViewModel (`OpenAsync<ShopViewModel>(args)`). Registry map VM type → prefab view. VM là C# thuần, tạo qua VContainer, test EditMode được. Câu hỏi "key bằng Type hay id sinh code" được trả lời luôn: **key = VM type** |
| Collection binding | **Thêm ObservableCollections** (Cysharp, NuGet `ObservableCollections` + `ObservableCollections.R3`) để bind list/grid vào `UIRecycleList` |
| Thực thể UI | **Chỉ một loại View**. Screen/Popup/HUD/Toast/Hint/System/Tutorial là **preset cấu hình**, không phải class riêng. Một popup full màn hình có animation kiểu screen vẫn chạy được, nhưng muốn có hành vi của screen thì phải bật các cờ (`hidesBelow`, `history`). Tab/panel bên trong một screen là widget, không đi qua router (mục 2.2) |
| Animation | **Component `UIMotion` tự viết** (asmdef riêng, độc lập với UIView). Timeline chỉnh ngay trong Inspector, preview trong Edit mode, runtime tự sample. **Không DOTween, không adapter, không Unity Timeline**. Mecanim là một loại track (`AnimatorState`). Serialization thường, không `SerializeReference` (mục 2.7) |
| Disposable | **Không dùng `CompositeDisposable`**, theo yêu cầu của user. Dùng các kiểu struct của R3: `Disposable.CreateBuilder()` / `DisposableBag` / `Disposable.Combine`. `UIBinder` là `ref struct` (mục 2.3, 2.4) |

**Lịch sử chỉnh plan:**
- **Bản 1** (commit `fb4b461`): uGUI + UI Toolkit, có lớp backend abstraction.
- **Bản 2** (không commit riêng, gộp vào bản này): bỏ UI Toolkit. Thêm mục "uGUI chuyên sâu": hide bằng `Canvas.enabled`, raycast/layout hygiene, URP camera stacking, particle sorting. Virtualized list thành phase chính thức.
- **Bản 3** (commit `606327d`): tự review lại bản 2 theo hướng MVVM. Kết quả ở mục 2.0.
- **Bản 4** (không commit riêng): thêm View preset (Hint/Tooltip, HUD, `UITabGroup` đưa lên phase chính), animation adapter, và bỏ `CompositeDisposable`. Đây là kết quả của các câu hỏi:
  - "Có cần Screen/View/Panel/Hint riêng không, hay coi popup là thể duy nhất?"
  - "Tổ chức Mecanim/DOTween thành adapter để sau này thay bằng LitMotion/PrimeTween được không?"
  - góp ý của user về `CompositeDisposable`.
- **Bản 5** (file này): **bỏ adapter**, thay bằng component `UIMotion` (mục 2.7), có thêm track Animator State. Diễn biến dẫn tới quyết định này:
  - User thấy nhiều SO, mỗi SO là một loại component, kém hiệu quả.
  - User hỏi "dùng Timeline được không". Kết luận: không dùng Unity Timeline, mà tự viết component dạng timeline.
  - User đặt 5 tiêu chí: nhiều target; tuần tự hoặc song song; reset khi bật lại; Show/Hide chờ xong mới despawn; độc lập với UIView. Đáp ứng đủ nên bỏ DOTween, Mecanim backend và adapter.
  - User đặt 3 điều kiện cho track Animator: test được trong Editor, runtime tốt mọi platform, ổn định.
  - User hỏi "dùng gì, có ổn định không". Kết quả: chuyển từ `SerializeReference` sang serialization thường.
  - User hỏi "backend của UIMotion là gì". Trả lời: không có backend. `UIMotion` tự tính thời gian (PlayerLoop + `unscaledDeltaTime`), tự tính ease, và ghi thẳng vào component. Riêng track AnimatorState giao việc cho Animator.
  - User yêu cầu animate đủ các thuộc tính của RectTransform (anchoredPosition, pivot, anchorMin/Max…), không chỉ vị trí. Kết quả: thêm track `Rect` (mục 2.7).
  - User yêu cầu Start/Target value là **tuỳ chọn riêng của từng track**. Bật thì dùng cả Start và Target. Tắt thì Start = giá trị hiện tại của component, và Inspector ẩn field Start. Kết quả: thêm `useStartValue` cho từng track, value mode `RelativeToStart`, và Mirror xử lý theo từng track (mục 2.7).

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
- `MoveAnchorAnimation` tween **anchor** thay vì `anchoredPosition`, và không bù offset, nên làm hỏng layout của các con. Bản mới hỗ trợ animate anchor một cách chủ đích (track `Rect`, có tuỳ chọn giữ nguyên vị trí hiển thị, mục 2.7).
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
| B13 | **`UIMotion`** (mục 2.7): component animation độc lập. Timeline trong Inspector, preview trong Edit mode, có Show/Hide và preset SO. Runtime tự sample, không phụ thuộc thư viện tween. Có track Animator State | Một asset/component thay cho 4–5 SO. Chỉnh trực quan, dùng được cho mọi object. Không khoá vào thư viện nào |
| B14 | **View preset** (mục 2.2) + **Hint/Tooltip** (`UIAnchorPlacement`) + **HUD** (`visibleOnScreens`) + `UITabGroup` được đưa lên phase chính | Một primitive duy nhất cho router, còn hành vi do cờ quyết định. Tooltip và tab là nhu cầu phổ biến của mid-core |

**Loại bỏ / đơn giản hoá:**
- Bỏ API view-first `OpenAsync<TView, TModel>`, `UIView<TModel>`, `IResultView`. Kết quả chuyển sang `IResultViewModel<TResult>`.
- `OnBackRequested` chuyển từ View sang **VM** (`BackResult HandleBack()`). Business logic quyết định việc "có chắc muốn thoát?".
- `cachePolicy` còn Destroy / KeepAlive. `preload` tách thành cờ `bool` riêng vì hai thứ này độc lập nhau.
- `Enqueue` bây giờ **trả kết quả** (`UniTask<TResult>`), không còn là `void`.
- Cooldown của `UIButton` giảm xuống vai trò phụ: mặc định 0, việc chống double-tap giao cho async command. `BindClick → Observable<Unit>` vẫn giữ, dùng cho trường hợp không cần command.
- Bỏ `AnimationMachine`, `ViewAnimationConfig` và các SO `Fade/Scale/MoveAnchor/Sequential/ParallelAnimation`. Mỗi loại chỉ có 1 asset mẫu và không prefab nào dùng, nên không cần công cụ chuyển đổi. **Bỏ DOTween khỏi UISystem.** Mecanim chỉ còn là track `AnimatorState` bên trong `UIMotion`.
- Badge/red-dot (phase tuỳ chọn) được viết thành service VM thuần, không phụ thuộc engine.

### 2.1 Assemblies
| Assembly | Folder | Engine | Nội dung |
|---|---|---|---|
| `…UISystem.Logic` | `Logic/` | Không | `UIStack`, `UIPopupQueue` (priority + FIFO), `SortOrderAllocator`, `UIViewStateMachine` (Hidden→Showing→Shown→Hiding, chống re-entrance), `InputLockCounter`, `BackRouter` |
| `…UISystem.MVVM` | `MVVM/` | Không | `UIViewModel`, `UIViewModel<TArgs>`, `IResultViewModel<TResult>`, `BackResult`, `UICommand` / `AsyncUICommand`, `IUINavigator`. Chỉ reference R3 core + ObservableCollections (+ `.R3`) |
| `…UISystem.Testing` | `Testing/` | Không | `FakeUINavigator` (ghi lại các lệnh open/close, trả result giả) để game test VM |
| `…UISystem.Motion` | `Motion/` | Có | `UIMotion`, `UIMotionTrack`, `UIEase`, `UIMotionRunner`, `UIMotionPreset` SO, `IUIMotionTriggerSource`, `IUIMotionCustomTrack`. **Không reference UISystem runtime.** Chỉ phụ thuộc UniTask + `PlayerLoopSystem.UpdateServices` |
| `…UISystem.Motion.Editor` | `Motion/Editor/` | Editor | Inspector timeline, preview/scrub bằng `AnimationMode`, bake Animator track, validator |
| `…UISystem` | `Data/ Core/ Core/Loading/ Views/ Binding/ Components/ Focus/ Input/ Installer/` | Có | Runtime uGUI trực tiếp: `IUIService : IUINavigator`, registry, router, VM factory/scope, layer root, `UIView<TVM>`, `UIWidget<TVM>`, `UIBinder`, components, focus, back input, installer. Reference `Motion` (chiều ngược lại thì không) |
| `…UISystem.Editor` | `Editor/` | Editor | Registry window, VM type picker, validator + build validator, UI Debugger |
| `…UISystem.Tests` | `Tests/Editor/` | Editor | EditMode test cho Logic + MVVM |
| `…UISystem.PlayModeTests` | `Tests/Runtime/` | | Smoke test runtime |

Dependencies của runtime:
- UniTask (+ `UniTask.Addressables`), VContainer, R3 + `R3.Unity`, ObservableCollections(+`.R3`), `Unity.ugui`, TextMeshPro.
- UISystem **không** phụ thuộc thư viện tween nào (kể cả DOTween). Phần còn lại của game vẫn dùng DOTween bình thường nếu muốn.
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
  - Layer, **`preset`** (xem bảng bên dưới).
  - Prefab: direct reference hoặc `AssetReferenceGameObject`.
  - `cachePolicy` (Destroy / KeepAlive), `preload`, `hideMode` (DisableCanvas mặc định / Deactivate).
  - `reopenPolicy`.
  - `modal`, `closeOnBackdrop`, `defaultPriority`, `scope` (Screen / Global).
  - `hidesBelow`: tắt Canvas của các view bên dưới sau khi transition in xong, và bật lại trước khi transition out bắt đầu.
  - `history`: có vào lịch sử screen không.
  - `participatesInStack`, `visibleOnScreens` (dành cho HUD).
  - Animation: `UIMotion` trên root prefab (timeline Show/Hide, mục 2.7). Không có `UIMotion` thì xem như Instant. `speedOverride` cho từng view.

**Nguyên tắc: View là thực thể duy nhất mà router quản lý.** Screen/Popup/HUD… **không phải class riêng**. `UIViewPreset` chỉ **điền giá trị mặc định** cho các cờ, và từng cờ vẫn chỉnh riêng được. Ví dụ: một popup full màn hình bật `hidesBelow` để không vẽ phần bên dưới, mà Back vẫn là "close". Về hình ảnh, popup full màn hình với animation kiểu screen (slide ngang) trông không khác gì screen. Khác biệt chỉ nằm ở **hành vi**, và hành vi do cờ quyết định. Nếu thiếu `hidesBelow` thì mọi thứ bên dưới vẫn được render (overdraw, tốn batch).

| Preset | Layer mặc định | Cờ mặc định | Ghi chú |
|---|---|---|---|
| **Screen** | Screen | `hidesBelow`, `history`, Back = pop, đóng popup có `scope = Screen` khi rời screen | Có thể mở kiểu "Replace" để không lưu lịch sử (ví dụ Loading → Home). Tính là "màn hình hiện tại" cho analytics/tutorial |
| **Popup** | Popup | `modal`, backdrop, Back = close | Có queue và trả kết quả |
| **Overlay / HUD** | Overlay | `participatesInStack = false`, Back = PassThrough, không backdrop | Mở một lần và tồn tại lâu. Hiện hoặc ẩn theo `visibleOnScreens` khi screen active thay đổi |
| **Toast** | Toast | không chặn input, tự đóng, `AllowMultiple`, có queue | Không nhận raycast |
| **Hint / Tooltip** | Tooltip | không modal, bấm ra ngoài thì đóng, `reopenPolicy = Replace` (một instance cho mỗi layer) | Cần thêm `UIAnchorPlacement` (mục 2.5) |
| **System** | System | khoá input, nuốt Back | Loading, blocker. Luôn nằm trên cùng |
| **Tutorial** | Tutorial | chặn input ngoài vùng highlight | Phase tuỳ chọn |

**Những thứ không phải View** (không đi qua router, để không làm rối lịch sử và Back):
- Tab/panel bên trong một screen (các tab của Shop, Inventory, Event): dùng `UITabGroup` + `UIWidget<TVM>`, việc đổi tab là state của VM. Nếu muốn Back quay về tab trước thì xử lý trong `HandleBack()` của VM.
- Thành phần con (CurrencyBar, TimerLabel, item trong list): dùng `UIWidget<TVM>`.
- `UIRegistry`: dictionary `Type → definition` được dựng **trong service**, không nằm trong SO. Validate lúc init. Có nhiều registry: một cho root, và mỗi `UIScope` thêm registry của riêng nó.

### 2.3 MVVM
```csharp
// .MVVM — C# thuần
public abstract class UIViewModel : IDisposable {
    private IDisposable _activation = Disposable.Empty;      // subscription của lần activate hiện tại
    private DisposableBag _late;                              // subscription thêm sau khi activate (async load, v.v.)
    protected IUINavigator Navigator { get; }                // inject
    protected CancellationToken ActivationToken { get; }     // bị cancel khi deactivate

    internal void Activate() {                               // framework gọi
        var d = Disposable.CreateBuilder();
        OnActivated(ref d);
        _activation = d.Build();
    }
    internal void Deactivate() { _activation.Dispose(); _activation = Disposable.Empty; _late.Clear(); OnDeactivated(); }

    protected virtual void OnActivated(ref DisposableBuilder d) {}
    protected virtual void OnDeactivated() {}
    protected ref DisposableBag Late => ref _late;           // dùng: x.Subscribe(...).AddTo(ref Late)
    public virtual BackResult HandleBack() => BackResult.Close;
    protected void RequestClose();
}
public abstract class UIViewModel<TArgs> : UIViewModel {
    protected abstract void OnActivated(TArgs args, ref DisposableBuilder d);
}
// ví dụ
protected override void OnActivated(ShopArgs args, ref DisposableBuilder d) {
    _wallet.Gold.Subscribe(g => Gold.Value = g).AddTo(ref d);
    Buy.Subscribe(OnBuy).AddTo(ref d);
}
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
- **Quản lý disposable (không dùng `CompositeDisposable`)**: `CompositeDisposable` là class, thread-safe (có lock), và alloc khi thêm phần tử. Framework chỉ chạy trên main thread nên không cần những thứ đó. Dùng các kiểu struct của R3 theo tài liệu chính chủ:
  | Trường hợp | Dùng |
  |---|---|
  | Subscription đăng ký một lượt, số lượng thay đổi (activate, bind) | `Disposable.CreateBuilder()` + `.AddTo(ref d)` + `d.Build()` |
  | Số lượng cố định, biết trước | `Disposable.Combine(d1, d2, …)` (nhanh nhất) |
  | Cần thêm dần sau khi đã build (subscription sau async load, item động) | `DisposableBag` (struct, field không `readonly`, `.AddTo(ref bag)`, `Clear()`/`Dispose()`) |
  | Tài nguyên VM sở hữu suốt đời (các field `ReactiveProperty`, command) | Builder trong constructor, rồi `Build()` vào một field `_owned`. `Dispose()` của VM dispose `_owned` |

  Các ràng buộc cần nhớ:
  - `DisposableBuilder` là struct nên **phải truyền bằng `ref`**. Vì vậy `OnActivated` không được là `async`, và không được capture trong lambda.
  - Việc async trong VM dùng `ActivationToken`, hoặc đi qua `AsyncUICommand`.
  - Sau `Build()` thì không `Add` thêm được nữa. Cần thêm về sau thì dùng `Late` (`DisposableBag`).
  - Tất cả chỉ dùng trên main thread.
  
  Validator/grep kiểm tra: code foundation không có `CompositeDisposable`.
- **Command**: `UICommand` / `UICommand<T>` / `AsyncUICommand`. `CanExecute` là `ReadOnlyReactiveProperty<bool>`, có thể ghép từ nhiều nguồn. `IsExecuting` dùng cho spinner. Async command dùng `AwaitOperation.Drop` và hỗ trợ cancel khi VM deactivate.
- **State**: R3 `ReactiveProperty<T>` / `ReadOnlyReactiveProperty<T>`. Collection dùng `ObservableList<T>` / `ObservableDictionary<TKey,TValue>`, expose ra ngoài dưới dạng read-only.

### 2.4 View, Widget, Binder (uGUI)
```csharp
public abstract class UIView<TVM> : UIViewBase where TVM : UIViewModel {      // routed, root prefab
    protected abstract void Bind(ref UIBinder b, TVM vm);
    [SerializeField] Selectable defaultSelectable;                            // focus
}
public abstract class UIWidget<TVM> : MonoBehaviour where TVM : class {       // không routed: sub-view, list item
    protected abstract void Bind(ref UIBinder b, TVM vm);
}
// framework: var b = new UIBinder(...); Bind(ref b, vm); _binding = b.Build();   Unbind: _binding.Dispose();
// ví dụ
protected override void Bind(ref UIBinder b, ShopViewModel vm) {
    b.Text(goldText, vm.Gold);                          // int → TMP.SetText không alloc
    b.Command(buyButton, vm.Buy);                       // interactable = CanExecute, bấm lại khi đang chạy bị bỏ qua
    b.TwoWay(volumeSlider, vm.Volume);
    b.List(itemList, vm.Items, itemWidgetPrefab);       // ObservableList → UIRecycleList
    b.Widget(currencyBar, vm.Currency);                 // VM con
    b.Active(saleBadge, vm.IsOnSale);
}
```
- `UIViewBase` (MonoBehaviour trên root prefab, `[RequireComponent(Canvas, GraphicRaycaster, CanvasGroup)]`). Framework gọi các hook (**protected virtual**): `OnCreated`, `OnOpening`, `OnOpened`, `OnClosing`, `OnClosed`, `OnFocused` / `OnBlurred`. Hook chỉ dành cho việc thuần hiển thị như animation hay VFX. Logic đặt trong VM. Base class **không dùng Unity message virtual** (`Awake`/`OnDestroy`) cho logic framework, nhằm tránh lặp lại lỗi A10.
- `UIBinder` là **`ref struct`** bọc một `DisposableBuilder`, nên bản thân binder không alloc class nào. Mỗi hàm bind thêm subscription vào builder. Sau `Bind`, framework gọi `Build()` để ra đúng một `IDisposable` cho lần bind đó, và `Unbind` dispose nó. Widget con (`b.Widget`, item của `b.List`) tự có builder riêng. Binder cha chỉ giữ disposable "unbind con" của từng widget.
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
- **Transition**: UIView gọi `UIMotion.PlayShowAsync` / `PlayHideAsync` và **await Hide xong mới** ẩn, despawn hoặc release. UIView implement `IUIMotionTriggerSource`, để các `UIMotion` con có trigger OnParentShow chạy theo. Luôn dùng unscaled time. Trong transition, input bị lock và `CanvasGroup.blocksRaycasts = false`. Hủy qua `CancellationToken` của view (hủy thì snap về pose cuối). Chi tiết ở **mục 2.7**.
- **HUD**: view preset Overlay, không vào stack. Router bật hoặc tắt HUD theo `visibleOnScreens` mỗi khi screen active thay đổi (dùng `hideMode`, không destroy).
- **Hint/Tooltip**: `UIAnchorPlacement` làm các việc sau:
  - neo vào `RectTransform` đích;
  - chọn phía (trên/dưới/trái/phải) theo chỗ trống, có danh sách phía ưu tiên;
  - clamp trong safe area;
  - đổi toạ độ giữa hai canvas khác render mode (Overlay ↔ Camera);
  - tuỳ chọn bám theo đích khi đích di chuyển hoặc scroll.
  
  Mỗi layer chỉ có một hint: mở hint mới thì hint cũ bị thay. Bấm ra ngoài thì đóng. Trên PC/Console, hint mở khi hover hoặc khi đích được focus (gamepad).
- **Loading**: `IUIAssetProvider` có 2 cách load là Direct và Addressables. Addressables dùng lease + refcount, theo mẫu `AudioSystem/Core/Loading/AudioClipLibrary.cs` + `AudioClipLease.cs`. Instantiate thẳng vào parent với `worldPositionStays = false` (sửa A5). Preload chạy trong `IAsyncInitializable` của service. `Application.lowMemory` → release view KeepAlive đang ẩn.
- **Components**:
  - `UIButton` là class concrete, không kế thừa view. Cooldown tuỳ chọn (mặc định 0), dùng unscaled time. Trạng thái `interactable` tách khỏi cooldown lock (AND hai cờ). Có hook `IUIClickFeedback` để game cắm AudioSystem/MobileVibration. Click punch là preset `ButtonPunch` của `UIMotion`.
  - `UISlider`, `UIToggle`, `UITabGroup`.
  - `SafeAreaFitter`, `UIBackdrop`, `UIParticleSortingBinder`, `UIAnchorPlacement`. Effect idle (pulse/shake) dùng `UIMotion` có loop.
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
- **Không alloc khi bind**: TMP dùng `SetText`. Binder là `ref struct` + `DisposableBuilder`, không dùng `CompositeDisposable`. Collection binding cập nhật theo từng phần tử, không rebuild cả list.
- (Ngoài scope, chỉ để link) Sprite atlas, TMP font fallback.

### 2.7 UIMotion: animation component độc lập
**Quyết định:** bỏ cơ chế adapter (`IUITransition` + `IUITweenDriver` + các asmdef `Tween.DOTween/PrimeTween/LitMotion`), thay bằng một component animation **tự viết**, thống nhất và trực quan ngay trong Inspector.
- **Không dùng DOTween**, và **không dùng Mecanim làm backend**. Mecanim chỉ còn là một loại track (`AnimatorState`).
- **Không dùng Unity Timeline package**, vì những lý do sau:
  - không chỉnh được trong Inspector (phải mở cửa sổ riêng);
  - Animation track dùng Animator, mà Animator trên uGUI ghi mọi property mỗi frame nên canvas rebuild liên tục;
  - custom track cần 3–4 class Playables cho mỗi loại;
  - mỗi lần play phải dựng một `PlayableGraph`;
  - không có sẵn giá trị tương đối với rest pose;
  - project chưa cài package này.

**Tiêu chí của user và cách đáp ứng:**
| Tiêu chí | Cách đáp ứng |
|---|---|
| Target được nhiều object/component | Mỗi track có field `target` tham chiếu trực tiếp (kéo thả) |
| Xếp chồng track, tuần tự hoặc song song | `startMode`: WithPrevious / AfterPrevious / AtTime, cộng `offset` |
| Tắt rồi bật lại thì reset, chạy từ đầu | Rest pose chụp lúc `Awake`. Mỗi lần play về frame 0 trước. Trigger OnEnable |
| Show/Hide, chờ Hide xong mới despawn | 2 timeline Show/Hide + `PlayHideAsync()`. UIView await xong mới ẩn/despawn |
| Component độc lập, chạy với mọi object | Asmdef `UISystem.Motion` **không reference UISystem runtime** |

**Công nghệ sử dụng** (không có thư viện bên thứ ba):
| Phần | Công nghệ |
|---|---|
| Lưu dữ liệu | `MonoBehaviour` + `ScriptableObject` (preset), serialization **thường** của Unity |
| Nội suy | Toán tự viết (`LerpUnclamped`), bộ ease Penner (khoảng 30 hàm tĩnh, cùng công thức DOTween/PrimeTween dùng), `AnimationCurve.Evaluate` |
| Vòng lặp runtime | Một `UIMotionRunner` trên PlayerLoop, dùng lại `PlayerLoopSystem/UpdateServices` (`IUpdateHandler`), lấy `Time.unscaledDeltaTime` |
| Await | UniTask, completion source có pool |
| Track Animator | API public của `Animator` |
| Preview trong Editor | `AnimationMode`: chính API mà cửa sổ Animation và Timeline của Unity dùng để preview rồi tự khôi phục |

**Mô hình dữ liệu**
- `UIMotion` (MonoBehaviour) có 2 timeline là **Show** và **Hide**. Hide có thể chọn **Mirror Show**: đảo thời gian, đảo from↔to, đảo ease.
- Mỗi timeline là một `List<UIMotionTrack>` với **serialization thường, không dùng `[SerializeReference]`**. `UIMotionTrack` là một `[Serializable] class` cụ thể gồm:
  - `kind` (enum `UIMotionTrackKind`);
  - `Object target`;
  - `startMode` + `offset`, `duration`;
  - `UIEase` hoặc `AnimationCurve`;
  - `bool useStartValue` + `Vector4 from` (start value) + `Vector4 to` (target value). Kiểu `Vector4` đủ cho float/Vector2/Vector3/Color. Xem mục "Start / Target value" bên dưới;
  - `loops` (−1 = vô hạn, chỉ dành cho effect idle);
  - `stagger` + `staggerDelay` (áp dụng lần lượt cho các con);
  - các field riêng của Animator (xem bên dưới).
  
  Custom drawer chỉ hiện các field hợp với `kind`. Lý do không dùng `SerializeReference`, vì các lỗi dữ liệu đã biết của nó:
  - đổi tên class hoặc namespace là mất dữ liệu;
  - IL2CPP có thể strip mất class;
  - prefab variant override phần tử trong list dễ lỗi;
  - lỗi "missing managed reference".
- `kind` bản đầu: Fade (CanvasGroup), Move (anchoredPosition, lối tắt hay dùng nhất), **Rect** (mọi thuộc tính layout của RectTransform, xem bên dưới), Scale, Rotate, Color (Graphic), Fill (`Image.fillAmount`), Punch, Shake, SetActive, **AnimatorState**, **Custom**.
- Giá trị **tương đối với rest pose**: Scale là hệ số nhân, Move là offset theo tỉ lệ kích thước parent. Fade, Color và Fill dùng giá trị tuyệt đối. Nhờ vậy một bộ track dùng được cho mọi kích thước.

**Start / Target value (áp dụng cho mọi track có giá trị)**
- Đây là **tuỳ chọn riêng của từng track**, không phải của cả motion hay cả timeline. Trong cùng một timeline Show hoặc Hide có thể trộn track bật và track tắt, ví dụ Fade bật (0 → Rest) cùng với Move tắt (hiện tại → Rest). Mọi quy tắc bên dưới (chụp giá trị, mirror, validator) đều xử lý theo từng track.
- Mỗi track có toggle **`useStartValue`**, mặc định **tắt**:
  - **Tắt**: chỉ có **Target value**. Start value là **giá trị hiện tại của component target** tại thời điểm track bắt đầu. Inspector **ẩn field Start value** và hiện dòng mờ "Start: giá trị hiện tại".
  - **Bật**: dùng cả **Start value** và **Target value**. Mỗi lần track bắt đầu, component bị đặt về Start value rồi mới chạy tới Target.
  - Khi tắt, giá trị `from` vẫn được serialize (chỉ ẩn đi), nên bật lại không mất số đã nhập.
- **Thời điểm chụp "giá trị hiện tại"**: lúc track **thực sự bắt đầu**, tức sau `offset`/delay và sau các track AfterPrevious trước nó, chứ không phải lúc gọi Play. Hai track nối tiếp trên cùng một thuộc tính vì thế chạy liền mạch: track sau đi tiếp từ chỗ track trước dừng. DOTween cũng chụp giá trị đầu theo cách này.
- **Value mode** (bảng mode dùng chung cho mọi track có giá trị, không riêng track `Rect`):
  - **Absolute**: dùng đúng giá trị nhập vào;
  - **RelativeToRest**: cộng offset vào giá trị lúc rest;
  - **RelativeToStart**: target = start + delta. Ví dụ "dịch thêm 100px từ chỗ đang đứng", dùng rất hợp với chế độ tắt `useStartValue`;
  - **FractionOfParent**: offset theo tỉ lệ kích thước parent.
  
  Cả Start lẫn Target đều chọn được **"Rest"**, nghĩa là dùng đúng giá trị lúc rest (vị trí thiết kế).
- **Stagger**: mỗi object con tự chụp giá trị hiện tại của riêng nó.
- **Loop**: start value chụp **một lần** khi track bắt đầu, và mọi vòng lặp dùng lại giá trị đó (không trôi dần sau mỗi vòng).
- **Kind không áp dụng**:
  - `AnimatorState` và `SetActive` ẩn cả Start lẫn Target.
  - `Punch` và `Shake` luôn dao động quanh giá trị hiện tại rồi trả về đúng giá trị đó, nên ẩn toggle và chỉ có biên độ.
  - `Custom` nhận callback `CaptureStart()` khi track bắt đầu.
- **Cách dùng khuyến nghị** (các preset mặc định làm theo, Inspector có gợi ý):
  - **Show**: bật `useStartValue` (ví dụ alpha 0, scale 0.8), với Target = Rest. Lần mở đầu tiên, component đang ở pose thiết kế. Nếu Show tắt start value thì sẽ chạy từ 1 tới 1, tức không có chuyển động. Validator **cảnh báo** trường hợp Show có track tắt start value mà Target trùng giá trị rest.
  - **Hide**: tắt `useStartValue` (đi từ chỗ hiện tại tới alpha 0 / scale 0.8). Nhờ vậy khi Hide cắt ngang Show đang chạy dở, chuyển động nối tiếp **mượt**, không bị giật về đầu.
- **Mirror Show xử lý theo từng track**:
  - Track **bật** start value: Hide đảo thành Target → Start value, đảo thời gian và đảo ease.
  - Track **tắt** start value: khi track đó chạy trong Show, runtime chụp **snapshot** giá trị lúc nó bắt đầu (runtime state trên component, không serialize, bị xoá khi `Restart`). Bản mirror sẽ chạy từ giá trị hiện tại về snapshot đó. Nếu Show chưa từng chạy (Hide được gọi khi view đang ở rest) thì đích là giá trị Rest.
  - Không cần bắt mọi track của Show phải bật start value. Validator chỉ **cảnh báo** track tắt start value được mirror, để người chỉnh biết đích của Hide phụ thuộc runtime.

**Track `Rect` (RectTransform)**
- Field `rectProperty` chọn thuộc tính cần animate:
  | `rectProperty` | Kiểu | Ghi chú |
  |---|---|---|
  | AnchoredPosition | Vector2 | Giống `Move` nhưng có đủ các value mode bên dưới |
  | AnchorMin / AnchorMax | Vector2 | Animate riêng từng góc |
  | Anchors | Vector4 (min.xy + max.zw) | Animate cả hai góc cùng lúc. Ví dụ panel trượt vào bằng anchor, thay cho `MoveAnchorAnimation` cũ |
  | Pivot | Vector2 | Thường dùng để đổi tâm scale hoặc xoay trước một hiệu ứng |
  | SizeDelta | Vector2 | Co giãn kích thước (thanh mở rộng, panel mở ra) |
  | OffsetMin / OffsetMax | Vector2 | Tương ứng Left/Bottom và Right/Top trong Inspector khi anchor dạng stretch |
- Dùng chung Start/Target value và value mode ở trên. `FractionOfParent` chỉ áp dụng cho AnchoredPosition, SizeDelta và Offset. Hiệu ứng kiểu "trượt từ ngoài vào vị trí thiết kế" đặt Start = ngoài màn hình (FractionOfParent), Target = Rest.
- **Giữ nguyên vị trí hiển thị** khi đổi pivot hoặc anchor (tuỳ chọn, bật mặc định):
  - Đổi `Pivot` thì tự bù `anchoredPosition += (pivotMới − pivotCũ) × size`, để object không nhảy chỗ.
  - Đổi `AnchorMin/Max/Anchors` thì tự bù offset để rect giữ nguyên trên màn hình, giống cách Inspector của Unity làm.
  
  Tắt tuỳ chọn này nếu muốn object dịch chuyển theo anchor (kiểu `MoveAnchorAnimation` cũ).
- **Rest pose** của RectTransform chụp đủ `anchorMin`, `anchorMax`, `pivot`, `anchoredPosition`, `sizeDelta` một lần lúc `Awake`. Reset và Snap khôi phục đủ cả 5 giá trị.
- **Hiệu năng**:
  - `anchoredPosition` chỉ làm dịch geometry.
  - `SizeDelta`, `Anchors`, `Pivot` và `Offset` **đổi kích thước rect**, nên kích hoạt `OnRectTransformDimensionsChange` và làm các con có LayoutGroup/ContentSizeFitter rebuild layout mỗi frame trong lúc track chạy.
  - Inspector hiện badge "layout cost" cho các thuộc tính này, và khuyến nghị dùng Move/Scale khi chỉ cần hiệu ứng hình ảnh.
- **Validator**:
  - **Cảnh báo** khi target bị điều khiển bởi LayoutGroup của cha, hoặc có ContentSizeFitter/AspectRatioFitter trên chính nó, vì layout sẽ ghi đè giá trị animate. Kiểm tra bằng cách tìm `ILayoutController` trên cha và trên chính target.
  - **Cảnh báo** khi có 2 track cùng ghi một thuộc tính của cùng RectTransform trong khoảng thời gian chồng nhau. Ví dụ `Move` và `Rect/AnchoredPosition`, hoặc `Anchors` và `AnchorMin`.
- **Preview** (`AnimationMode`): đăng ký property path `m_AnchoredPosition`, `m_AnchorMin`, `m_AnchorMax`, `m_Pivot`, `m_SizeDelta`, nên Unity khôi phục đủ khi tắt preview.
- **Mở rộng**: kind `Custom` tham chiếu tới một component implement `IUIMotionCustomTrack` (`Capture` / `Sample(t)` / `Snap`). Đây là tham chiếu Unity bình thường nên không có rủi ro serialization.
- **Preset**: `UIMotionPreset` (SO) chứa timeline dùng chung, ví dụ PopupIn, SlideFromBottom, ScreenPush, ToastDrop, Pulse, ButtonPunch. Track trong preset trỏ tới `Self` hoặc tới đường dẫn con. Có thể "Apply preset" (copy vào component) hoặc tham chiếu preset (dùng chung). Foundation kèm sẵn một bộ preset.

**Hành vi**
- **Trigger**:
  - **OnEnable**;
  - **OnParentShow**: nghe interface `IUIMotionTriggerSource` do cha phát ra. Interface này nằm trong asmdef Motion, nên Motion không biết UIView. Cần trigger này vì UIView ẩn bằng `Canvas.enabled`, lúc đó `OnEnable` không chạy lại;
  - **Manual**.
- **Reset**: rest pose chụp một lần lúc `Awake`, không bao giờ serialize.
  - `Restart()` trả mọi target về rest pose rồi mới play.
  - Trigger **OnEnable** luôn dùng `Restart()`, nên tắt rồi bật lại là chạy lại từ đầu.
  - `PlayShowAsync` / `PlayHideAsync` **không** tự reset, để các track tắt start value đi tiếp từ trạng thái hiện tại.
  - `OnDisable` dừng motion và tuỳ chọn trả về rest pose.
- **API**:
  - `UniTask PlayShowAsync(ct)`, `UniTask PlayHideAsync(ct)`;
  - `HideAndDeactivateAsync()` / `SetActiveAnimated(bool)`. Unity không cho trì hoãn `SetActive(false)`, nên muốn chờ Hide xong thì phải gọi API này;
  - `Snap(Show|Hide)`, `Stop()`.
- **Play khi đang play** (ví dụ Hide cắt ngang Show): lần play cũ **dừng tại chỗ** (không snap), await của nó hoàn thành với trạng thái "bị thay thế", rồi lần play mới chạy.
  - Track tắt start value đi tiếp mượt từ pose hiện tại.
  - Track bật start value nhảy về Start value, đúng như cấu hình.
- **Cancel qua `CancellationToken` mà không có lần play mới** thì snap về pose cuối của timeline đang chạy, để UI không bao giờ kẹt ở trạng thái mờ dở.
- Luôn dùng **unscaled time**. Có `speed` (UIView có `speedOverride`) và "reduce motion" (tất cả thành Instant).
- Code API nhỏ `UIMotionRunner.Tween(from, to, duration, ease, state, setter)`, dạng struct state, không closure, cho các effect viết bằng code. Binder `b.CountUp` dùng API này.
- `UIButton` punch và pulse chỉ là **preset** của `UIMotion`. Không có `UILoopEffect` riêng.

**Runtime performance (mọi platform)**
- **Một** `UIMotionRunner` tick mọi motion đang chạy trong một callback PlayerLoop.
- Motion đang chạy được giữ trong mảng (xoá bằng swap-remove). Không LINQ, không alloc mỗi lần play.
- `kind` được ánh xạ sang **evaluator tĩnh** qua bảng tra: không virtual call cho mỗi track, không boxing.
- **Schedule** (thời điểm bắt đầu tuyệt đối của mỗi track) được tính một lần lúc `Awake`, và chỉ tính lại khi dữ liệu đổi (trong Editor). Danh sách con cho stagger cũng được cache lúc `Awake`.
- Chỉ ghi giá trị khi track đang trong khoảng thời gian hoạt động, và bỏ qua nếu giá trị không đổi, để không làm canvas dirty thừa.
- Không reflection, không `dynamic`, an toàn với IL2CPP/AOT (iOS, Android, console, WebGL).

**Track `AnimatorState` (Mecanim)**

Ba yêu cầu của user: test được trong Editor, runtime tốt trên mọi platform, ổn định.
- **Dữ liệu**:
  - `Animator animator`, `int layer`;
  - `stateName` (chỉ để hiển thị) + `stateHash` được **bake trong Editor**, runtime không dùng string;
  - `bakedDuration` = độ dài clip / speed của state, **bake trong Editor** (runtime không tra được clip từ state);
  - `speed`.
- **Runtime performance**:
  - Animator trên uGUI ghi mọi property mỗi frame và làm canvas rebuild. Vì vậy track **chỉ bật `animator.enabled` trong khoảng thời gian của track**, rồi tắt ngay khi xong. Khi tắt, pose được giữ lại (`keepAnimatorStateOnDisable = true`, `writeDefaultValuesOnDisable = false`). **Hành vi này cần xác minh trên 6000.3 ở phase 4a.**
  - `updateMode = UnscaledTime`, `cullingMode = AlwaysAnimate`.
  - **Phát hiện kết thúc** bằng cách runner poll mỗi frame: `GetCurrentAnimatorStateInfo(layer)` có `shortNameHash == stateHash`, `normalizedTime ≥ 1` và `!IsInTransition(layer)`. Kết thúc thật lấy theo poll. `bakedDuration` chỉ dùng để xếp lịch cho các track AfterPrevious.
  - **Timeout** = `bakedDuration × 2 + 0.5s`: quá thời gian thì snap và log warning một lần. Nhờ vậy không bao giờ treo Hide, và UIView không bao giờ kẹt không despawn được.
  - Snap về cuối: `Play(stateHash, layer, 1f)` + `Update(0f)`. Reset về đầu: `Play(stateHash, layer, 0f)` + `Update(0f)`. Cả hai làm trong lúc Animator đang bật, sau đó mới tắt.
  - Validator khuyến nghị đặt Animator trên một **sub-canvas riêng** để cô lập rebuild.
- **Test được trong Editor**:
  - Preview và scrub dùng `AnimationMode.StartAnimationMode` + `AnimationMode.SampleAnimationClip(go, clip, time)`. Clip được lấy từ `AnimatorController` (Editor API) theo `stateHash`.
  - State là **BlendTree** hoặc không có clip thì báo "không preview được", và track vẫn chạy bình thường ở Play mode.
  - PlayMode test với một controller mẫu: kết thúc đúng lúc khi `timeScale = 0`, snap khi cancel, timeout khi state sai, và Animator bị tắt sau khi track xong.
- **Ổn định dữ liệu**:
  - `stateHash` và `bakedDuration` được re-bake ở 3 thời điểm: `OnValidate`, khi controller thay đổi (`AssetPostprocessor`), và trong `IPreprocessBuildWithReport`.
  - Validator báo **lỗi** khi: state không tồn tại trong controller, `layer` vượt số layer, hoặc Animator null.
  - Validator báo **cảnh báo** khi: dữ liệu bake đã cũ, hoặc Animator đang animate cùng property với một track khác trên cùng target trong khoảng thời gian chồng nhau (kiểm tra bằng `AnimationUtility.GetCurveBindings`).
  - **Mirror Show không áp dụng được** cho Animator track (Animator không phát ngược được nếu không có tham số speed). Timeline Show có Animator track mà Hide chọn Mirror thì validator báo lỗi và yêu cầu chỉ định state Hide riêng. Animator track không có Start/Target value: pose do clip quyết định.

**Test trong Editor (mọi loại track)**
- Inspector của `UIMotion` và `UIMotionPreset` có:
  - timeline vẽ trực tiếp (mỗi track là một thanh theo thời điểm bắt đầu và duration đã tính, màu theo `kind`);
  - nút "Add Track" (liệt kê giá trị enum);
  - kéo thả target;
  - Play Show / Play Hide / scrub / loop.
- **Preview dùng `AnimationMode`** cho mọi track: mọi lệnh ghi giá trị đi qua một helper duy nhất, helper này đăng ký `AddPropertyModification` trước khi ghi. Nhờ vậy khi tắt preview, Unity tự khôi phục. **Preview không bao giờ ghi vào scene hay prefab.**
- Tự dừng preview khi:
  - đổi selection;
  - vào Play mode;
  - trước khi reload assembly;
  - `EditorSceneManager.sceneSaving` / `PrefabStage` saving, để giá trị preview không bị lưu nhầm.
- Chỉnh trong Play mode có hiệu lực ở lần play tiếp theo (schedule bị đánh dấu dirty).

**Ổn định dữ liệu**
- Serialization thường nên không cần `[MovedFrom]`, `[Preserve]` hay `link.xml` cho track. Thêm giá trị enum mới thì chỉ **append** vào cuối và ghi giá trị số tường minh, không bao giờ đánh lại số.
- Có `dataVersion` trên `UIMotion` và `UIMotionPreset`. Khi format thay đổi thì migration chạy trong `OnValidate`/`ISerializationCallbackReceiver` (chỉ trong Editor).
- Validator bắt các lỗi:
  - `target` sai kiểu so với `kind` (ví dụ Fade nhưng target không phải `CanvasGroup`);
  - target null;
  - target nằm ngoài hierarchy của motion (tham chiếu chéo prefab);
  - `duration ≤ 0`;
  - loop vô hạn trong timeline Hide (Hide sẽ không bao giờ xong).
- Runtime chịu được target bị destroy giữa chừng: dùng Unity null-check, bỏ qua track đó và vẫn hoàn thành motion. Motion bị destroy thì tự huỷ đăng ký khỏi runner và hoàn thành task, không treo `await`.

**Đánh giá độ ổn định (nói thẳng)**
- So với DOTween (đã kiểm chứng khoảng 10 năm), code này mới viết nên giai đoạn đầu sẽ có bug.
- Bù lại, phạm vi hẹp hơn nhiều: khoảng 10 loại track cho UI, không có path hay physics. Mô hình cũng đơn giản hơn: pose là một hàm của thời gian (`Sample(t)`), không phải state machine tween. Mô hình này tránh được cả nhóm lỗi "hai tween tranh nhau một property" (loại lỗi A6/A7 của code cũ), và test được đầy đủ.
- **Rủi ro còn lại cần xác minh sớm ở phase 4a:**
  - hành vi giữ pose khi tắt Animator trên 6000.3;
  - Animator dùng chung property với track khác (validator cảnh báo).

---

## 3. Layout folder đích
```
UISystem/
  REWRITE_PLAN.md  README.md
  Logic/        Navigation/ Queue/ Layers/ Lifecycle/ Input/                 (.Logic, no engine)
  MVVM/         ViewModels/ Commands/ Navigation/                            (.MVVM, no engine)
  Testing/                                                                  (.Testing, no engine)
  Motion/       Runtime/ Tracks/ Easing/ Presets/ Editor/                     (.Motion + .Motion.Editor, độc lập với UIView)
  Data/ Core/ Core/Loading/ Views/ Binding/
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
- Ý tưởng các loại animation Fade/Scale/Move được giữ lại dưới dạng track của `UIMotion`. Sequential/Parallel được thay bằng `startMode` (mục 2.7).
- `PlayerLoopSystem/UpdateServices` (`IUpdateHandler`, `UpdateServiceManager`) cho `UIMotionRunner`.
- **Không** dùng `Utilities/ObjectPooling` (có các lỗi ở A5/A11). Một view chỉ cần một instance cache. `UIRecycleList` tự quản lý item pool của riêng nó.

---

## 5. Thứ tự triển khai (mỗi phase kết thúc bằng compile-check, và chạy test nếu phase đó có test)
0. ~~Lưu review + plan thành `UISystem/REWRITE_PLAN.md`~~ (**đã xong**: chính là file này).
1. **Logic**: asmdef `.Logic` + `UIStack`, `UIPopupQueue`, `SortOrderAllocator`, `UIViewStateMachine`, `InputLockCounter`, `BackRouter`. Viết EditMode test cho từng class: thứ tự priority, re-entrance, back khi đang lock, cấp lại sort order sau khi pop.
2. **MVVM**: cài ObservableCollections + ObservableCollections.R3 qua NuGetForUnity. Tạo asmdef `.MVVM` và `.Testing`: `UIViewModel`, command, `IUINavigator`, `FakeUINavigator`. Viết EditMode test cho vòng đời VM (activate/deactivate/dispose; subscription của lần activate trước và `Late` bị dispose hết; activate lại không bị leak), async command (drop, cancel, `CanExecute`) và `ResultViewModel`.
3. **Core runtime**:
   - Data SO, layer root theo `UIRootConfig`.
   - `UIViewBase`, `UIView<TVM>`, `UIWidget<TVM>`, `UIBinder` (one-way, two-way, command, widget).
   - `UIService : IUINavigator`, router, VM factory + `UIScope`, cache, `IUIAssetProvider` (Direct), backdrop, input lock.
   - Installer (tự register VM).
4a. **Motion** (mục 2.7). Asmdef `Motion` + `Motion.Editor`, không phụ thuộc UIView, có thể làm song song với phase 1–3:
   - `UIEase` + test đối chiếu giá trị tham chiếu. Schedule + test `startMode` → thời điểm bắt đầu.
   - `UIMotionRunner`, `UIMotionTrack` + các evaluator tĩnh (Fade/Move/Scale/Rotate/Color/Fill/Punch/Shake/SetActive/Custom).
   - Show/Hide/Mirror, trigger, `UIMotionPreset` + bộ preset mặc định.
   - `AnimatorState` track + bake + validator. **Xác minh sớm** `keepAnimatorStateOnDisable` / `writeDefaultValuesOnDisable` trên 6000.3.
   - Inspector timeline + preview bằng `AnimationMode` (dừng preview khi save, đổi selection, vào Play mode, reload assembly).
   - Track `Rect` (6 thuộc tính, bù pivot/anchor) + validator LayoutGroup và trùng thuộc tính.
   - Start/Target value theo từng track: `useStartValue`, chụp giá trị lúc track bắt đầu, 4 value mode (Absolute, RelativeToRest, RelativeToStart, FractionOfParent) + Rest, Mirror theo từng track, drawer ẩn Start khi tắt toggle.
   - PlayMode test cho các kịch bản (o)–(r4) ở mục 6.

   4b. **Components**:
   - `UIButton` (punch = preset `UIMotion`), `UISlider`, `UIToggle`, `SafeAreaFitter`, `UIParticleSortingBinder`.
   - UIView nối vào `UIMotion` (await Hide, `IUIMotionTriggerSource`).
   - `UIAnchorPlacement` + preset Hint/Tooltip, HUD `visibleOnScreens`.
   - PlayMode smoke test: open/close, CloseAll, queue, `timeScale = 0`, popup đã đóng không còn chặn raycast, hai view dùng chung một transition, và `hidesBelow`.
5. **Input + Focus**: `InputSystemBackInputSource`, `BackRouter` → `vm.HandleBack()`, focus controller. PlayMode test cho Back và focus.
6. **Addressables provider** (lease/refcount, preload, release, lowMemory) sau `USE_EXTENDED_ADDRESSABLE`.
7. **`UIRecycleList` + collection binding + `UITabGroup`**:
   - `UIRecycleList`: dọc/ngang/grid, item có kích thước cố định (bản đầu), `ScrollTo(index)`, bind `ObservableList` qua `ISynchronizedView` → `UIWidget<TItemVM>`.
   - `UITabGroup`: tab là widget, tab đang chọn là state của VM, hỗ trợ điều hướng bằng gamepad (LB/RB).
8. **Editor**: Registry window, VM type picker, validator (key, prefab ↔ VM, raycast/layout hygiene, VM không dùng `UnityEngine`) + build validator + UI Debugger.
9. **Samples + README**: Confirm, Toast, Loading, 2 screen, Inventory, Settings. Sau đó `git rm` code cũ.
10. (Tuỳ chọn) Badge/red-dot service (VM thuần, cây key → count), hook tutorial highlight (mask lỗ + chặn input ngoài vùng), UI scale setting.
11. (Tuỳ chọn) Benchmark `UIMotionRunner`: 1000 motion chạy cùng lúc trên thiết bị mobile tầm trung, đo bằng Profiler. Mục tiêu GC alloc/frame = 0, và ghi lại CPU/frame làm baseline.

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
  - (n) sau Close không còn subscription nào (đếm binding bằng UI Debugger/test hook);
  - (o) cancel `UIMotion` giữa chừng thì về đúng pose cuối. Play khi đang play thì snap lần cũ rồi chạy lại;
  - (p) tắt/bật lại object thì motion chạy lại từ frame 0. View không có `UIMotion` thì mở/đóng Instant;
  - (q) Hide được await xong mới despawn. Hide có loop vô hạn thì validator báo lỗi;
  - (r) track `AnimatorState` kết thúc đúng lúc với `timeScale = 0`, snap khi cancel, timeout khi state sai, và Animator bị tắt sau khi track xong;
  - (r2) target bị destroy giữa chừng không làm treo `await`, và motion loop (pulse/punch) dừng sạch khi hide hoặc destroy;
  - (r3) track `Rect`:
    - đổi Pivot và Anchors khi bật "giữ nguyên vị trí hiển thị" thì world corners không đổi (sai số < 0.01);
    - Reset/Snap khôi phục đủ 5 thuộc tính RectTransform;
    - Start/Target = Rest hoạt động đúng trên nhiều độ phân giải;
  - (r4) Start/Target value theo từng track:
    - track tắt `useStartValue` chụp giá trị hiện tại **lúc track bắt đầu** (sau offset và sau track trước);
    - hai track nối tiếp trên cùng thuộc tính chạy liền mạch;
    - timeline trộn track bật và tắt chạy đúng;
    - loop không trôi giá trị sau mỗi vòng;
    - Hide cắt ngang Show giữa chừng thì chuyển tiếp mượt (không giật);
    - Mirror của track tắt start value trả về đúng snapshot;
    - tắt toggle rồi bật lại vẫn giữ Start value đã nhập;
  - (s) popup có `hidesBelow` thì Canvas bên dưới bị tắt sau khi transition in xong, và bật lại trước khi transition out;
  - (t) Hint nằm trong safe area ở các góc màn hình, và mở hint mới thì hint cũ bị thay.
- **EditMode bổ sung**:
  - schedule `startMode` (WithPrevious/AfterPrevious/AtTime + offset + stagger) ra đúng thời điểm bắt đầu;
  - `UIEase` khớp giá trị tham chiếu cho mọi giá trị enum;
  - Mirror Show đảo đúng;
  - validator bắt đủ các lỗi dữ liệu ở mục 2.7.
- **Toàn vẹn dữ liệu**: preview trong Editor rồi Save scene/prefab thì **diff file phải rỗng** (preview không làm bẩn dữ liệu).
- **IL2CPP**: build Android (IL2CPP) chạy sample, mọi loại track đều hoạt động.
- **Grep**: không có `DG.Tweening` trong toàn bộ `UISystem/`. Không có `CompositeDisposable` trong code foundation. `Motion/` không `using` namespace nào của UISystem runtime.
- **Thủ công trong Editor**:
  - Chạy sample scene trên Device Simulator (safe area, notch, đổi orientation).
  - Thử gamepad/bàn phím (PC).
  - Dùng Frame Debugger và Profiler để kiểm tra batch và rebuild khi timer/currency cập nhật liên tục.
