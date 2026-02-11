using System;
using DracoRuan.Foundation.UISystem.Popups.PopupInstance;

namespace DracoRuan.Foundation.UISystem.Popups.PopupManager
{
    [Serializable]
    public class PopupEntry
    {
        public string popupName;
        public BaseUIPopup popupPrefab;
    }
}