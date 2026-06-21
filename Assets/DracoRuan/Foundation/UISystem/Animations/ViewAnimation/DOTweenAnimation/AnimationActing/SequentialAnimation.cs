using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    [CreateAssetMenu(fileName = "SequentialAnimation", menuName = "DracoRuan/UISystem/AnimationConfig/SequentialAnimation")]
    public class SequentialAnimation : BaseAnimation
    {
        [SerializeField] private BaseAnimation[] animations;
        
        private Sequence _sequence;

        public SequentialAnimation(BaseAnimation[] animations)
        {
            this.animations = animations;
        }

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
            foreach (ParallelAnimation simultaneouslyAnimation in this.animations)
            {
                Tween animation = simultaneouslyAnimation.BuildAnimation();
                this._sequence.Append(animation);
            }
            
            return this._sequence;
        }
    }
}
