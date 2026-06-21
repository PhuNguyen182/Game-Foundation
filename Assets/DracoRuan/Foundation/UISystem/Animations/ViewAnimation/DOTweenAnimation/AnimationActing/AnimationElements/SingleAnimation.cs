using System;
using UnityEngine;
using DG.Tweening;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    public abstract class SingleAnimation : AnimationConfig
    {
        [Header("Animation Timing")]
        public float duration;
        public float delay;
        
        [Header("Animation Loop")]
        public bool loop;
        public LoopType loopType = LoopType.Restart;
        public int loopCount = 1;
        
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
