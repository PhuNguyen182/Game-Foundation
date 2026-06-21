using DG.Tweening;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing
{
    [CreateAssetMenu(fileName = "ParallelAnimation", menuName = "DracoRuan/UISystem/AnimationConfig/ParallelAnimation")]
    public class ParallelAnimation : ScriptableObject
    {
        public SingleAnimation[] singleAnimationConfigs;
        
        private Sequence _sequence;

        public void SetTargetAnimation(CanvasGroup target)
        {
            int count = this.singleAnimationConfigs.Length;
            for (int i = 0; i < count; i++)
            {
                this.singleAnimationConfigs[i].InitializeTarget(target);
            }
        }

        public Tween BuildSimultaneouslyAnimation()
        {
            if (this._sequence != null) 
                return this._sequence;
            
            this._sequence = DOTween.Sequence();
            int count = this.singleAnimationConfigs.Length;
            for (int i = 0; i < count; i++)
            {
                Tween animation = this.singleAnimationConfigs[i].BuildAnimation();
                this._sequence.Insert(0, animation);
            }
            
            return this._sequence;
        }
    }
}
