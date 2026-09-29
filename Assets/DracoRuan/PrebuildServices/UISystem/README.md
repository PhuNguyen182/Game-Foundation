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
| `...UISystem.Editor` | `Editor/` | Editor | Registry window, VM type picker, validator, UI Debugger |
| `...UISystem.Tests` / `...PlayModeTests` | `Tests/Editor/`, `Tests/Runtime/` | Editor | Test tự động |

Dependencies: UniTask, VContainer, R3 (+ `R3.Unity`), Addressables (qua `USE_EXTENDED_ADDRESSABLE`), Input System (qua `UISYSTEM_INPUT_SYSTEM`), TextMeshPro. **Không** phụ thuộc thư viện tween nào.

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

## Nợ kỹ thuật đang ghi nhận

- **`UIRecycleList`** (virtualized list) hoãn vô thời hạn theo yêu cầu — không dùng cho collection binding, xem `REWRITE_PLAN.md` mục "Việc còn mở".
- Editor tooling của `UIMotion` (Inspector timeline, preview `AnimationMode`, bộ preset mặc định) chưa hoàn thiện.
- Nhiều phần runtime (router, focus, Addressables load thật) mới xác minh bằng EditMode/PlayMode test tự động, chưa qua kiểm thử thủ công đầy đủ trên thiết bị thật. Xem mục 3 của `PROGRESS.md` trước khi dùng cho production.
