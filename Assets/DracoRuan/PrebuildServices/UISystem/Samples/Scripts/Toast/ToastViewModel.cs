using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.Samples.Toast
{
    /// <summary>REWRITE_PLAN.md muc 5 buoc 9 sample: a plain UIViewModel&lt;TArgs&gt; (no result)
    /// that self-closes after a fixed delay - demonstrates a Toast-preset one-shot view opened via
    /// OpenAsync&lt;ToastViewModel, ToastArgs&gt;(args), no caller-side close call needed.</summary>
    public sealed class ToastViewModel : UIViewModel<ToastArgs>
    {
        private static readonly TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(2);

        public ToastViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        public string Message { get; private set; }

        protected override void OnActivated(ToastArgs args, ref DisposableBuilder d)
        {
            this.Message = args.Message;
            this.AutoCloseAsync(this.ActivationToken).Forget();
        }

        private async UniTask AutoCloseAsync(CancellationToken ct)
        {
            await UniTask.Delay(AutoCloseDelay, cancellationToken: ct).SuppressCancellationThrow();
            if (ct.IsCancellationRequested)
                return;

            this.RequestClose();
        }
    }
}
