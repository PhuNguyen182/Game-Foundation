using DracoRuan.PrebuildServices.UISystem.Popups.PopupInstance;
using DracoRuan.PrebuildServices.UISystem.Views;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Popups.PopupManager
{
    public interface IUIPopupManager
    {
        public TPopup ShowPopup<TPopup>(string popupName, Transform parent) where TPopup : BaseUIPopup;

        public TPopup ShowPopup<TPopup, TPopupModel>(string popupName, Transform parent, TPopupModel model)
            where TPopup : BaseUIPopup, IUIModel<TPopupModel>;

        public void ClosePopup(string popupName);
        public void ClosePopupByType<TPopup>() where TPopup : BaseUIPopup;
        public void ClosePopup(BaseUIPopup popupInstance);
        public void CloseAllPopups();
    }
}
