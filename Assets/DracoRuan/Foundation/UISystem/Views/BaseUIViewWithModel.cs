using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Views
{
    public abstract class BaseUIView<TViewModel> : MonoBehaviour, IUIView<TViewModel>
    {
        private event Action OnViewModelUpdatedInternal; 
        
        public TViewModel ViewModel { get; private set; }
        public event Action OnViewModelUpdated
        {
            add => this.OnViewModelUpdatedInternal += value;
            remove => this.OnViewModelUpdatedInternal -= value;
        }
        
        public abstract UniTask Show();

        public abstract UniTask Hide();
        
        public void BindViewModel(TViewModel viewModel)
        {
            this.ViewModel = viewModel;
            this.OnViewModelBound(viewModel);
            this.OnViewModelUpdatedInternal?.Invoke();
        }

        public void UnbindViewModel()
        {
            this.ViewModel = default;
            this.OnViewModelUnbound();
            this.OnViewModelUpdatedInternal?.Invoke();
        }

        public abstract void OnViewModelBound(TViewModel viewModel);

        public abstract void OnViewModelUnbound();

        protected virtual void OnDestroy()
        {
            this.UnbindViewModel();
            this.OnViewModelUpdatedInternal = null;
        }
    }
}
