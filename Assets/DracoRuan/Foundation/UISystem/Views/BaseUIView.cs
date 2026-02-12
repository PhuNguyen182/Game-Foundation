using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Views
{
    [RequireComponent(typeof(AnimationMachine))]
    public abstract class BaseUIView : MonoBehaviour, IUIView
    {
        [SerializeField] private bool forceDestroyOnClose;
        [SerializeField] private AnimationMachine animationMachine;

        public virtual void Show(Action onShown = null)
        {
            this.animationMachine.PlayShowAnimation()
                .ContinueWith(() => onShown?.Invoke())
                .Forget();
        }

        public virtual void Hide(Action onHidden = null)
        {
            this.animationMachine.PlayHideAnimation()
                .ContinueWith(() =>
                {
                    onHidden?.Invoke();
                    if (this.forceDestroyOnClose)
                        Destroy(this.gameObject);
                }).Forget();
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
