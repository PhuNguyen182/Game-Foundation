using System;
using System.Threading;
using System.Threading.Tasks;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.MVVM
{
    /// <summary>
    /// Async command bindable to a button. Re-entrant Execute() calls made while a previous
    /// invocation is still running are dropped (AwaitOperation.Drop), so double-tapping a
    /// button never starts a second concurrent execution. Disposing the command (e.g. on VM
    /// Deactivate) cancels an in-flight execution.
    /// </summary>
    public sealed class AsyncUICommand : IDisposable
    {
        private readonly Subject<Unit> _trigger = new Subject<Unit>();
        private readonly ReactiveProperty<bool> _isExecuting = new ReactiveProperty<bool>(false);
        private readonly Func<CancellationToken, ValueTask> _executeAsync;
        private readonly IDisposable _subscription;
        private readonly IDisposable _ownedCanExecute;

        public AsyncUICommand(Func<CancellationToken, ValueTask> executeAsync, Observable<bool> canExecuteSource = null)
        {
            this._executeAsync = executeAsync;

            if (canExecuteSource != null)
            {
                var readOnly = canExecuteSource.ToReadOnlyReactiveProperty(true);
                this.CanExecute = readOnly;
                this._ownedCanExecute = readOnly;
            }
            else
            {
                var constant = new ReactiveProperty<bool>(true);
                this.CanExecute = constant;
                this._ownedCanExecute = constant;
            }

            this._subscription = this._trigger.SubscribeAwait(this.RunAsync, AwaitOperation.Drop);
        }

        public ReadOnlyReactiveProperty<bool> CanExecute { get; }
        public ReadOnlyReactiveProperty<bool> IsExecuting => this._isExecuting;

        public void Execute()
        {
            if (!this.CanExecute.CurrentValue)
                return;

            this._trigger.OnNext(Unit.Default);
        }

        private async ValueTask RunAsync(Unit _, CancellationToken ct)
        {
            this._isExecuting.Value = true;
            try
            {
                await this._executeAsync(ct);
            }
            finally
            {
                this._isExecuting.Value = false;
            }
        }

        public void Dispose()
        {
            this._subscription.Dispose();
            this._trigger.Dispose();
            this._isExecuting.Dispose();
            this._ownedCanExecute.Dispose();
        }
    }
}
