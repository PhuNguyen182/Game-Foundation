using DG.Tweening;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation
{
    [CreateAssetMenu(fileName = "AnimationConfig", menuName = "DracoRuan/UISystem/AnimationConfig/DOTweenAnimationConfig")]
    public class DOTweenAnimationConfig : ScriptableObject
    {
        [SerializeField] public SequentialAnimation animationConfig;

        public Tween PlayAnimation(CanvasGroup target)
        {
            this.animationConfig.SetTargetAnimation(target);
            Tween tweenAnimation = this.animationConfig.BuildSimultaneouslyAnimation();
            tweenAnimation.Play();
            return tweenAnimation;
        }
    }
}
