# UISystem: camera, config theo scene, input (plan bổ sung)

> Viết ngày 2026-09-30. **Đã triển khai cả 6 bước (2026-09-30)**, xem `REWRITE_PLAN.md` mục 2.1/2.5/2.6 và `README.md` mục "Camera, EventSystem và input". Còn lại: thử thủ công trên URP thật (post-processing khác nhau giữa 2 scene, `renderScale = 0.5`) và asset có tên action tùy ý.
> Plan này bổ sung cho [REWRITE_PLAN.md](REWRITE_PLAN.md). Mục tiêu vẫn như cũ: foundation UI cho game mid-core trở lên, về sau phát hành dạng package trên **OpenUPM**.

---

## Tóm tắt phiên

Có 3 câu hỏi liên quan đến nhau:
1. Có nên để mỗi scene một UISystem riêng, với bộ canvas config riêng?
2. Truyền Input Action của từng game vào thế nào? Mỗi game có InputActionAsset khác nhau, tên map/action/binding cũng khác.
3. UI camera nên là (A) một camera sống suốt game, hay (B) lấy main camera của scene hiện tại và cache lại khi mất reference?

**Hiện trạng code (đã đọc):**
- `UIService` là Singleton ở root scope. Nó tự dựng GameObject `UISystem` (DontDestroyOnLoad) và dựng các layer canvas một lần trong constructor, lấy thông tin từ `UIRootConfig` (`Core/UIService.cs:68-78`, `BuildLayerRoot` ở `:226`).
- `builder.AddUIScope(collections)` (`Installer/UIServiceInstallerExtensions.cs:52`) đã cho mỗi scene:
  - registry riêng;
  - VM được resolve từ `LifetimeScope` của scene;
  - khi scope dispose thì các view của scope bị force-close.
- `UIRootConfig.uiCamera` (`Data/UIRootConfig.cs:22`) là field của ScriptableObject, nên **không thể tham chiếu camera nằm trong scene**. Chế độ `ScreenSpaceCamera` vì thế gần như không dùng được.
- `pixelPerfect`, `minUIScale`, `maxUIScale` được khai báo nhưng không được dùng ở đâu.
- Input:
  - UIService chỉ phụ thuộc vào `IUIBackInputSource` / `IUIFocusHandler`. Implementation theo mẫu *own-or-borrow* (nhận `InputAction` từ ngoài, hoặc tự tạo action default theo usage `*/{Cancel}`).
  - Asmdef lõi reference thẳng `Unity.InputSystem`. Code Input System nằm rải rác sau `#if UISYSTEM_INPUT_SYSTEM`, kể cả trong `Components/UITabGroup.cs`.
  - Không có chỗ nào nối dây sẵn: không code nào gọi `AttachBackInputSource` / `AttachFocusHandler`.
  - Ba API dùng ba kiểu tham số khác nhau: `InputAction`, `InputActionReference[]`, và hai `InputAction` rời.
- EventSystem nằm trong từng scene, UISystem không quản lý nó.
- Project dùng URP 17.3, Input System 1.18, Unity 6000.3.

**Người dùng đã chốt:**
- Camera: **A**, tức UI camera sống suốt game, do UISystem sở hữu.
- Render mode mặc định: **Screen Space Overlay**; chế độ Camera để opt-in.
- Pipeline: **URP hỗ trợ đầy đủ + fallback** cho Built-in/HDRP.

---

## Quyết định 1: Một UISystem cho toàn game. Scene chỉ đóng góp qua `UIScope`

Không tách UISystem theo scene, vì:
- **Chỉ có một nguồn Back input.** Hai service cùng nghe Back thì phải tự phân xử service nào nhận trước. `BackRouter` đã giải chuyện này trong một service.
- **Chỉ có một không gian sort order.** Popup/System/Toast của hai hệ canvas sẽ đè lên nhau mà không hệ nào biết hệ kia.
- **Loading, toast, confirm vẫn phải sống qua lúc chuyển scene,** nên vẫn cần một service global. Tách theo scene thì thành hai lớp service.
- **`IUINavigator` sẽ bị mơ hồ:** VM không biết đang mở view vào service nào.

Scene đóng góp:
- registry và VM riêng (đã có);
- base camera, qua `UIBaseCameraBinder` hoặc qua scope (xem Quyết định 2);
- (optional) `UIScopeOverrides` gồm `referenceResolution` / `matchWidthOrHeight`, cho scene portrait hoặc landscape. Override được áp và hoàn tác theo stack.

Danh sách **layer không được override theo scene**, vì đó là hợp đồng chung của cả game (BackRouter và sort order dựa vào nó).

## Quyết định 2: UI camera sống suốt game (A), không dùng main camera của scene (B)

