using DG.Tweening;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    public abstract class AnimationConfig : ScriptableObject
    {
        protected CanvasGroup Target;
        
        public abstract void SetTargetAnimation(CanvasGroup target);
        public abstract Tween BuildAnimation();
        public abstract void TryKillAnimation();
    }
}