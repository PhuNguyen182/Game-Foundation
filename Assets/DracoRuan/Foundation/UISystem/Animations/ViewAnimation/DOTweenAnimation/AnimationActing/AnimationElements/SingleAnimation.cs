using System;
using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    public abstract class SingleAnimation : BaseAnimation
    {
        public float duration;

        public override void SetTargetAnimation(CanvasGroup target)
        {
            this.Target = target;
            this.OnTargetSet();
        }

        protected virtual void OnTargetSet()
        {
            
        }
    }

    [Serializable]
    public struct AnimationEasing
    {
        public AnimationEasingType easingType;
        public AnimationCurve curve;
        public Ease ease;
    }
}
