using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.PrebuildServices.UISystem.Core.Loading;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Logic;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>
    /// Root implementation of IUINavigator. Owns the layer roots, the screen/popup stacks, the
    /// per-layer sort order allocators, the KeepAlive instance cache, and scope resolution.
    /// No UIMotion in this phase, so every Show/Hide is instant (state machine still runs
    /// through Showing/Hiding so later phases can hook animation in without changing callers).
    /// </summary>
    public sealed class UIService : IUINavigator, IAsyncInitializable, IDisposable
    {
        private readonly UIRootConfig _rootConfig;
        private readonly IUIAssetProvider _assetProvider;
        private readonly Transform _rootTransform;
        private readonly InputLockCounter _inputLock = new InputLockCounter();
        private bool _isInitialized;

        private readonly Dictionary<string, UILayerRoot> _layerRoots = new Dictionary<string, UILayerRoot>();
        private readonly UIStack<ViewInstance> _screenStack = new UIStack<ViewInstance>();
        private readonly Dictionary<string, List<ViewInstance>> _popupsByLayer = new Dictionary<string, List<ViewInstance>>();
        private readonly Dictionary<string, UIPopupQueue<Func<UniTask>>> _queueByLayer = new Dictionary<string, UIPopupQueue<Func<UniTask>>>();
        private readonly Dictionary<Type, ViewInstance> _openByType = new Dictionary<Type, ViewInstance>();
        private readonly Dictionary<Type, GameObject> _keepAliveCache = new Dictionary<Type, GameObject>();
        private readonly List<UIScope> _scopes = new List<UIScope>();

        private readonly UIRegistry _rootRegistry;
        private readonly IObjectResolver _rootResolver;

        public UIService(UIRootConfig rootConfig, UIRegistry rootRegistry, IObjectResolver rootResolver, IUIAssetProvider assetProvider)
        {
            this._rootConfig = rootConfig;
            this._rootRegistry = rootRegistry;
            this._rootResolver = rootResolver;
            this._assetProvider = assetProvider;

            var rootGo = new GameObject("UISystem");
            UnityEngine.Object.DontDestroyOnLoad(rootGo);
            this._rootTransform = rootGo.transform;

            foreach (UILayerDefinition layerDefinition in rootConfig.Layers)
            {
                UILayerRoot layerRoot = this.BuildLayerRoot(layerDefinition);
                this._layerRoots[layerDefinition.LayerName] = layerRoot;
                this._popupsByLayer[layerDefinition.LayerName] = new List<ViewInstance>();
                this._queueByLayer[layerDefinition.LayerName] = new UIPopupQueue<Func<UniTask>>();
            }

            this.PreloadAsync().Forget();
        }

        public bool IsInitialized() => this._isInitialized;

        private async UniTask PreloadAsync()
        {
            foreach (UIViewDefinition definition in this._rootRegistry.All)
            {
                if (!definition.Preload || definition.CachePolicy != UICachePolicy.KeepAlive)
                    continue;

                (GameObject go, bool _) = await this.AcquireInstanceAsync(definition, CancellationToken.None);
                var view = go.GetComponent<UIViewBase>();
                view.Canvas.enabled = false;
                view.GraphicRaycaster.enabled = false;
                this._keepAliveCache[definition.ViewModelType] = go;
            }

            this._isInitialized = true;
        }

        // ---------------------------------------------------------------
        // Layer roots
        // ---------------------------------------------------------------

        private UILayerRoot BuildLayerRoot(UILayerDefinition definition)
        {
            var go = new GameObject($"Layer_{definition.LayerName}", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(this._rootTransform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = this._rootConfig.RenderMode == UIRenderMode.ScreenSpaceOverlay
                ? RenderMode.ScreenSpaceOverlay
                : RenderMode.ScreenSpaceCamera;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = this._rootConfig.UICamera;
                canvas.planeDistance = this._rootConfig.PlaneDistance;
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

            return new UILayerRoot
            {
                Definition = definition,
                Canvas = canvas,
                RectTransform = rect,
                SortOrderAllocator = new SortOrderAllocator(1, Math.Max(1, definition.SortStep)),
                BackdropGameObject = backdropGo,
                BackdropCanvas = backdropCanvas,
                BackdropButton = backdropGo.GetComponent<Button>(),
            };
        }

        // ---------------------------------------------------------------
        // Scopes
        // ---------------------------------------------------------------

        public UIScope RegisterScope(UIRegistry registry, IObjectResolver resolver)
        {
            var scope = new UIScope(this, registry, resolver);
            this._scopes.Add(scope);
            return scope;
        }

        internal void UnregisterScope(UIScope scope)
        {
            this._scopes.Remove(scope);

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

        private bool TryResolve(Type viewModelType, out UIRegistry registry, out IObjectResolver resolver, out UIViewDefinition definition, out UIScope owningScope)
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

        private async UniTask<(GameObject GameObject, bool IsFreshInstance)> AcquireInstanceAsync(UIViewDefinition definition, CancellationToken ct)
        {
            if (definition.CachePolicy == UICachePolicy.KeepAlive &&
                this._keepAliveCache.TryGetValue(definition.ViewModelType, out GameObject cached))
            {
                this._keepAliveCache.Remove(definition.ViewModelType);
                return (cached, false);
            }

            GameObject prefab = await this._assetProvider.LoadPrefabAsync(definition, ct);
            if (prefab == null)
                throw new InvalidOperationException($"IUIAssetProvider returned a null prefab for '{definition.ViewModelType.Name}'.");

            UILayerRoot layerRoot = this._layerRoots[definition.Layer.LayerName];
            GameObject instance = UnityEngine.Object.Instantiate(prefab, layerRoot.RectTransform, false);
            return (instance, true);
        }

        private void ReleaseOrCacheInstance(ViewInstance instance)
        {
            if (instance.Definition.CachePolicy == UICachePolicy.KeepAlive)
            {
                this.ApplyHide(instance);
                this._keepAliveCache[instance.ViewModelType] = instance.GameObject;
            }
            else
            {
                UnityEngine.Object.Destroy(instance.GameObject);
                this._assetProvider.ReleasePrefab(instance.Definition, instance.Definition.Prefab);
            }
        }

        private void ApplyHide(ViewInstance instance)
        {
            if (instance.Definition.HideMode == UIHideMode.DisableCanvas)
            {
                instance.View.Canvas.enabled = false;
                instance.View.GraphicRaycaster.enabled = false;
            }
            else
            {
                instance.GameObject.SetActive(false);
            }
        }

        private void ApplyShow(ViewInstance instance)
        {
            if (!instance.GameObject.activeSelf)
                instance.GameObject.SetActive(true);

            instance.View.Canvas.enabled = true;
            instance.View.GraphicRaycaster.enabled = true;
        }

        // ---------------------------------------------------------------
        // Open (screens/popups share this core)
        // ---------------------------------------------------------------

        private async UniTask<ViewInstance> OpenCoreAsync<TVM>(Action<TVM> configureBeforeActivate, CancellationToken ct)
            where TVM : UIViewModel
        {
            Type vmType = typeof(TVM);

            if (!this.TryResolve(vmType, out UIRegistry _, out IObjectResolver resolver, out UIViewDefinition definition, out UIScope owningScope))
                throw new InvalidOperationException($"No UIViewDefinition registered for view model type '{vmType.Name}'.");

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
                (GameObject go, bool isFresh) = await this.AcquireInstanceAsync(definition, ct);

                var view = go.GetComponent<UIView<TVM>>();
                if (view == null)
                {
                    throw new InvalidOperationException(
                        $"Prefab for '{vmType.Name}' has no UIView<{vmType.Name}> component on its root.");
                }

                var viewModel = (TVM)resolver.Resolve(vmType);
                configureBeforeActivate?.Invoke(viewModel);
                viewModel.Activate();

                var instance = new ViewInstance
                {
                    Definition = definition,
                    GameObject = go,
                    View = view,
                    ViewModel = viewModel,
                    ViewModelType = vmType,
                    OwningScope = owningScope,
                    UnbindView = () => view.UnbindViewModel(),
                };
                instance.StateMachine.TryBeginShow();

                if (isFresh)
                    view.OnCreated();

                view.OnOpening();

                if (definition.Preset == UIViewPreset.Screen && definition.ParticipatesInStack)
                {
                    this.CloseScreenScopedPopups();
                    if (this._screenStack.TryPeek(out ViewInstance previousScreen) && definition.HidesBelow)
                        this.ApplyHide(previousScreen);
                    this._screenStack.Push(instance);
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

                instance.StateMachine.TryCompleteShow();
                view.OnOpened();

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
            layerRoot.BackdropGameObject.transform.SetParent(instance.GameObject.transform.parent, false);
            int myIndex = instance.GameObject.transform.GetSiblingIndex();
            layerRoot.BackdropGameObject.transform.SetSiblingIndex(Math.Max(0, myIndex));

            layerRoot.BackdropButton.onClick.RemoveAllListeners();
            if (instance.Definition.CloseOnBackdrop)
            {
                layerRoot.BackdropButton.onClick.AddListener(() =>
                {
                    BackResult result = instance.ViewModel.HandleBack();
                    if (result == BackResult.Close)
                        this.CloseInternalAsync(instance).Forget();
                });
            }
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

            this._inputLock.Acquire();
            try
            {
                instance.View.OnClosing();

                if (instance.Definition.Preset == UIViewPreset.Screen && instance.Definition.ParticipatesInStack)
                {
                    this._screenStack.TryPop(out _);
                    if (this._screenStack.TryPeek(out ViewInstance below))
                    {
                        this.ApplyShow(below);
                        below.View.OnFocused();
                    }
                }
                else
                {
                    List<ViewInstance> layerPopups = this._popupsByLayer[instance.Definition.Layer.LayerName];
                    layerPopups.Remove(instance);
                    if (instance.Definition.Modal)
                        this.HideBackdropIfNoModalRemains(instance.Definition.Layer.LayerName);
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
            instance.UnbindView();
            instance.ViewModel.Dispose();
            this._openByType.Remove(instance.ViewModelType);

            if (instance.Definition.Preset == UIViewPreset.Screen && instance.Definition.ParticipatesInStack)
                this._screenStack.Remove(instance); // not necessarily topmost: any screen in the stack may belong to the disposed scope
            else
                this._popupsByLayer[instance.Definition.Layer.LayerName].Remove(instance);

            UnityEngine.Object.Destroy(instance.GameObject);
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

        public async ValueTask<TResult> OpenForResultAsync<TViewModel, TArgs, TResult>(TArgs args, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult>
        {
            var tcs = new TaskCompletionSource<TResult>();
            ViewInstance instance = await this.OpenCoreAsync<TViewModel>(vm => vm.SetArgs(args), ct).AsTask();
            instance.CompleteResult = boxed => tcs.TrySetResult(boxed is TResult typed ? typed : default);

            // CompletePendingResult (called from every close path) resolves this with
            // default(TResult) if the view closes without Complete() having fired
            // (Back, backdrop, CloseAll, scope dispose).
            return await new ValueTask<TResult>(tcs.Task);
        }

        public async ValueTask<TResult> EnqueueAsync<TViewModel, TArgs, TResult>(TArgs args, int priority = 0, CancellationToken ct = default)
            where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult>
        {
            Type vmType = typeof(TViewModel);
            if (!this.TryResolve(vmType, out UIRegistry _, out IObjectResolver _, out UIViewDefinition definition, out UIScope _))
                throw new InvalidOperationException($"No UIViewDefinition registered for view model type '{vmType.Name}'.");

            var tcs = new TaskCompletionSource<TResult>();
            UIPopupQueue<Func<UniTask>> queue = this._queueByLayer[definition.Layer.LayerName];

            async UniTask OpenNow()
            {
                ViewInstance instance = await this.OpenCoreAsync<TViewModel>(vm => vm.SetArgs(args), ct);
                instance.CompleteResult = boxed => tcs.TrySetResult(boxed is TResult typed ? typed : default);
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

            if (!hasModalOpen && queue.Count == 0)
                OpenNow().Forget();
            else
                queue.Enqueue(OpenNow, priority);

            return await new ValueTask<TResult>(tcs.Task);
        }

        public async ValueTask CloseAsync<TViewModel>() where TViewModel : UIViewModel
        {
            if (this._openByType.TryGetValue(typeof(TViewModel), out ViewInstance instance))
                await this.CloseAsync(instance.ViewModel);
        }

        public async ValueTask CloseAsync(UIViewModel viewModel)
        {
            if (!this._openByType.TryGetValue(viewModel.GetType(), out ViewInstance instance) || instance.ViewModel != viewModel)
                return;

            await this.CloseInternalAsync(instance).AsTask();
        }

        /// <summary>
        /// Reads HasResult/Result off a ResultViewModel&lt;,&gt; subclass via reflection and
        /// forwards it through CompleteResult. Called from every close path (not just the
        /// explicit CloseAsync a Complete()'d VM triggers) so a popup closed by Back, the
        /// backdrop, CloseAll, or a disposed scope always resolves its caller's awaited
        /// result — with default(TResult) when Complete() never fired.
        /// </summary>
        private static void CompletePendingResult(ViewInstance instance)
        {
            if (instance.CompleteResult == null)
                return;

            Type type = instance.ViewModel.GetType();
            System.Reflection.PropertyInfo hasResultProp = type.GetProperty("HasResult");
            System.Reflection.PropertyInfo resultProp = type.GetProperty("Result");

            object result = null;
            if (hasResultProp != null && resultProp != null && hasResultProp.GetValue(instance.ViewModel) is bool hasResult && hasResult)
                result = resultProp.GetValue(instance.ViewModel);

            instance.CompleteResult(result);
            instance.CompleteResult = null; // guard against a double-invoke if closed again
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
            if (this._rootTransform != null)
                UnityEngine.Object.Destroy(this._rootTransform.gameObject);
        }
    }
}
