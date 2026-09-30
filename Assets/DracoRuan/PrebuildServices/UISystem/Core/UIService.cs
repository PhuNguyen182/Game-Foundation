using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Components;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core.Loading;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Input;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using DracoRuan.PrebuildServices.UISystem.Input;
using DracoRuan.PrebuildServices.UISystem.Logic;
using DracoRuan.PrebuildServices.UISystem.Logic.DracoRuan.PrebuildServices.UISystem.Logic.Input;
using DracoRuan.PrebuildServices.UISystem.Logic.DracoRuan.PrebuildServices.UISystem.Logic.Layers;
using DracoRuan.PrebuildServices.UISystem.Logic.DracoRuan.PrebuildServices.UISystem.Logic.Navigation;
using DracoRuan.PrebuildServices.UISystem.Logic.DracoRuan.PrebuildServices.UISystem.Logic.Queue;
using DracoRuan.PrebuildServices.UISystem.Motion;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.MVVM.DracoRuan.PrebuildServices.UISystem.MVVM;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using UIMotion = DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion.UIMotion;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Root implementation of IUINavigator. Owns the layer roots, the screen/popup stacks, the
    /// per-layer sort order allocators, the KeepAlive instance cache, and scope resolution.
    /// Show/Hide plays each view's own UIMotion when present (see PlayShowTransitionAsync/
    /// PlayHideTransitionAsync), Instant otherwise. Back input (HandleBackInput) is driven by
    /// whatever IUIBackInputSource the game wires up (e.g. InputSystemBackInputSource, Phase 5);
    /// this class only owns the BackRouter and the System/Popup/Screen tier logic.
    /// </summary>
    public sealed class UIService : IUINavigator, IAsyncInitializable, IDisposable
    {
        private readonly UIRootConfig _rootConfig;
        private readonly IUIAssetProvider _assetProvider;
        private readonly Transform _rootTransform;
        private readonly InputLockCounter _inputLock = new();
        private readonly BackRouter _backRouter = new();
        private bool _isInitialized;

        private readonly Dictionary<string, UILayerRoot> _layerRoots = new();
        private readonly UIStack<ViewInstance> _screenStack = new();
        private readonly Dictionary<string, List<ViewInstance>> _popupsByLayer = new();
        private readonly Dictionary<string, UIPopupQueue<Func<UniTask>>> _queueByLayer = new();
        private readonly Dictionary<Type, ViewInstance> _openByType = new();
        private readonly Dictionary<Type, UIViewBase> _keepAliveCache = new();
        private readonly List<UIScope> _scopes = new();

        private EventSystem _eventSystem;
        private UICameraController _cameraController;
        private UIRenderMode _effectiveRenderMode;

        private readonly UIRegistry _rootRegistry;
        private readonly IObjectResolver _rootResolver;

        /// <summary>
        /// Debug-only discovery hook (REWRITE_PLAN.md mục 5 bước 8, "UI Debugger"): the most
        /// recently constructed UIService, so an EditorWindow - which VContainer never injects
        /// into - has something to read. UIService is registered as a Singleton, so in a normal
        /// game there is exactly one; a test or sample that constructs several still gets a
        /// sane answer (whichever is newest), which is all a debug view needs. Not used by any
        /// runtime logic in this class itself.
        /// </summary>
        public static UIService Current { get; private set; }

        public UIService(UIRootConfig rootConfig, UIRegistry rootRegistry, IObjectResolver rootResolver,
            IUIAssetProvider assetProvider)
        {
            this._rootConfig = rootConfig;
            this._rootRegistry = rootRegistry;
            this._rootResolver = rootResolver;
            this._assetProvider = assetProvider;

            Current = this;

            var rootGo = new GameObject("UISystem");
            UnityEngine.Object.DontDestroyOnLoad(rootGo);
            this._rootTransform = rootGo.transform;

            this.SetupCamera();
            this.SetupEventSystem();

            SceneManager.sceneLoaded += this.OnSceneLoaded;
            SceneManager.activeSceneChanged += this.OnActiveSceneChanged;

            foreach (UILayerDefinition layerDefinition in rootConfig.Layers)
            {
                UILayerRoot layerRoot = this.BuildLayerRoot(layerDefinition);
                this._layerRoots[layerDefinition.LayerName] = layerRoot;
                this._popupsByLayer[layerDefinition.LayerName] = new List<ViewInstance>();
                this._queueByLayer[layerDefinition.LayerName] = new UIPopupQueue<Func<UniTask>>();
            }

            // REWRITE_PLAN.md 2.5: "BackRouter duyệt từ layer cao xuống: System > Popup >
            // Screen". Ruling (plan lists 3 tiers, UILayerKind has 6 values): Tutorial joins the
            // System tier (both are "always on top, highest priority" layers); Overlay (HUD)
            // and Toast never participate - HUD is non-modal by definition and Toast doesn't
            // accept input, so neither should be able to swallow a Back press.
            this._backRouter.RegisterLayer(() => this.HandleBackForKinds(UILayerKind.System, UILayerKind.Tutorial));
            this._backRouter.RegisterLayer(() => this.HandleBackForKinds(UILayerKind.Popup));
            this._backRouter.RegisterLayer(this.HandleBackForScreenStack);
            this._backRouter.BackAtRoot += () => this.BackAtRoot?.Invoke();

            Application.lowMemory += this.ReleaseHiddenKeepAliveViews;

            this.PreloadAsync().Forget();
        }

        /// <summary>
        /// REWRITE_PLAN.md 2.5: "Application.lowMemory → release view KeepAlive đang ẩn".
        /// Destroys every currently-hidden (not open) KeepAlive-cached instance and releases its
        /// prefab through the asset provider - an open KeepAlive view is left alone (its user is
        /// looking at it right now). A closed one simply reacquires on its next Open, same as a
        /// Destroy-policy view always does.
        /// </summary>
        private void ReleaseHiddenKeepAliveViews()
        {
            if (this._keepAliveCache.Count == 0)
                return;

            var toRelease = new List<Type>(this._keepAliveCache.Keys);
            foreach (Type vmType in toRelease)
            {
                if (!this.TryResolve(vmType, out UIRegistry _, out IObjectResolver _, out UIViewDefinition definition,
                        out UIScope _))
                    continue;

                UIViewBase cached = this._keepAliveCache[vmType];
                this._keepAliveCache.Remove(vmType);
                this._assetProvider.ReleasePrefab(definition, definition.Prefab);
                UnityEngine.Object.Destroy(cached.gameObject);
            }
        }

        /// <summary>The single EventSystem owned by UISystem (null when UIRootConfig.ManageEventSystem
        /// is off). An input adapter attaches its input module to this object.</summary>
        public EventSystem EventSystem => this._eventSystem;

        /// <summary>The always-alive UI camera controller; null in Screen Space Overlay mode.</summary>
        public UICameraController CameraController => this._cameraController;

        public bool IsInitialized() => this._isInitialized;

        /// <summary>Fired when Back passes through every layer unhandled (topmost/root
        /// screen) - REWRITE_PLAN.md: "Ở root screen thì bắn BackAtRoot", for the game to
        /// decide what "back" means globally (e.g. prompt to quit).</summary>
        public event Action BackAtRoot;

        /// <summary>
        /// Subscribes this service's Back handling to the given source's BackRequested event -
        /// optional, and not a constructor dependency, since not every game wires Back input the
        /// same way (or at all, e.g. mobile-only UI). InputSystemBackInputSource is the plan's
        /// default (REWRITE_PLAN.md 2.5) when UISYSTEM_INPUT_SYSTEM is defined; the game can
        /// supply any other IUIBackInputSource instead. Call at most once per source instance.
        /// </summary>
        public void AttachBackInputSource(IUIBackInputSource source) =>
            source.BackRequested += this.HandleBackInputInternal;

        public void DetachBackInputSource(IUIBackInputSource source) =>
            source.BackRequested -= this.HandleBackInputInternal;

        private IUIFocusHandler _focusHandler;

        /// <summary>
        /// Optional (Phase 5 debt closed): a game that wants PC/Console focus behavior attaches
        /// a UIFocusController here. Unset, every focus call below is a no-op - mobile-only UI
        /// never pays for it. Same optional-dependency shape as AttachBackInputSource, for the
        /// same reason: UIService's own asmdef isn't gated on UISYSTEM_INPUT_SYSTEM, only
        /// UIFocusController's concrete implementation is.
        /// </summary>
        public void AttachFocusHandler(IUIFocusHandler handler) => this._focusHandler = handler;

        public void DetachFocusHandler(IUIFocusHandler handler)
        {
            if (this._focusHandler == handler)
                this._focusHandler = null;
        }

        /// <summary>
        /// Returns true if some layer handled the press. Swallowed entirely while a transition
        /// is in flight or input is otherwise locked (REWRITE_PLAN.md: "Khi đang transition hoặc
        /// lock thì Back bị nuốt"). Exposed publicly too, for a game that wants to call it
        /// directly instead of going through an IUIBackInputSource.
        /// </summary>
        public bool HandleBackInput() => this._backRouter.TryHandleBack(this._inputLock.IsLocked);

        private void HandleBackInputInternal() => this.HandleBackInput();

        private UIBackResult HandleBackForKinds(params UILayerKind[] kinds)
        {
            ViewInstance topmost = null;
            foreach (KeyValuePair<string, List<ViewInstance>> pair in this._popupsByLayer)
            {
                if (Array.IndexOf(kinds, this._layerRoots[pair.Key].Definition.Kind) < 0)
                    continue;

                foreach (ViewInstance instance in pair.Value)
                {
                    if (topmost == null || instance.SortOrder > topmost.SortOrder)
                        topmost = instance;
                }
            }

            return topmost == null ? UIBackResult.PassThrough : this.DispatchHandleBack(topmost);
        }

        private UIBackResult HandleBackForScreenStack()
        {
            if (!this._screenStack.TryPeek(out ViewInstance topScreen))
                return UIBackResult.PassThrough;

            return this.DispatchHandleBack(topScreen);
        }

        private UIBackResult DispatchHandleBack(ViewInstance instance)
        {
            BackResult result = instance.ViewModel.HandleBack();
            if (result == BackResult.Close)
            {
                this.CloseInternalAsync(instance).Forget();
                return UIBackResult.Close;
            }

            return UIBackResult.Consume;
        }

        private async UniTask PreloadAsync()
        {
            foreach (UIViewDefinition definition in this._rootRegistry.All)
            {
                if (!definition.Preload || definition.CachePolicy != UICachePolicy.KeepAlive)
                    continue;

                (UIViewBase view, bool _) = await this.AcquireInstanceAsync(definition, CancellationToken.None);
                view.OnCreated(); // preload bypasses OpenCoreAsync, so this is the only place it can fire
                ApplyHide(definition, view, view.gameObject);
                this._keepAliveCache[definition.ViewModelType] = view;
            }

            this._isInitialized = true;
        }

        // ---------------------------------------------------------------
        // Camera + EventSystem
        // ---------------------------------------------------------------

        private void SetupCamera()
        {
            this._effectiveRenderMode = this._rootConfig.RenderMode;
            if (this._effectiveRenderMode != UIRenderMode.ScreenSpaceCamera)
                return;

            IUICameraStacker stacker = UICameraStackerProvider.Resolve();
            if (stacker == null)
            {
                Debug.LogWarning("[UISystem] No camera stacker supports the active render pipeline " +
                                 "(e.g. HDRP has no camera stacking); falling back to Screen Space - Overlay.");
                this._effectiveRenderMode = UIRenderMode.ScreenSpaceOverlay;
                return;
            }

            Camera camera = this._rootConfig.UICameraPrefab
                ? UnityEngine.Object.Instantiate(this._rootConfig.UICameraPrefab, this._rootTransform)
                : CreateDefaultUICamera(this._rootTransform);
            camera.gameObject.name = "UICamera";

            this._cameraController = new UICameraController(camera, stacker);
            this._cameraController.ResolveFallback();
        }

        private static Camera CreateDefaultUICamera(Transform parent)
        {
            var go = new GameObject("UICamera", typeof(Camera));
            go.transform.SetParent(parent, false);

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            camera.clearFlags = CameraClearFlags.Depth;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            return camera;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this._cameraController?.ResolveFallback();
            this.EnforceSingleEventSystem();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next) => this._cameraController?.ResolveFallback();

        private void SetupEventSystem()
        {
            if (!this._rootConfig.ManageEventSystem)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(this._rootTransform, false);
            this._eventSystem = go.GetComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            // Only when the legacy input manager is active; Input System projects get their
            // module from the UISystem.InputSystem adapter (AddUIInputSystem).
            go.AddComponent<StandaloneInputModule>();
#endif
            this.EnforceSingleEventSystem();
        }

        /// <summary>Disables every EventSystem that is not the one UISystem owns, so scenes
        /// (including additive ones) never leave two active.</summary>
        internal void EnforceSingleEventSystem()
        {
            if (!this._eventSystem)
                return;

            foreach (EventSystem other in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (other == this._eventSystem)
                    continue;

                Debug.LogWarning($"[UISystem] Disabling scene EventSystem {other.name}: UISystem owns the only EventSystem.", other);
                other.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------
        // Layer roots
        // ---------------------------------------------------------------

        private UILayerRoot BuildLayerRoot(UILayerDefinition definition)
        {
            var go = new GameObject($"Layer_{definition.LayerName}", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(this._rootTransform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = go.GetComponent<Canvas>();
            canvas.pixelPerfect = this._rootConfig.PixelPerfect;
            if (this._effectiveRenderMode == UIRenderMode.ScreenSpaceCamera)
            {
                // The camera goes first: a ScreenSpaceCamera canvas without one reads back as Overlay.
                canvas.worldCamera = this._cameraController.Camera;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = this._rootConfig.PlaneDistance;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            canvas.sortingOrder = definition.BaseSortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = this._rootConfig.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = this._rootConfig.MatchWidthOrHeight;

            GameObject backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(go.transform, false);
            var backdropRect = (RectTransform)backdropGo.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImage = backdropGo.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.6f);
            var backdropCanvas = backdropGo.AddComponent<Canvas>();
            backdropCanvas.overrideSorting = true;
            backdropGo.AddComponent<GraphicRaycaster>();
            backdropGo.SetActive(false);

            RectTransform contentRect = rect;
            if (definition.ApplySafeArea)
            {
                var safeAreaGo = new GameObject("SafeArea", typeof(RectTransform));
                safeAreaGo.transform.SetParent(go.transform, false);
                contentRect = (RectTransform)safeAreaGo.transform;
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;
                safeAreaGo.AddComponent<SafeAreaFitter>();
            }

            return new UILayerRoot
            {
                Definition = definition,
                Canvas = canvas,
                RectTransform = rect,
                ContentRectTransform = contentRect,
                SortOrderAllocator = new SortOrderAllocator(1, Math.Max(1, definition.SortStep)),
                BackdropGameObject = backdropGo,
                BackdropCanvas = backdropCanvas,
                BackdropButton = backdropGo.GetComponent<Button>(),
            };
        }

        // ---------------------------------------------------------------
        // Scopes
        // ---------------------------------------------------------------

        public UIScope RegisterScope(UIRegistry registry, IObjectResolver resolver,
            UIScopeOverrides overrides = null, Camera baseCamera = null)
        {
            var scope = new UIScope(this, registry, resolver, overrides, baseCamera);
            this._scopes.Add(scope);
            if (overrides)
                this.ApplyScalerSettings();

            return scope;
        }

        /// <summary>Pushes the winning scaler settings (newest scope with overrides, else the
        /// root config) onto every layer's CanvasScaler.</summary>
        internal void ApplyScalerSettings()
        {
            Vector2 resolution = this._rootConfig.ReferenceResolution;
            float match = this._rootConfig.MatchWidthOrHeight;

            for (int i = this._scopes.Count - 1; i >= 0; i--)
            {
                UIScopeOverrides overrides = this._scopes[i].Overrides;
                if (!overrides)
                    continue;

                resolution = overrides.ResolveReferenceResolution(resolution);
                match = overrides.ResolveMatch(match);
                break;
            }

            foreach (UILayerRoot layerRoot in this._layerRoots.Values)
            {
                var scaler = layerRoot.Canvas.GetComponent<CanvasScaler>();
                scaler.referenceResolution = resolution;
                scaler.matchWidthOrHeight = match;
            }
        }

        internal void UnregisterScope(UIScope scope)
        {
            this._scopes.Remove(scope);
            if (scope.Overrides)
                this.ApplyScalerSettings();

            // Force-close (no transition) any views opened under this scope.
            var toClose = new List<ViewInstance>();
            foreach (ViewInstance instance in this._openByType.Values)
            {
                if (instance.OwningScope == scope)
                    toClose.Add(instance);
            }

            foreach (ViewInstance instance in toClose)
                this.ForceCloseImmediate(instance);
        }

        private bool TryResolve(Type viewModelType, out UIRegistry registry, out IObjectResolver resolver,
            out UIViewDefinition definition, out UIScope owningScope)
        {
            for (int i = this._scopes.Count - 1; i >= 0; i--)
            {
                if (this._scopes[i].Registry.TryGet(viewModelType, out definition))
                {
                    registry = this._scopes[i].Registry;
                    resolver = this._scopes[i].Resolver;
                    owningScope = this._scopes[i];
                    return true;
                }
            }

            if (this._rootRegistry.TryGet(viewModelType, out definition))
            {
                registry = this._rootRegistry;
                resolver = this._rootResolver;
                owningScope = null;
                return true;
            }

            registry = null;
            resolver = null;
            definition = null;
            owningScope = null;
            return false;
        }

        // ---------------------------------------------------------------
        // Instance acquisition (Direct load, KeepAlive cache)
        // ---------------------------------------------------------------

        private async UniTask<(UIViewBase View, bool IsFreshInstance)> AcquireInstanceAsync(
            UIViewDefinition definition, CancellationToken ct)
        {
            if (definition.CachePolicy == UICachePolicy.KeepAlive &&
                this._keepAliveCache.Remove(definition.ViewModelType, out UIViewBase cached))
            {
                return (cached, false);
            }

            UIViewBase prefab = await this._assetProvider.LoadPrefabAsync(definition, ct);
            if (!prefab)
                throw new InvalidOperationException(
                    $"IUIAssetProvider returned a null prefab for '{definition.ViewModelType.Name}'.");

            UILayerRoot layerRoot = this._layerRoots[definition.Layer.LayerName];
            UIViewBase instance = UnityEngine.Object.Instantiate(prefab, layerRoot.ContentRectTransform, false);
            return (instance, true);
        }

        private void ReleaseOrCacheInstance(ViewInstance instance)
        {
            if (instance.Definition.CachePolicy == UICachePolicy.KeepAlive)
            {
                this.ApplyHide(instance);
                this._keepAliveCache[instance.ViewModelType] = instance.View;
            }
            else
            {
                UnityEngine.Object.Destroy(instance.GameObject);
                this._assetProvider.ReleasePrefab(instance.Definition, instance.Definition.Prefab);
            }
        }

        private void ApplyHide(ViewInstance instance) =>
            ApplyHide(instance.Definition, instance.View, instance.GameObject);

        private static void ApplyHide(UIViewDefinition definition, UIViewBase view, GameObject gameObject)
        {
            if (definition.HideMode == UIHideMode.DisableCanvas)
            {
                view.Canvas.enabled = false;
                view.GraphicRaycaster.enabled = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyShow(ViewInstance instance)
        {
            if (!instance.GameObject.activeSelf)
                instance.GameObject.SetActive(true);

            instance.View.Canvas.enabled = true;
            instance.View.GraphicRaycaster.enabled = true;
        }

        /// <summary>
        /// Re-evaluates every open HUD-preset view's UIViewDefinition.VisibleOnScreens against
        /// the current topmost screen (REWRITE_PLAN.md 2.5: "Router bật hoặc tắt HUD theo
        /// visibleOnScreens mỗi khi screen active thay đổi"). Toggles via ApplyShow/ApplyHide
        /// (hideMode, no destroy) - not a transition, since this is a passive side effect of
        /// screen navigation, not a state the HUD itself opened/closed through. Called after
        /// every screen push/pop; a HUD with no restriction (VisibleOnScreens empty) is
        /// unaffected either way, so this is a no-op for the common HUD.
        /// </summary>
        private void RefreshHudVisibility()
        {
            Type currentScreenType = this._screenStack.TryPeek(out ViewInstance topScreen)
                ? topScreen.ViewModelType
                : null;

            foreach (List<ViewInstance> layerPopups in this._popupsByLayer.Values)
            {
                foreach (ViewInstance instance in layerPopups)
                {
                    if (instance.Definition.Preset != UIViewPreset.Overlay ||
                        !instance.Definition.HasVisibleOnScreensRestriction)
                        continue;

                    bool shouldBeVisible = currentScreenType != null &&
                                           Contains(instance.Definition.VisibleOnScreens, currentScreenType);
                    if (shouldBeVisible)
                        this.ApplyShow(instance);
                    else
                        this.ApplyHide(instance);
                }
            }

            return;

            static bool Contains(IEnumerable<Type> types, Type target)
            {
                foreach (Type type in types)
                {
                    if (type == target)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Plays the view's Show transition (if it has a UIMotion) and raises ParentShown for
        /// OnParentShow children once it completes; a view with no UIMotion opens Instant
        /// (REWRITE_PLAN.md scenario (p)). ApplyShow already made the GameObject/Canvas visible
        /// before this runs, so the transition animates an already-visible view exactly as
        /// UIMotion's own "Start = current value" semantics expect.
        /// </summary>
        private async UniTask PlayShowTransitionAsync(ViewInstance instance, CancellationToken ct)
        {
            UIMotion motion = instance.View.Motion;
            if (motion)
            {
                instance.View.CanvasGroup.blocksRaycasts = false;
                await motion.PlayShowAsync(ct);
                if (instance.GameObject)
                    instance.View.CanvasGroup.blocksRaycasts = true;
            }

            instance.View.RaiseParentShown();
        }

        /// <summary>
        /// Plays the view's Hide transition (if it has a UIMotion) before the caller applies
        /// ApplyHide/despawn - REWRITE_PLAN.md: "await Hide xong mới ẩn, despawn hoặc release".
        /// A view with no UIMotion closes Instant.
        /// </summary>
        private async UniTask PlayHideTransitionAsync(ViewInstance instance, CancellationToken ct)
        {
            UIMotion motion = instance.View.Motion;
            if (!motion)
                return;

            instance.View.CanvasGroup.blocksRaycasts = false;
            await motion.PlayHideAsync(ct);
        }

        // ---------------------------------------------------------------
        // Open (screens/popups share this core)
        // ---------------------------------------------------------------

        private async UniTask<ViewInstance> OpenCoreAsync<TViewModel>(Action<TViewModel> configureBeforeActivate,
            CancellationToken ct)
            where TViewModel : UIViewModel
        {
            Type vmType = typeof(TViewModel);

            if (!this.TryResolve(vmType, out UIRegistry _, out IObjectResolver resolver,
                    out UIViewDefinition definition, out UIScope owningScope))
                throw new InvalidOperationException(
                    $"No UIViewDefinition registered for view model type '{vmType.Name}'.");

            if (this._openByType.TryGetValue(vmType, out ViewInstance existing))
            {
                switch (definition.ReopenPolicy)
                {
                    case UIReopenPolicy.Ignore:
                        return existing;
                    case UIReopenPolicy.BringToFront:
                        this.BringToFront(existing);
                        return existing;
                    case UIReopenPolicy.AllowMultiple:
                        break;
                }
            }

            this._inputLock.Acquire();
            try
            {
                (UIViewBase acquired, bool isFresh) = await this.AcquireInstanceAsync(definition, ct);

                var view = acquired as UIView<TViewModel>;
                if (!view)
                {
                    // acquired was either freshly instantiated or pulled from the KeepAlive cache
                    // above; either way it's not registered anywhere yet, so it would otherwise
                    // leak silently rather than surfacing only as a thrown exception.
                    UnityEngine.Object.Destroy(acquired.gameObject);
                    throw new InvalidOperationException(
                        $"Prefab for '{vmType.Name}' is a {acquired.GetType().Name}, not a UIView<{vmType.Name}>.");
                }

                var viewModel = (TViewModel)resolver.Resolve(vmType);
                configureBeforeActivate?.Invoke(viewModel);
                viewModel.Activate();

                var instance = new ViewInstance
                {
                    Definition = definition,
                    GameObject = view.gameObject,
                    View = view,
                    ViewModel = viewModel,
                    ViewModelType = vmType,
                    OwningScope = owningScope,
                    UnbindView = view.UnbindViewModel,
                };
                instance.StateMachine.TryBeginShow();

                if (isFresh)
                    view.OnCreated();

                this._focusHandler?.Remember();
                view.OnOpening();

                if (definition.Preset == UIViewPreset.Screen && definition.ParticipatesInStack)
                {
                    this.CloseScreenScopedPopups();
                    if (this._screenStack.TryPeek(out ViewInstance previousScreen) && definition.HidesBelow)
                    {
                        this.ApplyHide(previousScreen);
                        previousScreen.View.OnBlurred();
                    }

                    this._screenStack.Push(instance);
                    this.RefreshHudVisibility();
                }
                else
                {
                    List<ViewInstance> layerPopups = this._popupsByLayer[definition.Layer.LayerName];
                    layerPopups.Add(instance);
                }

                UILayerRoot layerRoot = this._layerRoots[definition.Layer.LayerName];
                instance.SortOrder = layerRoot.SortOrderAllocator.Allocate();
                view.Canvas.overrideSorting = true;
                view.Canvas.sortingOrder = instance.SortOrder;

                if (definition.Modal)
                    this.ShowBackdropBelow(instance);

                this.ApplyShow(instance);
                view.BindViewModel(viewModel);

                this._openByType[vmType] = instance;

                if (definition.Preset == UIViewPreset.Overlay && definition.HasVisibleOnScreensRestriction)
                    this.RefreshHudVisibility();

                await this.PlayShowTransitionAsync(instance, ct);

                instance.StateMachine.TryCompleteShow();
                view.OnOpened();

                if (this._focusHandler != null)
                {
                    this._focusHandler.Restore(view.DefaultSelectable);
                    if (definition.Modal)
                        instance.ModalFocusScope =
                            this._focusHandler.BeginModalScope(view.transform, view.DefaultSelectable);
                }

                return instance;
            }
            finally
            {
                this._inputLock.Release();
            }
        }

        private void BringToFront(ViewInstance instance)
        {
            UILayerRoot layerRoot = this._layerRoots[instance.Definition.Layer.LayerName];
            layerRoot.SortOrderAllocator.Release(instance.SortOrder);
            instance.SortOrder = layerRoot.SortOrderAllocator.Allocate();
            instance.View.Canvas.sortingOrder = instance.SortOrder;
            instance.GameObject.transform.SetAsLastSibling();

            if (instance.Definition.Modal)
                this.ShowBackdropBelow(instance);
        }

        private void ShowBackdropBelow(ViewInstance instance)
        {
            UILayerRoot layerRoot = this._layerRoots[instance.Definition.Layer.LayerName];
            layerRoot.BackdropGameObject.SetActive(true);
            layerRoot.BackdropCanvas.sortingOrder = instance.SortOrder - 1;

            // Backdrop always parents to the raw layer root (RectTransform), not
            // ContentRectTransform - it must cover the full screen even when the layer has a
            // SafeAreaFitter clipping where views themselves land (UILayerRoot.ContentRectTransform).
            layerRoot.BackdropGameObject.transform.SetParent(layerRoot.RectTransform, false);

            layerRoot.BackdropButton.onClick.RemoveAllListeners();
            if (instance.Definition.CloseOnBackdrop)
                layerRoot.BackdropButton.onClick.AddListener(() => { this.DispatchHandleBack(instance); });
        }

        private bool HasAnyModalPopupOpen()
        {
            foreach (List<ViewInstance> layerPopups in this._popupsByLayer.Values)
            {
                foreach (ViewInstance popup in layerPopups)
                {
                    if (popup.Definition.Modal)
                        return true;
                }
            }

            return false;
        }

        private void HideBackdropIfNoModalRemains(string layerName)
        {
            UILayerRoot layerRoot = this._layerRoots[layerName];
            foreach (ViewInstance popup in this._popupsByLayer[layerName])
            {
                if (popup.Definition.Modal)
                {
                    layerRoot.BackdropGameObject.SetActive(true);
                    layerRoot.BackdropCanvas.sortingOrder = popup.SortOrder - 1;
                    return;
                }
            }

            layerRoot.BackdropGameObject.SetActive(false);
        }

        private void CloseScreenScopedPopups()
        {
            foreach (KeyValuePair<string, List<ViewInstance>> pair in this._popupsByLayer)
            {
                List<ViewInstance> popups = pair.Value;
                for (int i = popups.Count - 1; i >= 0; i--)
                {
                    if (popups[i].Definition.Scope == UIViewScope.Screen)
                        this.CloseInternalAsync(popups[i]).Forget();
                }
            }
        }

        // ---------------------------------------------------------------
        // Close
        // ---------------------------------------------------------------

        private async UniTask CloseInternalAsync(ViewInstance instance)
        {
            if (!instance.StateMachine.TryBeginHide())
                return;

            CompletePendingResult(instance);

            if (instance.ModalFocusScope != null)
            {
                instance.ModalFocusScope.Dispose();
                instance.ModalFocusScope = null;
            }

            this._inputLock.Acquire();
            try
            {
                this._focusHandler?.Remember();
                instance.View.OnClosing();

                await this.PlayHideTransitionAsync(instance, default);

                if (instance.Definition.Preset == UIViewPreset.Screen && instance.Definition.ParticipatesInStack)
                {
                    this._screenStack.TryPop(out _);
                    if (this._screenStack.TryPeek(out ViewInstance below))
                    {
                        this.ApplyShow(below);
                        below.View.OnFocused();
                        await this.PlayShowTransitionAsync(below, default);
                        this._focusHandler?.Restore(below.View.DefaultSelectable);
                    }

                    this.RefreshHudVisibility();
                }
                else
                {
                    List<ViewInstance> layerPopups = this._popupsByLayer[instance.Definition.Layer.LayerName];
                    layerPopups.Remove(instance);
                    if (instance.Definition.Modal)
                    {
                        this.HideBackdropIfNoModalRemains(instance.Definition.Layer.LayerName);

                        // Restore focus to the screen below only when no other modal popup on
                        // ANY layer is still open - if one is, it already owns a ModalFocusScope
                        // of its own that would immediately fight this restore. Determining the
                        // true cross-layer topmost is more machinery than a "closed the last
                        // modal" case needs.
                        if (this._focusHandler != null && !this.HasAnyModalPopupOpen() &&
                            this._screenStack.TryPeek(out ViewInstance topScreen))
                        {
                            this._focusHandler.Restore(topScreen.View.DefaultSelectable);
                        }
                    }
                }

                UILayerRoot layerRoot = this._layerRoots[instance.Definition.Layer.LayerName];
                layerRoot.SortOrderAllocator.Release(instance.SortOrder);

                instance.UnbindView();
                instance.ViewModel.Dispose();

                this._openByType.Remove(instance.ViewModelType);

                this.ReleaseOrCacheInstance(instance);

                instance.StateMachine.TryCompleteHide();
                instance.View.OnClosed();

                await this.DrainQueueAsync(instance.Definition.Layer.LayerName);
            }
            finally
            {
                this._inputLock.Release();
            }
        }

        private void ForceCloseImmediate(ViewInstance instance)
        {
            CompletePendingResult(instance);

            if (instance.ModalFocusScope != null)
            {
                instance.ModalFocusScope.Dispose();
                instance.ModalFocusScope = null;
            }

            instance.UnbindView();
            instance.ViewModel.Dispose();
            this._openByType.Remove(instance.ViewModelType);

            if (instance.Definition.Preset == UIViewPreset.Screen && instance.Definition.ParticipatesInStack)
                this._screenStack
                    .Remove(instance); // not necessarily topmost: any screen in the stack may belong to the disposed scope
            else
                this._popupsByLayer[instance.Definition.Layer.LayerName].Remove(instance);

            UILayerRoot layerRoot = this._layerRoots[instance.Definition.Layer.LayerName];
            layerRoot.SortOrderAllocator.Release(instance.SortOrder);

            UnityEngine.Object.Destroy(instance.GameObject);
            this._assetProvider.ReleasePrefab(instance.Definition, instance.Definition.Prefab);
        }

        // ---------------------------------------------------------------
        // Queue
        // ---------------------------------------------------------------

        private async UniTask DrainQueueAsync(string layerName)
        {
            UIPopupQueue<Func<UniTask>> queue = this._queueByLayer[layerName];
            bool hasModalOpen = false;
            foreach (ViewInstance popup in this._popupsByLayer[layerName])
            {
                if (popup.Definition.Modal)
                {
                    hasModalOpen = true;
                    break;
                }
            }

            if (hasModalOpen)
                return;

            if (queue.TryDequeue(out Func<UniTask> openNext))
                await openNext();
        }

        // ---------------------------------------------------------------
        // IUINavigator
        // ---------------------------------------------------------------

        public async ValueTask OpenAsync<TViewModel>(CancellationToken ct = default) where TViewModel : UIViewModel
        {
            await this.OpenCoreAsync<TViewModel>(null, ct).AsTask();
        }

        public async ValueTask OpenAsync<TViewModel, TArgs>(TArgs args, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>
        {
            await this.OpenCoreAsync<TViewModel>(vm => vm.SetArgs(args), ct).AsTask();
        }

        public async ValueTask<TResult> OpenForResultAsync<TViewModel, TArgs, TResult>(TArgs args,
            CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult>
        {
            ViewInstance instance = await this.OpenCoreAsync<TViewModel>(vm => vm.SetArgs(args), ct).AsTask();
            return await new ValueTask<TResult>(GetOrCreateResultTask<TResult>(instance));
        }

        public async ValueTask<TResult> EnqueueAsync<TViewModel, TArgs, TResult>(TArgs args, int priority = 0,
            CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult>
        {
            Type vmType = typeof(TViewModel);
            if (!this.TryResolve(vmType, out UIRegistry _, out IObjectResolver _, out UIViewDefinition definition,
                    out UIScope _))
                throw new InvalidOperationException(
                    $"No UIViewDefinition registered for view model type '{vmType.Name}'.");

            // If it's already open (BringToFront/Ignore), reuse its pending result task instead
            // of racing a fresh one that would never be completed.
            if (this._openByType.TryGetValue(vmType, out ViewInstance alreadyOpen))
                return await new ValueTask<TResult>(GetOrCreateResultTask<TResult>(alreadyOpen));

            var tcs = new TaskCompletionSource<TResult>();
            UIPopupQueue<Func<UniTask>> queue = this._queueByLayer[definition.Layer.LayerName];

            async UniTask OpenNow()
            {
                ViewInstance instance = await this.OpenCoreAsync<TViewModel>(vm => vm.SetArgs(args), ct);
                Task<TResult> resultTask = GetOrCreateResultTask<TResult>(instance);
                TResult result = await resultTask;
                tcs.TrySetResult(result);
            }

            bool hasModalOpen = false;
            foreach (ViewInstance popup in this._popupsByLayer[definition.Layer.LayerName])
            {
                if (popup.Definition.Modal)
                {
                    hasModalOpen = true;
                    break;
                }
            }

            if (!hasModalOpen && queue.Count == 0 && !queue.IsPaused)
                OpenNow().Forget();
            else
                queue.Enqueue(OpenNow, priority);

            return await new ValueTask<TResult>(tcs.Task);
        }

        /// <summary>
        /// Returns the current pending result task for this instance, creating one (and wiring
        /// CompleteResult) if this is the first caller. A second caller hitting the same
        /// already-open instance (ReopenPolicy.BringToFront/Ignore) gets the SAME task instead
        /// of a fresh one the first caller's close would never complete.
        /// </summary>
        private static Task<TResult> GetOrCreateResultTask<TResult>(ViewInstance instance)
        {
            if (instance.PendingResultCompletionSource is TaskCompletionSource<TResult> existing)
                return existing.Task;

            var tcs = new TaskCompletionSource<TResult>();
            instance.PendingResultCompletionSource = tcs;
            instance.CompleteResult = vm =>
            {
                if (vm is IResultViewModel<TResult> resultVm && resultVm.HasResult)
                    tcs.TrySetResult(resultVm.Result);
                else
                    tcs.TrySetResult(default);
            };

            return tcs.Task;
        }

        public async ValueTask CloseAsync<TViewModel>() where TViewModel : UIViewModel
        {
            if (this._openByType.TryGetValue(typeof(TViewModel), out ViewInstance instance))
                await this.CloseAsync(instance.ViewModel);
        }

        public async ValueTask CloseAsync(UIViewModel viewModel)
        {
            if (!this._openByType.TryGetValue(viewModel.GetType(), out ViewInstance instance) ||
                instance.ViewModel != viewModel)
                return;

            await this.CloseInternalAsync(instance).AsTask();
        }

        /// <summary>
        /// Forwards the view model to CompleteResult (set by GetOrCreateResultTask, which reads
        /// HasResult/Result through IResultViewModel&lt;TResult&gt; — no reflection). Called
        /// from every close path (not just the explicit CloseAsync a Complete()'d VM triggers)
        /// so a popup closed by Back, the backdrop, CloseAll, or a disposed scope always
        /// resolves its caller's awaited result — with default(TResult) when Complete() never
        /// fired.
        /// </summary>
        private static void CompletePendingResult(ViewInstance instance)
        {
            if (instance.CompleteResult == null)
                return;

            instance.CompleteResult(instance.ViewModel);
            instance.CompleteResult = null; // guard against a double-invoke if closed again
            instance.PendingResultCompletionSource = null;
        }

        public async ValueTask PopScreenAsync()
        {
            if (this._screenStack.TryPeek(out ViewInstance top))
                await this.CloseInternalAsync(top).AsTask();
        }

        public async ValueTask PopToRootAsync()
        {
            // Close one-by-one through the normal path (not UIStack.PopToRoot() directly) so
            // each pop gets its hooks, state machine transition, KeepAlive caching, sort-order
            // release, and queue drain instead of a stripped-down duplicate of CloseInternalAsync.
            while (this._screenStack.Count > 1 && this._screenStack.TryPeek(out ViewInstance top))
                await this.CloseInternalAsync(top).AsTask();
        }

        public void Dispose()
        {
            Application.lowMemory -= this.ReleaseHiddenKeepAliveViews;
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            SceneManager.activeSceneChanged -= this.OnActiveSceneChanged;
            this._cameraController?.Dispose();

            if (Current == this)
                Current = null;

            if (this._rootTransform)
                UnityEngine.Object.Destroy(this._rootTransform.gameObject);
        }

        /// <summary>Read-only introspection for UISystem.Editor's UI Debugger window
        /// (REWRITE_PLAN.md mục 5 bước 8) - not part of IUINavigator.</summary>
        public UIServiceDebugSnapshot CaptureDebugSnapshot()
        {
            var screenStack = new List<string>();
            foreach (ViewInstance instance in this._screenStack.Items)
                screenStack.Add(instance.ViewModelType.Name);

            var popupsByLayer = new Dictionary<string, IReadOnlyList<string>>();
            foreach (KeyValuePair<string, List<ViewInstance>> pair in this._popupsByLayer)
            {
                var names = new List<string>();
                foreach (ViewInstance instance in pair.Value)
                    names.Add(instance.ViewModelType.Name);

                popupsByLayer[pair.Key] = names;
            }

            var queueCountByLayer = new Dictionary<string, int>();
            var queuePausedByLayer = new Dictionary<string, bool>();
            foreach (KeyValuePair<string, UIPopupQueue<Func<UniTask>>> pair in this._queueByLayer)
            {
                queueCountByLayer[pair.Key] = pair.Value.Count;
                queuePausedByLayer[pair.Key] = pair.Value.IsPaused;
            }

            var openViewModelTypeNames = new List<string>();
            foreach (Type vmType in this._openByType.Keys)
                openViewModelTypeNames.Add(vmType.Name);

            return new UIServiceDebugSnapshot(
                screenStack, popupsByLayer, queueCountByLayer, queuePausedByLayer,
                this._inputLock.Count, openViewModelTypeNames);
        }
    }
}
