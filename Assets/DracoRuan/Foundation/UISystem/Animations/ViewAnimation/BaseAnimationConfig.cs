using System;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation
{
    [Serializable]
    public class BaseAnimationConfig
    {
        public AnimationType animationType;
        
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
