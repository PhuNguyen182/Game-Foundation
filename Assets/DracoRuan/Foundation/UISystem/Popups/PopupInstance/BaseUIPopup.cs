using DracoRuan.Foundation.UISystem.Popups.PopupManager;
using DracoRuan.Foundation.UISystem.UIElements;
using DracoRuan.Foundation.UISystem.Views;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Popups.PopupInstance
{
    public abstract class BaseUIPopup : BaseUIView
    {
        [SerializeField] protected BaseUIButton closeButton;
        
        private IUIPopupManager _popupManager;

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

        protected virtual void OnDestroy()
        {
            if (this.closeButton)
                this.closeButton.OnClick -= this.OnCloseButtonClicked;
        }
    }
}
