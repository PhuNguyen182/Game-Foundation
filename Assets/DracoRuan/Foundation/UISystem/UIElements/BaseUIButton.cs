using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.UISystem.Views;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.Foundation.UISystem.UIElements
{
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BaseUIButton : BaseUIView
    {
        [SerializeField] private Button uiButton;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] [Range(0f, 10f)] private float clickCooldown = 0.3f;
        
        private bool _isClicking;
        private CancellationToken _destroyCancellationToken;
        private event Action OnClickInternal;
        
        public event Action OnClick
        {
            add => this.OnClickInternal += value;
            remove => this.OnClickInternal -= value;
        }

        protected virtual void Awake()
        {
            this._destroyCancellationToken = this.GetCancellationTokenOnDestroy();
            this.RegisterButtonEvents();
        }

        protected virtual void OnEnable()
        {
            bool isInteractable = this.canvasGroup.interactable;
            this.SetInteractable(isInteractable);
        }

        private void RegisterButtonEvents()
        {
            this.uiButton.onClick.AddListener(this.OnButtonClick);
        }

        protected virtual void OnButtonClick()
        {
            this.OnClickInternal?.Invoke();
            this.TryBlockButtonInteraction().Forget();
        }

        private async UniTask TryBlockButtonInteraction()
        {
            if (this._isClicking || this.clickCooldown <= 0f)
                return;

            this.SetInteractable(false);
            this._isClicking = true;
            await UniTask.Delay(TimeSpan.FromSeconds(this.clickCooldown),
                cancellationToken: this._destroyCancellationToken);
            this.SetInteractable(true);
            this._isClicking = false;
        }

        public virtual void SetInteractable(bool interactable)
        {
            if (this._isClicking)
                return;
            
            this.canvasGroup.interactable = interactable;
        }

        protected virtual void OnDestroy()
        {
            this.uiButton.onClick.RemoveListener(this.OnButtonClick);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.uiButton)
                this.uiButton = this.GetComponent<Button>();
            
            if (!this.canvasGroup)
                this.canvasGroup = this.GetComponent<CanvasGroup>();
        }
#endif
    }
}
