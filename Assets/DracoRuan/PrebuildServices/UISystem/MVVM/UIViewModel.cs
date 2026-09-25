using System;
using System.Threading;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.MVVM
{
    /// <summary>
    /// Base class for every routed and non-routed view model. Created fresh (Transient) per
    /// open, bound to a view, and disposed on close. Activation subscriptions are scoped:
    /// everything added in OnActivated (or later via Late) is disposed on Deactivate, so
    /// re-activating never double-fires and never leaks the previous activation's state.
    /// </summary>
    public abstract class UIViewModel : IDisposable
    {
        private IDisposable _activation = Disposable.Empty;
        private DisposableBag _late;
        private CancellationTokenSource _activationCts;
        private bool _isDisposed;

        protected UIViewModel(IUINavigator navigator) => this.Navigator = navigator;

        protected IUINavigator Navigator { get; }

        /// <summary>Cancelled when this view model is deactivated (view closed/hidden).</summary>
        protected CancellationToken ActivationToken => this._activationCts?.Token ?? CancellationToken.None;

        internal void Activate()
        {
            this._activationCts = new CancellationTokenSource();
            DisposableBuilder builder = Disposable.CreateBuilder();
            this.OnActivated(ref builder);
            this._activation = builder.Build();
        }

        internal void Deactivate()
        {
            this._activationCts?.Cancel();
            this._activationCts?.Dispose();
            this._activationCts = null;

            this._activation.Dispose();
            this._activation = Disposable.Empty;
            this._late.Dispose();
            this._late = default;

            this.OnDeactivated();
        }

        /// <summary>Register subscriptions that live for exactly one activation.</summary>
        protected virtual void OnActivated(ref DisposableBuilder d)
        {
        }

        protected virtual void OnDeactivated()
        {
        }

        /// <summary>
        /// For subscriptions created after OnActivated already returned (e.g. once an async
        /// load completes). Disposed alongside the rest of the activation on Deactivate.
        /// </summary>
        protected ref DisposableBag Late => ref this._late;

        /// <summary>Default: Back closes this view. Override to consume Back (e.g. confirm-to-quit).</summary>
        public virtual BackResult HandleBack() => BackResult.Close;

        /// <summary>Ask the navigator to close this view model's view.</summary>
        protected void RequestClose() => _ = this.Navigator.CloseAsync(this);

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;
            this._activationCts?.Cancel();
            this._activationCts?.Dispose();
            this._activation.Dispose();
            this._late.Dispose();
            this.OnDispose();
        }

        protected virtual void OnDispose()
        {
        }
    }

    /// <summary>A view model opened with typed arguments (e.g. OpenAsync&lt;ShopViewModel&gt;(args)).</summary>
    public abstract class UIViewModel<TArgs> : UIViewModel
    {
        private TArgs _args;

        protected UIViewModel(IUINavigator navigator) : base(navigator)
        {
        }

        /// <summary>Framework hook: stash the open args before Activate() runs.</summary>
        internal void SetArgs(TArgs args) => this._args = args;

        protected sealed override void OnActivated(ref DisposableBuilder d) => this.OnActivated(this._args, ref d);

        protected abstract void OnActivated(TArgs args, ref DisposableBuilder d);
    }
}
