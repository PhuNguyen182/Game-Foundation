using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Views
{
    [RequireComponent(typeof(AnimationMachine))]
    public abstract class BaseUIView : MonoBehaviour, IUIView
    {
        [SerializeField] private AnimationMachine animationMachine;

        public virtual async UniTask Show(Action onShown = null)
        {
            await this.animationMachine.PlayShowAnimation();
            onShown?.Invoke();
        }

        public virtual async UniTask Hide(Action onHidden = null)
        {
            await this.animationMachine.PlayHideAnimation();
            onHidden?.Invoke();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.animationMachine)
                this.animationMachine = this.GetComponent<AnimationMachine>();
        }
#endif
    }
}
