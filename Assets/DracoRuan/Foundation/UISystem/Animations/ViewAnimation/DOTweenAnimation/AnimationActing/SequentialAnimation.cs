using DG.Tweening;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    [CreateAssetMenu(fileName = "SequentialAnimation", menuName = "DracoRuan/UISystem/AnimationConfig/SequentialAnimation")]
    public class SequentialAnimation : ScriptableObject
    {
        [SerializeField] private ParallelAnimation[] simultaneouslyAnimations;
        
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
            foreach (ParallelAnimation simultaneouslyAnimation in this.simultaneouslyAnimations)
            {
                Tween animation = simultaneouslyAnimation.BuildSimultaneouslyAnimation();
                this._sequence.Append(animation);
            }
            
            return this._sequence;
        }
    }
}