Lý do không chọn B:
- **Post-processing của scene ăn vào UI.** Bloom, tonemapping ACES, color grading, DoF, motion blur, vignette đều áp lên UI, nên màu UI khác nhau giữa các scene.
- **Có khoảng hở khi chuyển scene.** Camera cũ bị destroy thì `worldCamera` thành null, Unity fallback canvas sang kiểu Overlay, sort và particle bị nhảy. Đúng lúc đó loading screen lại đang hiện.
- **"Cache khi mất reference" nghĩa là phải poll `Camera.main`,** nên phát hiện chậm. Với additive scene, cutscene hay nhiều camera thì cũng không rõ đâu là "main".
- **Cách UI render do setting của camera gameplay quyết định:** projection, FOV, clip plane, culling mask, render scale.
- Mỗi lần đổi `worldCamera` thì mọi layer canvas phải reposition và rebuild.

Thiết kế:
- `UIRootConfig`: bỏ `Camera uiCamera`, thay bằng `Camera uiCameraPrefab` (optional).
  - SO tham chiếu được prefab, nên game có thể custom renderer hay volume riêng cho UI.
  - Nếu để trống thì service tự tạo camera mặc định: orthographic, culling mask chỉ layer UI, tắt post-processing, shadow, depth texture, opaque texture.
- Canvas `worldCamera` luôn là UI camera này, nên không bao giờ null và không đổi giữa các scene.
- **Base camera tracker (URP):** UI camera có `renderType = Overlay` và được thêm vào `cameraStack` của base camera hiện tại.
  - Base camera được đăng ký tường minh bằng component `UIBaseCameraBinder` gắn lên camera của scene (OnEnable thì push, OnDisable thì pop), hoặc truyền qua `AddUIScope(..., baseCamera)`.
  - Đăng ký được giữ theo **stack**, nên cutscene hay additive scene có thể tạm đè camera rồi trả lại.
  - Fallback: nếu không có ai đăng ký thì dùng `Camera.main` **một lần** khi có `SceneManager.activeSceneChanged` / `sceneLoaded`. Không poll mỗi frame.
  - Khi không có base camera nào (khoảng hở chuyển scene), UI camera tự chuyển sang `Base` với nền solid đen, nên loading screen vẫn render. Có base camera trở lại thì chuyển về `Overlay` và gắn lại vào stack.
- **Adapter theo pipeline:** lõi chỉ biết interface `IUICameraStacker`.
  - URP: asmdef `UISystem.URP`, bật theo versionDefine `com.unity.render-pipelines.universal`.
  - Built-in: camera riêng với `depth` cao hơn, `clearFlags = Depth`.
  - HDRP không có camera stacking: fallback về Overlay, ghi rõ trong README.

## Quyết định 3: Render mode mặc định là Screen Space Overlay; chế độ Camera để opt-in

- Camera nằm trong URP stack **dùng chung render target với base camera**. Khi game hạ `renderScale` hoặc bật FSR/STP/dynamic resolution (gần như bắt buộc với mid-core trên mobile), UI cũng bị render ở độ phân giải thấp và bị mờ.
- Overlay canvas được vẽ sau bước upscale, ở độ phân giải gốc. Nó rẻ nhất và không phụ thuộc vào camera nào.
- Nhu cầu hay kéo người ta sang chế độ Camera, và cách xử lý trên Overlay:
  - particle trong UI: dùng UI particle dạng mesh (kiểu ParticleEffectForUGUI), làm adapter optional;
  - model 3D trong UI: component `UIModelPreview`, dùng camera riêng render ra RenderTexture rồi hiển thị bằng RawImage.
- `UIRenderMode.ScreenSpaceCamera` vẫn giữ và dùng thiết kế ở Quyết định 2. `UIParticleSortingBinder` vẫn dùng cho chế độ này.

## Quyết định 4: Input và EventSystem sống cùng vòng đời với UISystem

- **EventSystem do UISystem sở hữu,** đặt dưới GameObject `UISystem` (DontDestroyOnLoad). Nếu scene có sẵn EventSystem khác thì cảnh báo, hoặc tắt cái của scene. Nhờ vậy không có khoảng hở hay trùng EventSystem khi chuyển scene.
- **Tách asmdef `UISystem.InputSystem`,** bật theo versionDefine `com.unity.inputsystem`.
  - Lõi bỏ reference tới `Unity.InputSystem`, chỉ giữ các interface `IUIBackInputSource`, `IUIFocusHandler`, và interface mới `IUITabNavigationSource` (`event Action Previous, Next`).
  - `UITabGroup` bỏ `#if` và nhận `IUITabNavigationSource`.
  - Game dùng Rewired, legacy Input hay input riêng thì tự implement các interface này. Lõi không đổi.
- **Thêm SO `UIInputActions`.** Các field là `InputActionReference`: point, click, scroll, navigate, submit, cancel/back, previousTab, nextTab.
  - `InputActionReference` lưu **GUID của action**, không lưu tên. Game đặt hay đổi tên map, action, binding thì reference vẫn đúng. Designer chỉ việc kéo-thả.
  - Field nào để trống thì dùng default hiện có (theo usage, ví dụ `*/{Cancel}`, `<Gamepad>/leftShoulder`).
  - `FindAction("UI/Cancel")` theo tên chỉ làm fallback (một field string path), không phải cách chính.
  - **Cùng SO này dùng để gán cho `InputSystemUIInputModule`** (`actionsAsset` + từng action reference). Vậy là có một nguồn cấu hình input duy nhất cho UI.
