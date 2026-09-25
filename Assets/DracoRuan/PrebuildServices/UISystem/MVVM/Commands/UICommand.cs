using System;
using R3;

namespace DracoRuan.PrebuildServices.UISystem.MVVM
{
    /// <summary>Synchronous, parameterless command bindable to a button.</summary>
    public sealed class UICommand : IDisposable
    {
        private readonly Action _execute;
        private readonly IDisposable _ownedCanExecute;

        public UICommand(Action execute, Observable<bool> canExecuteSource = null)
        {
            this._execute = execute;
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
        }

        public ReadOnlyReactiveProperty<bool> CanExecute { get; }

        public void Execute()
        {
            if (!this.CanExecute.CurrentValue)
                return;

            this._execute();
        }

        public void Dispose() => this._ownedCanExecute.Dispose();
    }

    /// <summary>Synchronous command that takes a parameter, bindable to e.g. a list item's button.</summary>
    public sealed class UICommand<T> : IDisposable
    {
        private readonly Action<T> _execute;
        private readonly IDisposable _ownedCanExecute;

        public UICommand(Action<T> execute, Observable<bool> canExecuteSource = null)
        {
            this._execute = execute;
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
        }

        public ReadOnlyReactiveProperty<bool> CanExecute { get; }

        public void Execute(T parameter)
        {
            if (!this.CanExecute.CurrentValue)
                return;

            this._execute(parameter);
        }

        public void Dispose() => this._ownedCanExecute.Dispose();
    }
}
