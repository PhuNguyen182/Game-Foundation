using System;

namespace DracoRuan.Foundation.UISystem.Views
{
    public interface IUIView
    {
        public void Show(Action onShown = null);
        public void Hide(Action onHidden = null);
    }
}
