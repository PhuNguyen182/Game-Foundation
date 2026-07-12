using DracoRuan.PrebuildServices.UISystem.Popups.PopupManager;
using DracoRuan.PrebuildServices.UISystem.UIElements;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;
using VContainer;

namespace DracoRuan.PrebuildServices.UISystem.Popups.PopupInstance
{
    public abstract class BaseUIPopup : BaseUIView
    {
        [SerializeField] protected BaseUIButton closeButton;
        
        private IUIPopupManager _popupManager;

        [Inject]
        public void Initialize(IUIPopupManager popupManager)
        {
            this._popupManager = popupManager;
        }

        protected virtual void Awake()
        {
            if (this.closeButton)
                this.closeButton.OnClick += this.OnCloseButtonClicked;
        }

        protected virtual void OnCloseButtonClicked()
        {
            this._popupManager?.ClosePopup(this);
        }
        
        public void SetPopupManager(IUIPopupManager popupManager)
        {
            this._popupManager = popupManager;
        }

        protected new virtual void OnDestroy()
        {
            if (this.closeButton)
                this.closeButton.OnClick -= this.OnCloseButtonClicked;
        }
    }
}
