using System;
using DracoRuan.PrebuildServices.UISystem.Popups.PopupInstance;

namespace DracoRuan.PrebuildServices.UISystem.Popups.PopupManager
{
    [Serializable]
    public class PopupEntry
    {
        public string popupName;
        public BaseUIPopup popupPrefab;
        public bool shouldPreload;
    }
}