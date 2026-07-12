using DracoRuan.Foundation.UISystem.Canvases;
using DracoRuan.Foundation.UISystem.Popups.PopupInstance;
using DracoRuan.Foundation.UISystem.Popups.PopupManager;
using DracoRuan.Foundation.UISystem.Views;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem
{
    public class UIManager
    {
        private readonly IUICanvasManager _uiCanvasManager;
        private readonly IUIPopupManager _uiPopupManager;

        public UIManager(IUICanvasManager uiCanvasManager, IUIPopupManager uiPopupManager)
        {
            this._uiCanvasManager = uiCanvasManager;
            this._uiPopupManager = uiPopupManager;
        }
        
        #region Puplic Popup Methods
        
        public TView Show<TView>(string popupName = null) where TView : BaseUIPopup
        {
            return this.ShowPopup<TView>(popupName);
        }

        public TView Show<TView, TViewModel>(string popupName = null, TViewModel viewModel = default)
            where TView : BaseUIPopup, IUIModel<TViewModel>
        {
            return this.ShowPopup<TView, TViewModel>(popupName, viewModel);
        }

        public void Hide<TView>(string popupName) where TView : BaseUIPopup
        {
            this.HidePopup<TView>(popupName);
        }
        
        #endregion

        #region Internal Popup Methods

        private TPopup ShowPopup<TPopup>(string popupName) where TPopup : BaseUIPopup
        {
            Transform popupParent = this._uiCanvasManager?.GetCanvas(CanvasCategory.Popup)?.CanvasTransform;
            TPopup popup = this._uiPopupManager.ShowPopup<TPopup>(popupName, popupParent);
            return popup;
        }

        private TPopup ShowPopup<TPopup, TPopupModel>(string popupName, TPopupModel popupModel)
            where TPopup : BaseUIPopup, IUIModel<TPopupModel>
        {
            Transform popupParent = this._uiCanvasManager?.GetCanvas(CanvasCategory.Popup)?.CanvasTransform;
            TPopup popup = this._uiPopupManager.ShowPopup<TPopup, TPopupModel>(popupName, popupParent, popupModel);
            return popup;
        }

        private void HidePopup<TPopup>(string popupName) where TPopup : BaseUIPopup
        {
            if (string.IsNullOrEmpty(popupName))
                this._uiPopupManager.ClosePopupByType<TPopup>();
            else
                this._uiPopupManager.ClosePopup(popupName);
        }

        #endregion
    }
}
