using System;
using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Binding;

namespace DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Views
{
    /// <summary>
    /// A bindable component that does NOT go through the router: a sub-view, list item, or
    /// reusable widget (CurrencyBar, TimerLabel). TVM is not required to be a routed
    /// UIViewModel — widgets bind directly to whatever plain observable model their owner
    /// passes in.
    /// </summary>
    public abstract class UIWidget<TViewModel> : UnityEngine.MonoBehaviour where TViewModel : class
    {
        private IDisposable _binding;

        public TViewModel ViewModel { get; private set; }

        protected abstract void Bind(ref UIBinder binder, TViewModel viewModel);

        public void BindViewModel(TViewModel viewModel)
        {
            this.UnbindViewModel();

            this.ViewModel = viewModel;
            UIBinder binder = new UIBinder();
            this.Bind(ref binder, viewModel);
            this._binding = binder.Build();
        }

        public void UnbindViewModel()
        {
            this._binding?.Dispose();
            this._binding = null;
            this.ViewModel = null;
        }
    }
}
