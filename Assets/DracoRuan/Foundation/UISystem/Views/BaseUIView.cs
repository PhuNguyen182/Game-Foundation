using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Views
{
    public abstract class BaseUIView : MonoBehaviour, IUIView
    {
        public abstract UniTask Show();

        public abstract UniTask Hide();
    }
}