- **Hỗ trợ `PlayerInput`.** PlayerInput clone asset cho từng player, nên reference trỏ vào asset gốc sẽ không nhận event. Cách xử lý: installer nhận `runtimeAsset` (ví dụ `playerInput.actions`) và resolve bằng:
  ```csharp
  InputAction Resolve(InputActionReference r, InputActionAsset runtimeAsset) =>
      runtimeAsset == null ? r.action : runtimeAsset.FindAction(r.action.id);
  ```
- **Installer:** `builder.AddUIInputSystem(config, runtimeAsset = null)` và SO `[AutoInstall]` `UIInputSystemInstaller` nằm trong asmdef adapter.
  - Installer đăng ký `InputSystemBackInputSource`, `UIFocusController`, `InputSystemTabNavigationSource`.
  - Một `IStartable` gọi `UIService.AttachBackInputSource` / `AttachFocusHandler`; khi Dispose thì Detach.
- **Ownership:**
  - Action **mượn** từ game: UISystem không Enable/Disable/Dispose. Nhờ vậy rebinding lúc chạy (`PerformInteractiveRebinding`) và việc đổi action map vẫn đúng.
  - Action **default tự tạo**: UISystem sở hữu và dọn khi Dispose.
  - Tất cả chạy theo event `performed`, không poll.

---

## Thứ tự làm (mỗi bước compile và test được riêng)

1. **Tách asmdef adapter:** `UISystem/InputSystem/` và `UISystem/URP/`. Lõi bỏ reference `Unity.InputSystem`. Chuyển `Input/InputSystemBackInputSource.cs`, `Input/UIFocusController.cs` sang adapter. Thêm `IUITabNavigationSource` và sửa `UITabGroup`.
2. **EventSystem + input:** service sở hữu EventSystem. Thêm `UIInputActions`, `Resolve` theo runtime asset, `AddUIInputSystem` / `UIInputSystemInstaller`, `InputSystemTabNavigationSource`.
3. **UI camera sống suốt game (chế độ Camera):** `uiCameraPrefab`, `IUICameraStacker` + `URPCameraStacker`, fallback cho Built-in, `UIBaseCameraBinder` theo stack, chuyển Base ↔ Overlay khi không có base camera.
4. **`UIScopeOverrides`:** override scaler theo stack. Dọn hoặc implement `pixelPerfect` / `minUIScale` / `maxUIScale`.
5. **(Sau)** `UIModelPreview` (RenderTexture) và adapter UI-particle optional.
6. **Tài liệu:** cập nhật `REWRITE_PLAN.md` (mục 2.5, B7, camera stacking) và `README.md`, kể cả phần fallback cho Built-in/HDRP.

## File dự kiến

- **Sửa:** `Core/UIService.cs`, `Core/UIScope.cs`, `Data/UIRootConfig.cs`, `Installer/UIServiceInstallerExtensions.cs`, `Components/UITabGroup.cs`, `DracoRuan.PrebuildServices.UISystem.asmdef`, asmdef test (thêm reference tới adapter).
- **Chuyển:** `Input/InputSystemBackInputSource.cs`, `Input/UIFocusController.cs` sang `InputSystem/`.
- **Mới:**
  - `InputSystem/{asmdef, UIInputActions, UIInputSystemInstaller, UIInputSystemInstallerExtensions, InputSystemTabNavigationSource}`;
  - `URP/{asmdef, URPCameraStacker}`;
  - `Core/IUICameraStacker.cs`, `Input/IUITabNavigationSource.cs`, `Components/UIBaseCameraBinder.cs`, `Data/UIScopeOverrides.cs`.
- **Lưu ý:** khi chuyển file, ghi qua scratchpad rồi `cp`, vì hook reformat của Rider có thể revert file vừa chuyển. Dùng `unity recompile --json` để kiểm tra compile.

## Verification

- `unity recompile --json` ở các tổ hợp: có/không có Input System, có/không có URP. Lõi phải compile được khi thiếu cả hai.
- EditMode test: chạy lại `InputSystemBackInputSourceTests`, `UIFocusControllerTests`, `UITabGroupTests`. Thêm test cho `Resolve`: `Instantiate` một asset để giả lập PlayerInput, rồi kiểm tra action resolve được thuộc về bản clone.
- PlayMode test (`Tests/Runtime/UIServiceTests.cs`):
  - `worldCamera` không đổi qua lần load scene;
  - UI camera chuyển Base ↔ Overlay khi base camera mất hoặc xuất hiện;
  - push/pop của binder lồng nhau khôi phục đúng thứ tự;
  - chỉ có một EventSystem sau khi load additive;
  - action mượn không bị Dispose khi scope dispose.
- Thử thủ công ở SampleScene:
  - chuyển giữa 2 scene có post-processing khác nhau, màu UI giữ nguyên;
  - đặt `renderScale = 0.5` thì UI Overlay vẫn nét;
  - dùng một asset có tên action tùy ý (ví dụ `Menu/GoBack`), nhấn Esc thì popup đóng.
