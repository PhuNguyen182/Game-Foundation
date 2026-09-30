using DracoRuan.PrebuildServices.UISystem.MVVM;
using DracoRuan.PrebuildServices.UISystem.MVVM.DracoRuan.PrebuildServices.UISystem.MVVM;

namespace DracoRuan.PrebuildServices.UISystem.Samples.TutorialDemo
{
    /// <summary>
    /// No state of its own - the tutorial's script lives entirely in the view's
    /// TutorialDemoRunner (a UITutorialBase, which the plan's MVVM split doesn't cover: it is
    /// itself a small, self-contained state machine over UI, closer to UIMotion than to a
    /// screen's business logic). This VM exists only so the tutorial has a router entry point
    /// (a System-preset view, opened/closed like any other) and a place for the view to report
    /// "the scripted run finished" back through, since only the VM can RequestClose().
    /// </summary>
    public sealed class TutorialDemoViewModel : UIViewModel
    {
        public TutorialDemoViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        /// <summary>Called by TutorialDemoView once TutorialDemoRunner.RunAsync() completes.</summary>
        public void NotifyFinished() => this.RequestClose();
    }
}
