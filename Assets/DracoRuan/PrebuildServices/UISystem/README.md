# UISystem

Bộ UI framework tái sử dụng cho game mid-core trở lên, chạy trên **Mobile + PC/Console**, dùng **uGUI**, theo mô hình **MVVM ViewModel-first**.

> Đang trong quá trình viết lại (`docs/uisystem-rewrite-plan`). Xem [`REWRITE_PLAN.md`](REWRITE_PLAN.md) cho kiến trúc chi tiết và lý do thiết kế, [`PROGRESS.md`](PROGRESS.md) cho trạng thái triển khai thực tế theo từng phase.

## Ý tưởng cốt lõi

Code game chỉ biết **ViewModel**, không tham chiếu MonoBehaviour/prefab:

```csharp
await navigator.OpenAsync<ShopViewModel>(new ShopArgs(playerId));
bool confirmed = await navigator.OpenForResultAsync<ConfirmViewModel, ConfirmArgs, bool>(args);
```

- **Registry** map `ViewModel Type → prefab view`. ViewModel được tạo qua VContainer (Transient), View là MonoBehaviour thuần binding.
- **Một loại View duy nhất.** Screen/Popup/HUD/Toast/Hint/System/Tutorial không phải class riêng — đó là **preset** điền sẵn giá trị mặc định cho các cờ (`hidesBelow`, `history`, `modal`, `participatesInStack`...). Mỗi cờ vẫn chỉnh riêng được.
- **Binding bằng code**, strongly-typed, không reflection:

```csharp
protected override void Bind(ref UIBinder b, ShopViewModel vm)
{
    b.Text(goldText, vm.Gold);
    b.Command(buyButton, vm.Buy);
    b.TwoWay(volumeSlider, vm.Volume);
    b.Widget(currencyBar, vm.Currency);
}
```

- **Animation** qua component `UIMotion` tự viết (không DOTween, không Unity Timeline): timeline Show/Hide chỉnh trong Inspector, mỗi track nhắm một target, chạy tuần tự hoặc song song, có track riêng cho Fade/Move/Scale/Rotate/Color/Fill/Rect/Punch/Shake/SetActive/AnimatorState/Custom.

## Cấu trúc assembly

| Assembly | Thư mục | Phụ thuộc engine | Vai trò |
|---|---|---|---|
| `...UISystem.Logic` | `Logic/` | Không | `UIStack`, `UIPopupQueue`, `SortOrderAllocator`, `UIViewStateMachine`, `InputLockCounter`, `BackRouter` — thuần C#, test EditMode nhanh |
| `...UISystem.MVVM` | `MVVM/` | Không | `UIViewModel`, `UICommand`/`AsyncUICommand`, `IUINavigator`, `IResultViewModel<T>` |
| `...UISystem.Testing` | `Testing/` | Không | `FakeUINavigator` — test ViewModel của game mà không cần scene/prefab |
| `...UISystem.Motion` | `Motion/` | Có (không phụ thuộc UISystem runtime) | `UIMotion`, track evaluator, `UIMotionRunner` |
| `...UISystem` | `Data/ Core/ Views/ Binding/ Components/ Input/ Installer/` | Có | `UIService : IUINavigator`, router, `UIView<TVM>`/`UIWidget<TVM>`, `UIBinder`, components (`UIButton`, `SafeAreaFitter`, `UIAnchorPlacement`, `UITabGroup`...) |
| `...UISystem.InputSystem` | `InputSystem/` | Có | Adapter Input System (chỉ compile khi có package): Back/Focus/Tab source, `UIInputActions`, `AddUIInputSystem` |
| `...UISystem.URP` | `URP/` | Có | Adapter URP (chỉ compile khi có package): `URPCameraStacker` |
| `...UISystem.Editor` | `Editor/` | Editor | Registry window, VM type picker, validator, UI Debugger |
| `...UISystem.Tests` / `...PlayModeTests` | `Tests/Editor/`, `Tests/Runtime/` | Editor | Test tự động |

Dependencies: UniTask, VContainer, R3 (+ `R3.Unity`), Addressables (qua `USE_EXTENDED_ADDRESSABLE`), TextMeshPro. Input System và URP là tuỳ chọn, nằm trong asmdef adapter, lõi không reference chúng. **Không** phụ thuộc thư viện tween nào.

