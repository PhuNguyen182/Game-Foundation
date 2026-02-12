using System.Collections.Generic;
using DracoRuan.Foundation.UISystem.Popups.PopupInstance;
using UnityEngine;

namespace DracoRuan.Foundation.UISystem.Popups.PopupManager
{
    [CreateAssetMenu(fileName = "PopupCollection", menuName = "DracoRuan/UISystem/Popups/PopupCollection")]
    public class PopupCollection : ScriptableObject
    {
        [SerializeField] private List<PopupEntry> popupEntries;
        
        private Dictionary<string, BaseUIPopup> _popupDictionary;
        public List<PopupEntry> PopupEntries => this.popupEntries;

        public void Initialize()
        {
            this._popupDictionary ??= new Dictionary<string, BaseUIPopup>();
            this._popupDictionary.Clear();
            foreach (PopupEntry entry in this.popupEntries)
            {
                this._popupDictionary.Add(entry.popupName, entry.popupPrefab);
            }
        }
        
        public BaseUIPopup GetPopupByKey(string key)
        {
            return this._popupDictionary.GetValueOrDefault(key);
        }
    }
}
