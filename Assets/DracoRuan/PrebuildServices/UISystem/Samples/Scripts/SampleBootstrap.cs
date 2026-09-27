using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Installer;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.Samples.Home;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.UISystem.Samples
{
    /// <summary>
    /// Minimal VContainer bootstrap for SampleScene.unity (REWRITE_PLAN.md mục 5 bước 9): wires
    /// AddUIService with the sample UIRootConfig/UIViewCollection, then opens HomeScreenViewModel
    /// once the service finishes its own preload (UIService.IsInitialized - see
    /// Core/UIService.cs's PreloadAsync). Deliberately not built on the project's own generated
    /// [AutoInstall]/AppInitializationPipelineEntryPoint pipeline (see
    /// Assets/Scripts/Test/SampleProjectLifetimeScope.cs) - a sample should be understandable
    /// standalone, not require the rest of the game's boot sequence to read.
    /// </summary>
    public sealed class SampleBootstrap : LifetimeScope
    {
        [SerializeField] private UIRootConfig rootConfig;
        [SerializeField] private UIViewCollection viewCollection;

        /// <summary>
        /// Without this, a bare LifetimeScope with no explicit parent auto-parents to
        /// VContainerSettings' configured project-wide root scope on Awake (see
        /// LifetimeScope.GetRuntimeParent in the VContainer package source: it falls through
        /// FindParent/parentReference/GlobalOverrideParents all the way to
        /// VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance() when none of those
        /// apply) - which in this project (Assets/Resources/Initializer/ProjectLifetimeScope.prefab)
        /// currently fails to build for an unrelated reason (TimerClock/CompleteTimerIntegration
        /// has a missing registration), and that failure would silently prevent this sample's own
        /// container - and therefore HomeScreenViewModel - from ever being created. Confirmed by
        /// entering Play mode on this exact scene and finding VContainerException in the console
        /// with zero children ever added under this GameObject. Not this sample's bug to fix (the
        /// project-wide root scope is out of REWRITE_PLAN.md's scope entirely).
        ///
        /// GetRuntimeParent checks GlobalOverrideParents (LifetimeScope.EnqueueParent's backing
        /// stack) BEFORE falling through to VContainerSettings, so pushing a literal null makes
        /// this scope build as its own genuinely parent-less root instead. LifetimeScope declares
        /// Awake as `protected virtual`, so overriding it and calling base.Awake() inside the
        /// bracket - rather than relying on Unity's separate reflection-based message dispatch -
        /// is exactly the intended extension point, not a hack around it.
        /// </summary>
        protected override void Awake()
        {
            using (LifetimeScope.EnqueueParent(null))
            {
                base.Awake();
            }
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.AddUIService(this.rootConfig, this.viewCollection);
            builder.RegisterEntryPoint<OpenHomeScreenOnStart>(Lifetime.Singleton);
        }

        private sealed class OpenHomeScreenOnStart : IAsyncStartable
        {
            private readonly IUINavigator _navigator;

            public OpenHomeScreenOnStart(IUINavigator navigator) => this._navigator = navigator;

            public async UniTask StartAsync(System.Threading.CancellationToken cancellation)
            {
                await this._navigator.OpenAsync<HomeScreenViewModel>(cancellation);
            }
        }
    }
}
