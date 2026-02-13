using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    [CreateAssetMenu(fileName = "SequentiallyAnimationConfig", menuName = "DracoRuan/UISystem/DOTweenAnimation/SequentiallyAnimationAnimation")]
    public class SequentiallyAnimation : ScriptableObject
    {
        [SerializeField] private SimultaneouslyAnimation[] simultaneouslyAnimations;
        
        private Sequence _sequence;

        public void SetTargetAnimation(CanvasGroup target)
        {
            int count = this.simultaneouslyAnimations.Length;
            for (int i = 0; i < count; i++)
            {
                this.simultaneouslyAnimations[i].SetTargetAnimation(target);
            }
        }

        public Tween BuildSimultaneouslyAnimation()
        {
            if (this._sequence != null) 
                return this._sequence;
            
            this._sequence = DOTween.Sequence();
            foreach (SimultaneouslyAnimation simultaneouslyAnimation in this.simultaneouslyAnimations)
            {
                Tween animation = simultaneouslyAnimation.BuildSimultaneouslyAnimation();
                this._sequence.Append(animation);
            }
            
            return this._sequence;
        }
    }
}
