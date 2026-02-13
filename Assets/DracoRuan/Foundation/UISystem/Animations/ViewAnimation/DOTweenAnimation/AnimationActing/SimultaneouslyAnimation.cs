using System;
using DG.Tweening;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    [Serializable]
    public class SimultaneouslyAnimation
    {
        public AnimationElement[] AnimationElements;
        
        private Sequence _sequence;

        public void SetTargetAnimation(CanvasGroup target)
        {
            int count = this.AnimationElements.Length;
            for (int i = 0; i < count; i++)
            {
                this.AnimationElements[i].InitializeTarget(target);
            }
        }

        public Tween BuildSimultaneouslyAnimation()
        {
            if (this._sequence != null) 
                return this._sequence;
            
            this._sequence = DOTween.Sequence();
            int count = this.AnimationElements.Length;
            for (int i = 0; i < count; i++)
            {
                Tween animation = this.AnimationElements[i].BuildAnimation();
                this._sequence.Insert(0, animation);
            }
            
            return this._sequence;
        }
    }
}
