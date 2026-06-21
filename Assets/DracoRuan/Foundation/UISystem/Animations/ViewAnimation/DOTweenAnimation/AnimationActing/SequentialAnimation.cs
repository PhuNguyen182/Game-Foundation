using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    [CreateAssetMenu(fileName = "SequentialAnimation", menuName = "DracoRuan/UISystem/AnimationConfig/SequentialAnimation")]
    public class SequentialAnimation : AnimationConfig
    {
        [SerializeField] private AnimationConfig[] animations;
        
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
            this._sequence = DOTween.Sequence();
            foreach (AnimationConfig animation in this.animations)
            {
                Tween builtAnimation = animation.BuildAnimation();
                this._sequence.Append(builtAnimation);
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
