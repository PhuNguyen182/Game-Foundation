using DracoRuan.PrebuildServices.UISystem.MVVM;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Loading
{
    /// <summary>REWRITE_PLAN.md muc 5 buoc 9 sample: a blocking, System-preset overlay with no
    /// interactive elements. Opened via OpenAsync&lt;LoadingViewModel&gt;() while an async
    /// operation runs, closed via CloseAsync&lt;LoadingViewModel&gt;() by the caller when it
    /// finishes - KeepAlive cache policy means the same instance is reused every time.</summary>
    public sealed class LoadingViewModel : UIViewModel
    {
        public LoadingViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        protected override void OnActivated(ref DisposableBuilder d)
        {
        }
    }
}
