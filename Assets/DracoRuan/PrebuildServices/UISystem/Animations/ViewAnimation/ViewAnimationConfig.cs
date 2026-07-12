using System;
using DG.Tweening;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation
{
    [Serializable]
    public class ViewAnimationConfig
    {
        public AnimationType animationType;

        [Header("DOTween Config")] [ShowIf("animationType", AnimationType.DOTween)]
        public AnimationConfig tweenAnimationConfig;

        [Header("Animator Config")]
        [Tooltip("If use Animator as main animation playback, please fill in the animation clip name")]
        [ShowIf("animationType", AnimationType.Animator)]
        public string animationClipName;

        [ShowIf("animationType", AnimationType.Animator)]
        public float animationDuration;

        private int _animationClipHash;

        public int AnimationClipHash
        {
            get
            {
                if (this._animationClipHash == 0)
                    this._animationClipHash = Animator.StringToHash(this.animationClipName);

                return this._animationClipHash;
            }
        }

        public Tween PlayTweenAnimation(CanvasGroup target)
        {
            this.tweenAnimationConfig.TryKillAnimation();
            this.tweenAnimationConfig.SetTargetAnimation(target);
            Tween tweenAnimation = this.tweenAnimationConfig.BuildAnimation();
            tweenAnimation.Play();
            return tweenAnimation;
        }

        public void TryKillAnimation()
        {
            if (animationType == AnimationType.DOTween && this.tweenAnimationConfig)
                this.tweenAnimationConfig.TryKillAnimation();
        }
    }
}
