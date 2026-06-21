using System;
using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    [CreateAssetMenu(fileName = "ScaleAnimation", menuName = "DracoRuan/UISystem/AnimationConfig/ScaleAnimation")]
    public class ScaleAnimation : SingleAnimation
    {
        [Serializable]
        public enum ScaleMode
        {
            Uniform = 0,
            Separate = 1,
        }
        
        public Vector3 startScale;
        public Vector3 endScale;
        public ScaleMode scaleMode;
        
        public AnimationEasing uniformEasing;
        public AnimationEasing xAxisEasing;
        public AnimationEasing yAxisEasing;
        public AnimationEasing zAxisEasing;
        
        public override Tween BuildAnimation()
        {
            if (!this.Target)
                return null;
            
            Tween result = this.scaleMode switch
            {
                ScaleMode.Uniform => this.GetUniformScaleTween(),
                ScaleMode.Separate => this.GetSeparateScaleTween(),
                _ => null
            };
            
            return result;
        }
        
        private Tween GetUniformScaleTween()
        {
            Tween result = this.Target.transform.DOScale(this.endScale, this.duration);
            switch (this.uniformEasing.easingType)
            {
                case AnimationEasingType.DOTweenEase:
                    result.SetEase(this.uniformEasing.ease);
                    break;
                case AnimationEasingType.AnimationCurve:
                    result.SetEase(this.uniformEasing.curve);
                    break;
            }
            
            return result;
        }

        private Tween GetSeparateScaleTween()
        {
            Sequence sequence = DOTween.Sequence();

            Tween xAxisTween = this.Target.transform.DOScaleX(this.endScale.x, this.duration);
            this.BuildEasingForTween(xAxisTween, this.xAxisEasing);
            
            Tween yAxisTween = this.Target.transform.DOScaleY(this.endScale.y, this.duration);
            this.BuildEasingForTween(yAxisTween, this.yAxisEasing);
            
            Tween zAxisTween = this.Target.transform.DOScaleZ(this.endScale.z, this.duration);
            this.BuildEasingForTween(zAxisTween, this.zAxisEasing);
            
            sequence.Insert(0, xAxisTween);
            sequence.Insert(0, yAxisTween);
            sequence.Insert(0, zAxisTween);
            
            return sequence;
        }

        private void BuildEasingForTween(Tween tween, AnimationEasing easing)
        {
            switch (easing.easingType)
            {
                case AnimationEasingType.DOTweenEase:
                    tween.SetEase(easing.ease);
                    break;
                case AnimationEasingType.AnimationCurve:
                    tween.SetEase(easing.curve);
                    break;
            }
        }
    }
}
