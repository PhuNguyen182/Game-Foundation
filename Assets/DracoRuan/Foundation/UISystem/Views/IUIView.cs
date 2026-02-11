using System;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.UISystem.Views
{
    public interface IUIView
    {
        public UniTask Show();
        public UniTask Hide();
    }
    
    public interface IUIView<TViewModel> : IUIView
    {
        public TViewModel ViewModel { get; }

        public event Action OnViewModelUpdated;

        public void BindViewModel(TViewModel viewModel);
        public void UnbindViewModel();
        public void OnViewModelBound(TViewModel viewModel);
        public void OnViewModelUnbound();
    }
}
