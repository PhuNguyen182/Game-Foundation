using System;
using DracoRuan.Foundation.UISystem.Views;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.Foundation.UISystem.UIElements
{
    [RequireComponent(typeof(Button))]
    public abstract class BaseUIButton : BaseUIView
    {
        [SerializeField] private Button uiButton;
        
        private event Action OnClickInternal;
        
        public event Action OnClick
        {
            add => this.OnClickInternal += value;
            remove => this.OnClickInternal -= value;
        }

        protected virtual void Awake()
        {
            this.RegisterButtonEvents();
        }

        protected virtual void OnEnable()
        {
            bool isInteractable = this.uiButton.interactable;
            this.SetInteractable(isInteractable);
        }

        private void RegisterButtonEvents()
        {
            this.uiButton.onClick.AddListener(this.OnButtonClick);
        }

        protected virtual void OnButtonClick()
        {
            this.OnClickInternal?.Invoke();
        }

        public void SetInteractable(bool interactable)
        {
            this.uiButton.interactable = interactable;
        }

        protected virtual void OnDestroy()
        {
            this.uiButton.onClick.RemoveListener(this.OnButtonClick);
            this.OnClickInternal = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.uiButton)
                this.uiButton = this.GetComponent<Button>();
        }
#endif
    }
}
