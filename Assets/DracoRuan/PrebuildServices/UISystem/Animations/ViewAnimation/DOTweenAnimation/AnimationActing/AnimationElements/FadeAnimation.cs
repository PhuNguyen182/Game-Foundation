using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    [CreateAssetMenu(fileName = "FadeAnimation", menuName = "DracoRuan/UISystem/AnimationConfig/FadeAnimation")]
    public class FadeAnimation : SingleAnimation
    {
        public float startValue;
        public float endValue;
        public AnimationEasing easing;
        
        public override Tween BuildAnimation()
        {
            if (!this.Target)
                return null;
            
            Tween result = this.BuildFadeAnimation();
            if (this.loop)
                result.SetLoops(this.loopCount, this.loopType);
            
            result.SetDelay(this.delay);
            return result;
        }

        private Tween BuildFadeAnimation()
        {
            this.Target.alpha = this.startValue;
            Tween result = this.Target.DOFade(this.endValue, this.duration);
            switch (this.easing.easingType)
            {
                case AnimationEasingType.DOTweenEase:
                    result.SetEase(this.easing.ease);
                    break;
                case AnimationEasingType.AnimationCurve:
                    result.SetEase(this.easing.curve);
                    break;
            }

            return result;
        }
    }
}
