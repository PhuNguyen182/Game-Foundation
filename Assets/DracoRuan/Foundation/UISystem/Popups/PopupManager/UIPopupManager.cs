using System;
using System.Collections.Generic;
using DracoRuan.Foundation.UISystem.Popups.PopupInstance;
using DracoRuan.Foundation.UISystem.Views;
using UnityEngine;
using UnityEngine.Pool;

namespace DracoRuan.Foundation.UISystem.Popups.PopupManager
{
    public class UIPopupManager : IUIPopupManager, IDisposable
    {
        private readonly PopupCollection _popupCollection;
        private readonly Dictionary<string, BaseUIPopup> _popupDictionary;
        
        private bool _isDisposed;
        
        public UIPopupManager(PopupCollection popupCollection)
        {
            this._popupDictionary = new Dictionary<string, BaseUIPopup>();
            this._popupCollection = popupCollection;
            this._popupCollection.Initialize();
            PreloadPopups();
            return;

            void PreloadPopups()
            {
                int count = this._popupCollection.PopupEntries.Count;
                for (int i = 0; i < count; i++)
                {
                    PopupEntry popupEntry = this._popupCollection.PopupEntries[i];
                    if (popupEntry.shouldPreload)
                    {
                        BaseUIPopup popupPrefab = popupEntry.popupPrefab;
                        GameObjectPoolManager.PoolPreLoad(popupPrefab, 1);
                    }
                }
            }
        }
        
        public TPopup ShowPopup<TPopup>(string popupName, Transform parent) where TPopup : BaseUIPopup
        {
            BaseUIPopup popupPrefab = this._popupCollection.GetPopupByKey(popupName);
            if (!popupPrefab || popupPrefab is not TPopup targetPopup) 
                return null;
            
            TPopup result = GameObjectPoolManager.SpawnInstance(targetPopup, Vector3.zero, Quaternion.identity, parent);
            result.transform.SetAsLastSibling();
            this._popupDictionary.Add(popupName, result);
            return result;
        }

        public TPopup ShowPopup<TPopup, TPopupModel>(string popupName, Transform parent, TPopupModel model) 
            where TPopup : BaseUIPopup, IUIModel<TPopupModel>
        {
            BaseUIPopup popupPrefab = this._popupCollection.GetPopupByKey(popupName);
            if (!popupPrefab || popupPrefab is not TPopup targetPopup) 
                return null;
            
            TPopup result = GameObjectPoolManager.SpawnInstance(targetPopup, Vector3.zero, Quaternion.identity, parent);
            result.transform.SetAsLastSibling();
            result.BindModelData(model);
            this._popupDictionary.Add(popupName, result);
            result.Show();
            return result;
        }

        public void ClosePopup(string popupName)
        {
            if (!this._popupDictionary.Remove(popupName, out BaseUIPopup popupInstance)) 
                return;

            popupInstance.Hide(); // To do: update the Hide function for Popup instance in case of disable or destroy
        }

        public void ClosePopupByType<TPopup>() where TPopup : BaseUIPopup
        {
            using (ListPool<string>.Get(out List<string> popupNames))
            {
                foreach (var kvp in this._popupDictionary)
                {
                    if (kvp.Value.GetType() != typeof(TPopup))
                        continue;

                    popupNames.Add(kvp.Key);
                }

                foreach (string popupName in popupNames)
                {
                    this.ClosePopup(popupName);
                }
            }
        }

        public void ClosePopup(BaseUIPopup popupInstance)
        {
            foreach (var kvp in this._popupDictionary)
            {
                if (kvp.Value.gameObject.GetInstanceID() != popupInstance.gameObject.GetInstanceID()) 
                    continue;
                
                this.ClosePopup(kvp.Key);
                return;
            }
        }

        public void CloseAllPopups()
        {
            foreach (var kvp in this._popupDictionary)
            {
                this.ClosePopup(kvp.Value);
            }
        }

        private void ReleaseManagedResources()
        {
            this._popupDictionary?.Clear();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._isDisposed) 
                return;
            
            if (disposing)
                this.ReleaseManagedResources();
            
            this._isDisposed = true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UIPopupManager()
        {
            this.Dispose(false);
        }
    }
}
