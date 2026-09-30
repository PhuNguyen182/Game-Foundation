# UISystem Samples

Eight minimal views wired through the real router (`UIService`/`IUINavigator`), demonstrating
every navigation shape the plan calls for: a result-yielding popup, a self-closing toast, a
blocking overlay, screen-to-screen navigation, a widget-bound list, two-way binding, and a
data-driven in-game tutorial. Nothing here uses `UIMotion`/presets or Addressables (except
TutorialDemo's own mask animation) — the point is the MVVM/router wiring, not polish.

## Opening it

1. Open `SampleScene.unity`.
2. Press Play. `SampleBootstrap` builds its own VContainer scope, wires `UIService` via
   `AddUIService`, and opens `HomeScreenViewModel` as soon as the container is ready.
3. Click **Open Shop** to push a second screen; the Shop screen's **Back** button pops it via
   `IUINavigator.PopScreenAsync()`.

To reach the other samples, open them directly from a VM you write (see below) — there's no menu
wired up between them; each one exists to be read as a small, standalone example, not to be
discovered by clicking through the scene.

## What each sample shows

| Sample | Preset | Demonstrates |
|---|---|---|
| **Confirm** | Popup | `ResultViewModel<TArgs, TResult>` - open via `OpenForResultAsync<ConfirmViewModel, ConfirmArgs, bool>(args)` and await the caller's yes/no directly, no callback wiring. |
| **Toast** | Toast | A plain `UIViewModel<TArgs>` that closes itself: `AutoCloseAsync` awaits a delay against `ActivationToken` (cancelled automatically if the toast is closed early some other way) then calls `RequestClose()`. Non-modal, doesn't participate in the screen stack. |
| **Loading** | System | The simplest possible view - no bindings, no commands. `cachePolicy = KeepAlive` so the same instance is reused every time; the caller opens it before a long operation and calls `CloseAsync<LoadingViewModel>()` when done. |
| **Home / Shop** | Screen | Two plain screens demonstrating push/pop: `HomeScreenViewModel.OpenShop` calls `Navigator.OpenAsync<ShopScreenViewModel>()`; `ShopScreenViewModel.Back` calls `Navigator.PopScreenAsync()`. |
| **Inventory** | Screen | `UIWidget<TVM>` for list items, without any collection-binding infrastructure (`UIRecycleList` is out of scope for this rewrite - see `REWRITE_PLAN.md`'s "Việc còn mở"). The VM exposes a plain `IReadOnlyList<InventoryItemViewModel>`; the view binds three pre-placed widget slots one by one via `binder.Widget(...)`. Fine for a short, fixed list - not a substitute for a virtualized list. |
| **Settings** | Screen | Two-way binding: `ReactiveProperty<bool>` to a `Toggle` and `ReactiveProperty<float>` to a `Slider`, both via `UIBinder.TwoWay`. |
| **TutorialDemo** | Tutorial | `Tutorial/UITutorialBase` in its default data-driven mode: 2 authored `UITutorialStep`s highlight `HomeScreenView`'s `Title` then its `OpenShopButton` (`TutorialDemoRunner.SetResolveRoot` points the tutorial at whatever `HomeScreenView` instance is currently open, found via `FindObjectOfType` since there's no general "current view" lookup on `IUINavigator`). Step 1 auto-advances after a timer; step 2 waits for the "Next" button (`ClickAnywhere`), wired to `UITutorialBase.AdvanceCurrentStep()`. `TutorialDemoRunner : UITutorialBase` only overrides `OnStepMessage` to write into a dialogue box's `TMP_Text` - see it for the minimum a concrete tutorial needs. Open it with `OpenAsync<TutorialDemoViewModel>()` any time after Home is on screen. |

## How to add your own view

1. **View model**: subclass `UIViewModel` (no args), `UIViewModel<TArgs>` (needs open arguments),
   or `ResultViewModel<TArgs, TResult>` (needs to hand a result back to whoever opened it). Put
   commands/reactive state in `OnActivated` and register subscriptions on the `DisposableBuilder`
   passed in.
2. **View**: subclass `UIView<TYourViewModel>`, implement `Bind(ref UIBinder binder, TYourViewModel
   viewModel)`. Wire `UIButton`/`Toggle`/`Slider` fields to `UIBinder`'s `Command`/`TwoWay`/`BindClick`
   methods there.
3. **Prefab**: root needs `Canvas` + `GraphicRaycaster` + `CanvasGroup` + your view script (these
   three components are `[RequireComponent]`d by `UIViewBase`). Build the rest of the hierarchy
   like any other uGUI screen.
4. **`UIViewDefinition`** asset (`Assets > Create > DracoRuan/UISystem/View Definition`): set
   `viewModelType` (the VM type picker only lists `UIViewModel` subclasses - see
   `UISystem.Editor`'s `SerializableTypeRefDrawer`), `layer`, `preset`, and `prefab`.
5. Add the definition to a **`UIViewCollection`** asset, and make sure that collection reaches
   `AddUIService`/`AddUIScope` (see `SampleBootstrap.Configure` for the root-scope case).
6. Run **Tools > DracoRuan > UISystem > Registry Window** to catch a bad key, a prefab/VM
   mismatch, or a raycast/layout hygiene warning before you ever press Play.

## Files

- `Scripts/<SampleName>/` - one folder per sample: `<Name>ViewModel.cs`, `<Name>View.cs`, and an
  `<Name>Args.cs` where the sample takes open arguments.
- `Prefabs/<SampleName>View.prefab` - the corresponding view prefab.
- `Data/<SampleName>Definition.asset` - the `UIViewDefinition` for each sample, plus the shared
  `ScreenLayer`/`PopupLayer`/`ToastLayer`/`SystemLayer`/`TutorialLayer` (`UILayerDefinition`)
  assets, one `SampleRootConfig.asset` (`UIRootConfig`) referencing all five layers, and
  `SampleViewCollection.asset` (`UIViewCollection`) listing all eight definitions.
- `SampleScene.unity` - `SampleBootstrap` + an `EventSystem` (with `InputSystemUIInputModule`,
  since this project has the new Input System package installed).
