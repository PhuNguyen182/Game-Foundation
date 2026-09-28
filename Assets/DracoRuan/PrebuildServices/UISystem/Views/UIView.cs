using System;
using DracoRuan.PrebuildServices.UISystem.Binding;
using DracoRuan.PrebuildServices.UISystem.MVVM;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Views
{
    /// <summary>
    /// Root component of a routed view's prefab (Screen/Popup/HUD/... — all the same entity,
    /// see UIViewDefinition). BindViewModel/UnbindViewModel are called by the router: the
    /// service Activates the view model, then calls BindViewModel; on close it calls
    /// UnbindViewModel first, then Deactivates the view model.
    /// </summary>
    public abstract class UIView<TViewModel> : UIViewBase where TViewModel : UIViewModel
    {
        [UnityEngine.SerializeField] private Selectable defaultSelectable;

        private IDisposable _binding;

        public TViewModel ViewModel { get; private set; }
        public override Selectable DefaultSelectable => this.defaultSelectable;

        protected abstract void Bind(ref UIBinder binder, TViewModel viewModel);

        internal void BindViewModel(TViewModel viewModel)
        {
            this.ViewModel = viewModel;
            UIBinder binder = new UIBinder();
            this.Bind(ref binder, viewModel);
            this._binding = binder.Build();
        }

        internal void UnbindViewModel()
        {
            this._binding?.Dispose();
            this._binding = null;
            this.ViewModel = null;
        }
    }
}
