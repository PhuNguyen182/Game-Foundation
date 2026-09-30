using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Core.Loading;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.MVVM.DracoRuan.PrebuildServices.UISystem.MVVM;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>
    /// Router-level PlayMode smoke tests (REWRITE_PLAN.md mục 6, scenarios (a)/(b)/(g)/(h)/(m)/
    /// (p)/(s)): UIService is constructed directly (no scene, no LifetimeScope) against a
    /// hand-built UIRootConfig/UIViewDefinition/prefab, and view models are resolved from a real
    /// plain VContainer ContainerBuilder (not a hand-rolled IObjectResolver fake) so registration
    /// semantics match what a game's own installer produces. Must run as PlayMode (not EditMode):
    /// UIService's constructor calls Object.DontDestroyOnLoad on its root GameObject, which
    /// throws InvalidOperationException outside Play mode - confirmed by running this suite
    /// under EditMode first and hitting exactly that exception on every test.
    /// </summary>
    public class UIServiceTests
    {
        private sealed class FakeViewModel : UIViewModel
        {
            public int HandleBackCallCount;
            public BackResult BackResultToReturn = BackResult.Close;

            public FakeViewModel(IUINavigator navigator) : base(navigator)
            {
            }

            public override BackResult HandleBack()
            {
                this.HandleBackCallCount++;
                return this.BackResultToReturn;
            }
        }

        private sealed class OtherFakeViewModel : UIViewModel
        {
            public OtherFakeViewModel(IUINavigator navigator) : base(navigator)
            {
            }
        }

        private sealed class FakeResultViewModel : ResultViewModel<Unit, bool>
        {
            public FakeResultViewModel(IUINavigator navigator) : base(navigator)
            {
            }

            protected override void OnActivated(Unit args, ref DisposableBuilder d)
            {
            }
        }

        public readonly struct Unit
        {
            public static readonly Unit Default = default;
        }

        private sealed class FakeView : UIView<FakeViewModel>
        {
            protected override void Bind(ref UIBinder binder, FakeViewModel viewModel)
            {
            }
        }

        private sealed class OtherFakeView : UIView<OtherFakeViewModel>
        {
            protected override void Bind(ref UIBinder binder, OtherFakeViewModel viewModel)
            {
            }
        }

        private sealed class FakeResultView : UIView<FakeResultViewModel>
        {
            protected override void Bind(ref UIBinder binder, FakeResultViewModel viewModel)
            {
            }
        }

        private readonly List<Object> _created = new List<Object>();
        private UIService _service;
        private IObjectResolver _resolver;

        [TearDown]
        public void TearDown()
        {
            this._service?.Dispose();
            this._service = null;
            this._resolver?.Dispose();
            this._resolver = null;

            foreach (Object obj in this._created)
                if (obj != null)
                    Object.DestroyImmediate(obj);

            this._created.Clear();
        }

        // -----------------------------------------------------------------
        // Fixture builders
        // -----------------------------------------------------------------

        private UILayerDefinition NewLayer(string name, UILayerKind kind, int baseSortOrder)
        {
            var layer = ScriptableObject.CreateInstance<UILayerDefinition>();
            this._created.Add(layer);

            var serialized = new SerializedObject(layer);
            serialized.FindProperty("layerName").stringValue = name;
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("baseSortOrder").intValue = baseSortOrder;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return layer;
        }

        private UIRootConfig NewRootConfig(params UILayerDefinition[] layers)
        {
            var config = ScriptableObject.CreateInstance<UIRootConfig>();
            this._created.Add(config);

            var serialized = new SerializedObject(config);
            SerializedProperty layersProperty = serialized.FindProperty("layers");
            layersProperty.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
                layersProperty.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return config;
        }

        /// <summary>Builds a "prefab" as a live scene GameObject (there's no real Prefab Asset
        /// here, only Instantiate's own source/clone distinction) and immediately deactivates
        /// it - a real UIViewDefinition.Prefab is a project asset, never an active object living
        /// in the scene, and Object.FindObjectOfType/FindObjectsOfType (used throughout this
        /// fixture to observe what UIService actually instantiated) ignore inactive objects by
        /// default, so an active source would otherwise be indistinguishable from a real clone.</summary>
        private TView NewPrefab<TView>() where TView : UIViewBase
        {
            var go = new GameObject(typeof(TView).Name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var view = go.AddComponent<TView>();
            go.SetActive(false);
            this._created.Add(go);
            return view;
        }

        private UIViewDefinition NewDefinition<TVM, TView>(UILayerDefinition layer, UIViewPreset preset,
            bool modal = false, bool participatesInStack = true, bool hidesBelow = false,
            UIReopenPolicy reopenPolicy = UIReopenPolicy.BringToFront)
            where TVM : UIViewModel
            where TView : UIViewBase
        {
            var definition = ScriptableObject.CreateInstance<UIViewDefinition>();
            this._created.Add(definition);

            var serialized = new SerializedObject(definition);
            SerializedProperty vmTypeProperty = serialized.FindProperty("viewModelType");
            vmTypeProperty.FindPropertyRelative("assemblyQualifiedName").stringValue = typeof(TVM).AssemblyQualifiedName;
            serialized.FindProperty("layer").objectReferenceValue = layer;
            serialized.FindProperty("preset").enumValueIndex = (int)preset;
            serialized.FindProperty("viewPrefab").objectReferenceValue = this.NewPrefab<TView>();
            serialized.FindProperty("modal").boolValue = modal;
            serialized.FindProperty("participatesInStack").boolValue = participatesInStack;
            serialized.FindProperty("hidesBelow").boolValue = hidesBelow;
            serialized.FindProperty("reopenPolicy").enumValueIndex = (int)reopenPolicy;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }

        /// <summary>Forwards to whatever UIService the fixture builds - registered as
        /// IUINavigator up front (VContainer needs the registration before Build()), but every
        /// fake VM only ever calls into it after NewService has finished assigning
        /// UIServiceTests._service, i.e. after the first OpenAsync the test itself issues.</summary>
        private sealed class DeferredNavigator : IUINavigator
        {
            public IUINavigator Target;

            public ValueTask OpenAsync<TViewModel>(CancellationToken ct = default) where TViewModel : UIViewModel =>
                this.Target.OpenAsync<TViewModel>(ct);

            public ValueTask OpenAsync<TViewModel, TArgs>(TArgs args, CancellationToken ct = default) where TViewModel : UIViewModel<TArgs> =>
                this.Target.OpenAsync<TViewModel, TArgs>(args, ct);

            public ValueTask<TResult> OpenForResultAsync<TViewModel, TArgs, TResult>(TArgs args, CancellationToken ct = default)
                where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult> =>
                this.Target.OpenForResultAsync<TViewModel, TArgs, TResult>(args, ct);

            public ValueTask<TResult> EnqueueAsync<TViewModel, TArgs, TResult>(TArgs args, int priority = 0, CancellationToken ct = default)
                where TViewModel : UIViewModel<TArgs>, IResultViewModel<TResult> =>
                this.Target.EnqueueAsync<TViewModel, TArgs, TResult>(args, priority, ct);

            public ValueTask CloseAsync<TViewModel>() where TViewModel : UIViewModel => this.Target.CloseAsync<TViewModel>();

            public ValueTask CloseAsync(UIViewModel viewModel) => this.Target.CloseAsync(viewModel);

            public ValueTask PopScreenAsync() => this.Target.PopScreenAsync();

            public ValueTask PopToRootAsync() => this.Target.PopToRootAsync();
        }

        /// <summary>Builds a UIService wired to a real plain VContainer container (not a scene,
        /// not a hand-rolled IObjectResolver fake) registering every VM type in `definitions` as
        /// Transient - the same registration UIServiceInstallerExtensions.AddUIService performs.
        /// Every fake VM constructor-injects IUINavigator; DeferredNavigator stands in for
        /// UIService itself so the registration can exist before UIService is constructed (it
        /// needs the built resolver as one of its own constructor arguments).</summary>
        private UIService NewService(UIRootConfig rootConfig, params UIViewDefinition[] definitions)
        {
            var navigator = new DeferredNavigator();

            var builder = new ContainerBuilder();
            builder.RegisterInstance<IUINavigator>(navigator);
            foreach (UIViewDefinition definition in definitions)
                builder.Register(definition.ViewModelType, Lifetime.Transient).AsSelf();
            this._resolver = builder.Build();

            var registry = new UIRegistry(definitions);
            this._service = new UIService(rootConfig, registry, this._resolver, new DirectUIAssetProvider());
            navigator.Target = this._service;
            return this._service;
        }

        /// <summary>ValueTask has no native UniTask/coroutine bridge; IUINavigator's API returns
        /// ValueTask/ValueTask&lt;T&gt;, so every await site here goes through the BCL's own
        /// ValueTask.AsTask() before UniTask's Task.AsUniTask()/ToCoroutine().</summary>
        private static IEnumerator ToCoroutine(ValueTask task) => task.AsTask().AsUniTask().ToCoroutine();

        private static IEnumerator ToCoroutine<T>(ValueTask<T> task) => task.AsTask().AsUniTask().ToCoroutine();

        // -----------------------------------------------------------------
        // (a) open then close: view no longer blocks raycasts once closed
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator OpenThenClose_ViewNoLongerBlocksRaycastsAfterClose()
        {
            UILayerDefinition popupLayer = this.NewLayer("Popup", UILayerKind.Popup, 1000);
            UIRootConfig rootConfig = this.NewRootConfig(popupLayer);
            UIViewDefinition definition = this.NewDefinition<FakeViewModel, FakeView>(popupLayer, UIViewPreset.Popup, modal: true);
            UIService service = this.NewService(rootConfig, definition);

            yield return ToCoroutine(service.OpenAsync<FakeViewModel>());

            var openView = Object.FindObjectOfType<FakeView>();
            Assert.IsNotNull(openView, "View should be instantiated after OpenAsync completes.");
            Assert.IsTrue(openView.CanvasGroup.blocksRaycasts, "An open, non-transitioning view should block raycasts.");

            yield return ToCoroutine(service.CloseAsync<FakeViewModel>());

            Assert.IsTrue(openView == null, "A closed Destroy-policy view's GameObject should be destroyed (Unity's overloaded null check, since Destroy() only queues the actual destruction).");
        }

        // -----------------------------------------------------------------
        // (b) BringToFront reopen: only one instance ever exists
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator OpenTwice_BringToFront_OnlyOneInstanceExists()
        {
            UILayerDefinition popupLayer = this.NewLayer("Popup", UILayerKind.Popup, 1000);
            UIRootConfig rootConfig = this.NewRootConfig(popupLayer);
            UIViewDefinition definition = this.NewDefinition<FakeViewModel, FakeView>(popupLayer, UIViewPreset.Popup, reopenPolicy: UIReopenPolicy.BringToFront);
            UIService service = this.NewService(rootConfig, definition);

            yield return ToCoroutine(service.OpenAsync<FakeViewModel>());
            yield return ToCoroutine(service.OpenAsync<FakeViewModel>());

            var instances = Object.FindObjectsOfType<FakeView>();
            Assert.AreEqual(1, instances.Length, "BringToFront must never spawn a second instance for an already-open VM type.");
        }

        // -----------------------------------------------------------------
        // (g) Back calls HandleBack on the topmost screen; passes through at root
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator BackInput_CallsHandleBackOnTopmostScreen()
        {
            UILayerDefinition screenLayer = this.NewLayer("Screen", UILayerKind.Screen, 0);
            UIRootConfig rootConfig = this.NewRootConfig(screenLayer);
            UIViewDefinition definition = this.NewDefinition<FakeViewModel, FakeView>(screenLayer, UIViewPreset.Screen);
            UIService service = this.NewService(rootConfig, definition);

            yield return ToCoroutine(service.OpenAsync<FakeViewModel>());

            var view = Object.FindObjectOfType<FakeView>();
            FakeViewModel viewModel = view.ViewModel; // captured before HandleBackInput, which
                                                       // synchronously Close()s (no UIMotion means
                                                       // CloseInternalAsync runs to completion
                                                       // before its first real await) and detaches
                                                       // it from the view (UnbindViewModel nulls
                                                       // UIView<TVM>.ViewModel).
            bool handled = service.HandleBackInput();

            Assert.IsTrue(handled, "Back should be handled (Close) by the topmost screen's VM.");
            Assert.AreEqual(1, viewModel.HandleBackCallCount);
        }

        [UnityTest]
        public IEnumerator BackInput_NoScreenOpen_FiresBackAtRoot()
        {
            UILayerDefinition screenLayer = this.NewLayer("Screen", UILayerKind.Screen, 0);
            UIRootConfig rootConfig = this.NewRootConfig(screenLayer);
            UIService service = this.NewService(rootConfig);

            bool firedBackAtRoot = false;
            service.BackAtRoot += () => firedBackAtRoot = true;

            bool handled = service.HandleBackInput();

            Assert.IsFalse(handled, "PassThrough with nothing open should report unhandled.");
            Assert.IsTrue(firedBackAtRoot);
            yield break;
        }

        // -----------------------------------------------------------------
        // (h) OpenForResultAsync resolves the caller's awaited result via Complete()
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator OpenForResultAsync_CompleteResolvesAwaitedResult()
        {
            UILayerDefinition popupLayer = this.NewLayer("Popup", UILayerKind.Popup, 1000);
            UIRootConfig rootConfig = this.NewRootConfig(popupLayer);
            UIViewDefinition definition = this.NewDefinition<FakeResultViewModel, FakeResultView>(popupLayer, UIViewPreset.Popup, modal: true);
            UIService service = this.NewService(rootConfig, definition);

            Task<bool> resultTask = service.OpenForResultAsync<FakeResultViewModel, Unit, bool>(Unit.Default).AsTask();

            yield return UniTask.WaitUntil(() => Object.FindObjectOfType<FakeResultView>() != null).ToCoroutine();
            var view = Object.FindObjectOfType<FakeResultView>();
            view.ViewModel.Complete(true);

            yield return resultTask.AsUniTask().ToCoroutine();

            Assert.IsTrue(resultTask.Result);
        }

        [UnityTest]
        public IEnumerator OpenForResultAsync_ClosedByBackInsteadOfComplete_ResolvesDefault()
        {
            UILayerDefinition popupLayer = this.NewLayer("Popup", UILayerKind.Popup, 1000);
            UIRootConfig rootConfig = this.NewRootConfig(popupLayer);
            UIViewDefinition definition = this.NewDefinition<FakeResultViewModel, FakeResultView>(popupLayer, UIViewPreset.Popup, modal: true);
            UIService service = this.NewService(rootConfig, definition);

            Task<bool> resultTask = service.OpenForResultAsync<FakeResultViewModel, Unit, bool>(Unit.Default).AsTask();
            yield return UniTask.WaitUntil(() => Object.FindObjectOfType<FakeResultView>() != null).ToCoroutine();

            yield return ToCoroutine(service.CloseAsync<FakeResultViewModel>());
            yield return resultTask.AsUniTask().ToCoroutine();

            Assert.IsFalse(resultTask.Result, "Closed without Complete() should resolve default(TResult).");
        }

        // -----------------------------------------------------------------
        // (m) disposing a UIScope force-closes views opened under it
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator ScopeDispose_ForceClosesViewsOpenedUnderIt()
        {
            UILayerDefinition popupLayer = this.NewLayer("Popup", UILayerKind.Popup, 1000);
            UIRootConfig rootConfig = this.NewRootConfig(popupLayer);
            UIService service = this.NewService(rootConfig);

            UIViewDefinition scopedDefinition = this.NewDefinition<OtherFakeViewModel, OtherFakeView>(popupLayer, UIViewPreset.Popup);
            var scopeBuilder = new ContainerBuilder();
            scopeBuilder.RegisterInstance<IUINavigator>(service);
            scopeBuilder.Register(typeof(OtherFakeViewModel), Lifetime.Transient).AsSelf();
            IObjectResolver scopeResolver = scopeBuilder.Build();

            UIScope scope = service.RegisterScope(new UIRegistry(new[] { scopedDefinition }), scopeResolver);

            yield return ToCoroutine(service.OpenAsync<OtherFakeViewModel>());
            Assert.IsNotNull(Object.FindObjectOfType<OtherFakeView>());

            scope.Dispose();
            yield return null; // ForceCloseImmediate's Object.Destroy only queues destruction

            Assert.IsNull(Object.FindObjectOfType<OtherFakeView>(), "Disposing the owning scope must force-close (destroy) its views.");
            scopeResolver.Dispose();
        }

        // -----------------------------------------------------------------
        // (p) a view with no UIMotion opens/closes Instant (no transition delay)
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator ViewWithNoUIMotion_OpensInstantly()
        {
            UILayerDefinition screenLayer = this.NewLayer("Screen", UILayerKind.Screen, 0);
            UIRootConfig rootConfig = this.NewRootConfig(screenLayer);
            UIViewDefinition definition = this.NewDefinition<FakeViewModel, FakeView>(screenLayer, UIViewPreset.Screen);
            UIService service = this.NewService(rootConfig, definition);

            yield return ToCoroutine(service.OpenAsync<FakeViewModel>());

            var view = Object.FindObjectOfType<FakeView>();
            Assert.IsTrue(view.Canvas.enabled, "No UIMotion on the prefab means Show completes without ever waiting on a transition.");
        }

        // -----------------------------------------------------------------
        // (s) hidesBelow: pushing a screen with hidesBelow disables the one below
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator PushScreenWithHidesBelow_DisablesCanvasOfScreenBelow()
        {
            UILayerDefinition screenLayer = this.NewLayer("Screen", UILayerKind.Screen, 0);
            UIRootConfig rootConfig = this.NewRootConfig(screenLayer);
            UIViewDefinition bottomDefinition = this.NewDefinition<FakeViewModel, FakeView>(screenLayer, UIViewPreset.Screen, hidesBelow: true);
            UIViewDefinition topDefinition = this.NewDefinition<OtherFakeViewModel, OtherFakeView>(screenLayer, UIViewPreset.Screen, hidesBelow: true);
            UIService service = this.NewService(rootConfig, bottomDefinition, topDefinition);

            yield return ToCoroutine(service.OpenAsync<FakeViewModel>());
            var bottomView = Object.FindObjectOfType<FakeView>();
            Assert.IsTrue(bottomView.Canvas.enabled);

            yield return ToCoroutine(service.OpenAsync<OtherFakeViewModel>());

            Assert.IsFalse(bottomView.Canvas.enabled, "hidesBelow should disable the previous screen's Canvas once the new one finishes its transition in.");

            yield return ToCoroutine(service.PopScreenAsync());

            Assert.IsTrue(bottomView.Canvas.enabled, "Popping back should re-enable the screen below before/at its own show transition.");
        }
    }
}
