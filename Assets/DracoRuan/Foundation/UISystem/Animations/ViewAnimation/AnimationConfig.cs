using System;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation
{
    [Serializable]
    public class AnimationConfig
    {
        public AnimationType animationType;
        
        [Header("DOTween Config")]
        public DOTweenAnimationConfig dotweenAnimationConfig;
        
        [Header("Animator Config")]
        [Tooltip("If use Animator as main animation playback, please fill in the animation clip name")] 
        public string animationClipName;
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
    }
}
