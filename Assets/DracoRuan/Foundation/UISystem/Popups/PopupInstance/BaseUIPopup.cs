using DracoRuan.Foundation.UISystem.UIElements;
using DracoRuan.Foundation.UISystem.Views;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Popups.PopupInstance
{
    public abstract class BaseUIPopup : BaseUIView
    {
        [SerializeField] protected BaseUIButton closeButton;

        protected virtual void Awake()
        {
            if (this.closeButton)
                this.closeButton.OnClick += this.OnCloseButtonClicked;
        }

        protected virtual void OnCloseButtonClicked()
        {
            this.Hide();
        }

        protected virtual void OnDestroy()
        {
            if (this.closeButton)
                this.closeButton.OnClick -= this.OnCloseButtonClicked;
        }
    }
}
