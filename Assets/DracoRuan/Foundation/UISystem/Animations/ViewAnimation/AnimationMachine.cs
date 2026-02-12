using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace DracoRuan.Foundation.UISystem.Animations.ViewAnimation
{
    public class AnimationMachine : MonoBehaviour
    {
        [Header("Main Subject")] 
        [SerializeField] private Animator subjectAnimator;
        [SerializeField] private CanvasGroup animatableSubject;
        [SerializeField] private BaseAnimationConfig showSubjectConfig;
        [SerializeField] private BaseAnimationConfig hideSubjectConfig;

        [Header("Background")] 
        [SerializeField] private Animator backgroundAnimator;
        [SerializeField] private CanvasGroup animatableBackground;
        [SerializeField] private BaseAnimationConfig showBackgroundConfig;
        [SerializeField] private BaseAnimationConfig hideBackgroundConfig;
        
        private CancellationToken _cancellationToken;

        private void Awake()
        {
            this._cancellationToken = this.GetCancellationTokenOnDestroy();
        }

        #region Show Animation

        public async UniTask PlayShowAnimation()
        {
            using (ListPool<UniTask>.Get(out List<UniTask> showAnimationTasks))
            {
                if (this.showSubjectConfig.animationType == AnimationType.Animator)
                    showAnimationTasks.Add(this.PlayShowSubjectAnimationByAnimator());
                
                if (this.showSubjectConfig.animationType == AnimationType.DOTween)
                    showAnimationTasks.Add(this.PlayShowSubjectAnimationByDoTween());
                
                if (this.showBackgroundConfig.animationType == AnimationType.Animator)
                    showAnimationTasks.Add(this.PlayShowBackgroundAnimationByAnimator());
                
                if (this.showBackgroundConfig.animationType == AnimationType.DOTween)
                    showAnimationTasks.Add(this.PlayShowBackgroundAnimationByDoTween());

                await UniTask.WhenAll(showAnimationTasks);
            }
        }

        private async UniTask PlayShowSubjectAnimationByAnimator()
        {
            if (this.subjectAnimator)
            {
                this.subjectAnimator.Play(this.showSubjectConfig.AnimationClipHash);
                await UniTask.WaitForSeconds(this.showSubjectConfig.animationDuration,
                    cancellationToken: this._cancellationToken);
            }
        }
        
        private async UniTask PlayShowBackgroundAnimationByAnimator()
        {
            if (this.backgroundAnimator)
            {
                this.backgroundAnimator.Play(this.showBackgroundConfig.AnimationClipHash);
                await UniTask.WaitForSeconds(this.showBackgroundConfig.animationDuration,
                    cancellationToken: this._cancellationToken);
            }
        }

        private async UniTask PlayShowSubjectAnimationByDoTween()
        {
            using (ListPool<UniTask>.Get(out List<UniTask> showAnimationTasks))
            {
                if (this.hideSubjectConfig.animationType == AnimationType.Animator)
                    showAnimationTasks.Add(this.PlayHideSubjectAnimationByAnimator());
                
                if (this.hideSubjectConfig.animationType == AnimationType.DOTween)
                    showAnimationTasks.Add(this.PlayHideSubjectAnimationByDoTween());
                
                if (this.hideBackgroundConfig.animationType == AnimationType.Animator)
                    showAnimationTasks.Add(this.PlayHideBackgroundAnimationByAnimator());
                
                if (this.hideBackgroundConfig.animationType == AnimationType.DOTween)
                    showAnimationTasks.Add(this.PlayHideBackgroundAnimationByDoTween());

                await UniTask.WhenAll(showAnimationTasks);
            }
        }

        private async UniTask PlayShowBackgroundAnimationByDoTween()
        {
            await UniTask.CompletedTask;
        }

        #endregion

        #region Hide Animation

        public async UniTask PlayHideAnimation()
        {
            await UniTask.CompletedTask;
        }
        
        private async UniTask PlayHideSubjectAnimationByAnimator()
        {
            if (this.subjectAnimator)
            {
                this.subjectAnimator.Play(this.hideSubjectConfig.AnimationClipHash);
                await UniTask.WaitForSeconds(this.hideSubjectConfig.animationDuration,
                    cancellationToken: this._cancellationToken);
            }
        }
        
        private async UniTask PlayHideBackgroundAnimationByAnimator()
        {
            if (this.backgroundAnimator)
            {
                this.backgroundAnimator.Play(this.hideBackgroundConfig.AnimationClipHash);
                await UniTask.WaitForSeconds(this.hideBackgroundConfig.animationDuration,
                    cancellationToken: this._cancellationToken);
            }
        }

        private async UniTask PlayHideSubjectAnimationByDoTween()
        {
            await UniTask.CompletedTask;
        }

        private async UniTask PlayHideBackgroundAnimationByDoTween()
        {
            await UniTask.CompletedTask;
        }

        #endregion
    }
}
