using System;
using DG.Tweening;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    [Serializable]
    public class FadeAnimation : AnimationElement
    {
        public float startValue;
        public float endValue;
        public AnimationEasing easing;
        
        public override Tween BuildAnimation()
        {
            if (!this.Target)
                return null;
            
            Tween result = this.BuildFadeAnimation();
            return result;
        }

        private Tween BuildFadeAnimation()
        {
            this.Target.alpha = this.startValue;
            Tween result = this.Target.DOFade(this.endValue, this.duration);
            if (this.easing.easingType == AnimationEasingType.DOTweenEase)
                result.SetEase(this.easing.ease);
            else
                result.SetEase(this.easing.curve);
            
            return result;
        }
    }
}
