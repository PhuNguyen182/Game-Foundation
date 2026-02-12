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
                showAnimationTasks.Add(this.PlayShowSubject());
                showAnimationTasks.Add(this.PlayShowBackground());
                await UniTask.WhenAll(showAnimationTasks);
            }
        }

        private async UniTask PlayShowSubject()
        {
            this.animatableSubject.interactable = false;
            switch (this.showSubjectConfig.animationType)
            {
                case AnimationType.Animator:
                    await this.PlayShowSubjectAnimationByAnimator();
                    break;
                case AnimationType.DOTween:
                    await this.PlayShowSubjectAnimationByDoTween();
                    break;
            }

            this.animatableSubject.interactable = true;
        }

        private async UniTask PlayShowBackground()
        {
            switch (this.showBackgroundConfig.animationType)
            {
                case AnimationType.Animator:
                    await this.PlayShowBackgroundAnimationByAnimator();
                    break;
                case AnimationType.DOTween:
                    await this.PlayShowBackgroundAnimationByDoTween();
                    break;
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
            await UniTask.CompletedTask;
        }

        private async UniTask PlayShowBackgroundAnimationByDoTween()
        {
            await UniTask.CompletedTask;
        }

        #endregion

        #region Hide Animation

        public async UniTask PlayHideAnimation()
        {
            using (ListPool<UniTask>.Get(out List<UniTask> showAnimationTasks))
            {
                showAnimationTasks.Add(this.PlayHideSubject());
                showAnimationTasks.Add(this.PlayHideBackground());
                await UniTask.WhenAll(showAnimationTasks);
            }
        }
        
        private async UniTask PlayHideSubject()
        {
            this.animatableSubject.interactable = false;
            switch (this.hideSubjectConfig.animationType)
            {
                case AnimationType.Animator:
                    await this.PlayHideSubjectAnimationByAnimator();
                    break;
                case AnimationType.DOTween:
                    await this.PlayHideSubjectAnimationByDoTween();
                    break;
            }
        }
        
        private async UniTask PlayHideBackground()
        {
            this.animatableBackground.interactable = true;
            switch (this.hideBackgroundConfig.animationType)
            {
                case AnimationType.Animator:
                    await this.PlayHideBackgroundAnimationByAnimator();
                    break;
                case AnimationType.DOTween:
                    await this.PlayHideBackgroundAnimationByDoTween();
                    break;
            }
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
