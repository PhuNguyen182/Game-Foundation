using System;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.UISystem.Views
{
    public interface IUIView
    {
        public UniTask Show(Action onShown = null);
        public UniTask Hide(Action onHidden = null);
    }
}
