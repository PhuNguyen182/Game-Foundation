using DG.Tweening;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    [CreateAssetMenu(fileName = "ParallelAnimation", menuName = "DracoRuan/UISystem/AnimationConfig/ParallelAnimation")]
    public class ParallelAnimation : AnimationConfig
    {
        public AnimationConfig[] animations;
        
        private Sequence _sequence;

        public override void SetTargetAnimation(CanvasGroup target)
        {
            int count = this.animations.Length;
            for (int i = 0; i < count; i++)
            {
                this.animations[i].SetTargetAnimation(target);
            }
        }

        public override Tween BuildAnimation()
        {
            if (this._sequence != null) 
                return this._sequence;
            
            this._sequence = DOTween.Sequence();
            int count = this.animations.Length;
            for (int i = 0; i < count; i++)
            {
                Tween animation = this.animations[i].BuildAnimation();
                this._sequence.Insert(0, animation);
            }
            
            return this._sequence;
        }
        
        public override void TryKillAnimation()
        {
            this._sequence?.Kill();
            if (this.Target)
                this.Target.DOKill();
        }
    }
}
