using System;
using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    [Serializable]
    public abstract class AnimationElement
    {
        public float duration;

        protected CanvasGroup Target;
        
        public virtual void InitializeTarget(CanvasGroup target) => this.Target = target;
        
        public abstract Tween BuildAnimation();
    }

    [Serializable]
    public struct AnimationEasing
    {
        public AnimationEasingType easingType;
        public AnimationCurve curve;
        public Ease ease;
    }
}
