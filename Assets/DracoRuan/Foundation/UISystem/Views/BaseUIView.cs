using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Views
{
    public abstract class BaseUIView : MonoBehaviour, IUIView
    {
        public abstract UniTask Show(Action onShown = null);

        public abstract UniTask Hide(Action onHidden = null);
    }
}
