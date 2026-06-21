using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    public abstract class BaseAnimation : ScriptableObject
    {
        protected CanvasGroup Target;
        
        public abstract void SetTargetAnimation(CanvasGroup target);
        public abstract Tween BuildAnimation();
    }
}