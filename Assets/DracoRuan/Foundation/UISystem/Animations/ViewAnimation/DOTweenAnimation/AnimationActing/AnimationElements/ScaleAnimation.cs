using System;
using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements
{
    [Serializable]
    public class ScaleAnimation : AnimationElement
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

            Tween result = this.scaleMode == ScaleMode.Uniform
                ? this.GetUniformScaleTween()
                : this.GetSeparateScaleTween();
            return result;
        }
        
        private Tween GetUniformScaleTween()
        {
            Tween result = this.Target.transform.DOScale(this.endScale, this.duration);
            if (this.uniformEasing.easingType == AnimationEasingType.DOTweenEase)
                result.SetEase(this.uniformEasing.ease);
            else
                result.SetEase(this.uniformEasing.curve);
            
            return result;
        }

        private Tween GetSeparateScaleTween()
        {
            Sequence sequence = DOTween.Sequence();

            Tween xAxisTween = this.Target.transform.DOScaleX(this.endScale.x, this.duration);
            if (this.xAxisEasing.easingType == AnimationEasingType.DOTweenEase)
                xAxisTween.SetEase(this.xAxisEasing.ease);
            else
                xAxisTween.SetEase(this.xAxisEasing.curve);
            
            Tween yAxisTween = this.Target.transform.DOScaleY(this.endScale.y, this.duration);
            if (this.xAxisEasing.easingType == AnimationEasingType.DOTweenEase)
                yAxisTween.SetEase(this.yAxisEasing.ease);
            else
                yAxisTween.SetEase(this.yAxisEasing.curve);
            
            Tween zAxisTween = this.Target.transform.DOScaleZ(this.endScale.z, this.duration);
            if (this.zAxisEasing.easingType == AnimationEasingType.DOTweenEase)
                zAxisTween.SetEase(this.zAxisEasing.ease);
            else
                zAxisTween.SetEase(this.zAxisEasing.curve);
            
            sequence.Insert(0, xAxisTween);
            sequence.Insert(0, yAxisTween);
            sequence.Insert(0, zAxisTween);
            
            return sequence;
        }
    }
}