## Bắt đầu từ đâu

- Xem **`Samples/`** (Confirm, Toast, Loading, Home/Shop, Inventory, Settings, TutorialDemo) — mỗi sample là VM + View + prefab + `UIViewDefinition` hoàn chỉnh. Chi tiết ở [`Samples/README.md`](Samples/README.md).
- Đăng ký service trong `LifetimeScope`:

```csharp
builder.AddUIService(rootConfig, collectionA, collectionB); // một hoặc nhiều UIViewCollection, gộp thành một registry
builder.AddUIScope(sceneCollection);                        // tương tự cho scope theo scene
```

Một definition xuất hiện ở nhiều collection chỉ được đăng ký một lần; hai definition khác nhau cùng ViewModel type vẫn báo lỗi khi khởi tạo.

- Cấu hình view mới qua **UI Registry Window** (`Tools/DracoRuan/UISystem/Registry Window`), chọn ViewModel type qua type picker, gán prefab và preset (Screen/Popup/HUD/...).
- Debug lúc Play mode qua **UI Debugger** (`Tools/DracoRuan/UISystem/UI Debugger`): xem stack, queue, input lock, VM đang mở.

## Camera, EventSystem và input

Thiết kế đầy đủ ở [`CAMERA_INPUT_PLAN.md`](CAMERA_INPUT_PLAN.md). Tóm tắt cách dùng:

- **Một UISystem cho cả game.** Scene chỉ đóng góp qua `AddUIScope`, có thể kèm `UIScopeOverrides` (scaler riêng cho scene portrait/landscape) và base camera: `builder.AddUIScope(overrides, baseCamera, collections)`.
- **Render mode mặc định là Overlay** (UI nét kể cả khi game hạ render scale). Bật `ScreenSpaceCamera` trong `UIRootConfig` nếu cần particle/3D trong UI; UISystem tạo một UI camera sống suốt game (hoặc dùng `uiCameraPrefab`). Gắn `UIBaseCameraBinder` lên camera gameplay của mỗi scene. URP dùng camera stacking (Overlay), Built-in dùng camera depth cao hơn, HDRP fallback về Overlay.
- **Model 3D trong Overlay:** `UIModelPreview` (RawImage + camera riêng + RenderTexture).
- **EventSystem** do UISystem tạo và giữ; EventSystem trong scene bị tắt. Tắt bằng `manageEventSystem = false` nếu game tự quản.
- **Input System:** tạo asset `UIInputActions` (kéo-thả `InputActionReference` của game), rồi `builder.AddUIInputSystem(uiInputActions, playerInput?.actions)` hoặc dùng asset `UIInputSystemInstaller` (AutoInstall). Trường để trống dùng mặc định. Action mượn từ game không bị Enable/Disable/Dispose. Tab bằng LB/RB: inject `IUITabNavigationSource` rồi `tabGroup.AttachNavigation(source)`. Game dùng Rewired hay input riêng thì tự implement `IUIBackInputSource`, `IUIFocusHandler`, `IUITabNavigationSource` và gọi `UIService.Attach...`.

## Nợ kỹ thuật đang ghi nhận

- **`UIRecycleList`** (virtualized list) hoãn vô thời hạn theo yêu cầu — không dùng cho collection binding, xem `REWRITE_PLAN.md` mục "Việc còn mở".
- Adapter UI-particle (kiểu ParticleEffectForUGUI) chưa kèm theo; `UIParticleSortingBinder` chỉ phục vụ chế độ Camera.
- Camera Screen Space - Camera mới có test tự động (EditMode/PlayMode), chưa thử thủ công trên URP với post-processing thật và render scale thấp; xem mục Verification của `CAMERA_INPUT_PLAN.md`.
- Editor tooling của `UIMotion` (Inspector, preview `AnimationMode` trong Edit mode) đang được làm.
- Nhiều phần runtime (router, focus, Addressables load thật) mới xác minh bằng EditMode/PlayMode test tự động, chưa qua kiểm thử thủ công đầy đủ trên thiết bị thật. Xem mục 3 của `PROGRESS.md` trước khi dùng cho production.
