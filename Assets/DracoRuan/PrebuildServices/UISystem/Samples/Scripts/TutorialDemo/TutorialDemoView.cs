using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.Components;
using DracoRuan.PrebuildServices.UISystem.Samples.Home;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Samples.TutorialDemo
{
    /// <summary>
    /// System-preset view demonstrating the Tutorial system end to end (REWRITE_PLAN.md mục 5
    /// bước 10): TutorialDemoRunner highlights HomeScreenView's Title then its OpenShopButton
    /// (see TutorialDemoDefinition.asset's authored steps), each step's dialogue text set via
    /// OnStepMessage. "Next" advances any step whose advanceMode is ClickAnywhere; steps using
    /// Timer/ClickHighlight advance without it. Runs on open (OnOpened, a visuals-only hook per
    /// UIViewBase's own contract) and asks the VM to close once the last step finishes - the
    /// view itself has no navigator/close access, only the VM does (RequestClose is protected).
    /// </summary>
    public sealed class TutorialDemoView : UIView<TutorialDemoViewModel>
    {
        [SerializeField] private TutorialDemoRunner runner;
        [SerializeField] private UIButton nextButton;

        protected override void Bind(ref UIBinder binder, TutorialDemoViewModel viewModel)
        {
            binder.BindClick(this.nextButton, this.runner.AdvanceCurrentStep);
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            this.RunThenNotifyAsync().Forget();
        }

        private async UniTask RunThenNotifyAsync()
        {
            // This sample's steps highlight HomeScreenView's own children, so the tutorial
            // needs that specific instance as its resolve root - there's no general "find the
            // currently open view" API on IUINavigator (by design: game code only ever talks to
            // view models, not views), so this demo-only lookup finds it directly.
            var homeScreen = FindObjectOfType<HomeScreenView>();
            if (homeScreen != null)
                this.runner.SetResolveRoot(homeScreen.transform);

            await this.runner.RunAsync();
            this.ViewModel?.NotifyFinished();
        }
    }
}
