using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;
using DracoRuan.Foundation.Initializers.Interfaces;
using UnityEngine;
using UnityEngine.Pool;
using ZLinq;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public class DynamicCustomDataManager : IDynamicCustomDataManager, IAsyncInitializable
    {
        private bool _isDisposed;
        private bool _isInitialized;
        
        private readonly object _lock;
        private readonly Dictionary<Type, IDynamicGameDataController> _dynamicDataHandlers = new();

        public DynamicCustomDataManager(IEnumerable<IInitializableDataController> dynamicDataControllers)
        {
            this._lock = new object();
            this._dynamicDataHandlers.Clear();
            List<IInitializableDataController> dataControllers = dynamicDataControllers.AsValueEnumerable().ToList();
            foreach (IInitializableDataController dataController in dataControllers)
            {
                if (dataController is IDynamicGameDataController staticGameDataController)
                    this._dynamicDataHandlers.Add(staticGameDataController.SourceDataType, staticGameDataController);
            }

            WaitAllDataControllerInitialized().Forget();
            return;

            async UniTask WaitAllDataControllerInitialized()
            {
                using (ListPool<UniTask>.Get(out List<UniTask> waitTasks))
                {
                    foreach (IInitializableDataController dataController in dataControllers)
                    {
                        if (dataController is not IDynamicGameDataController) 
                            continue;
                        
                        UniTask waitTask = UniTask.WaitUntil(dataController.IsDataControllerIInitialized);
                        waitTasks.Add(waitTask);
                    }

                    await UniTask.WhenAll(waitTasks);
                    this._isInitialized = true;
                    dataControllers.Clear();
                }
            }
        }
        
        public bool IsInitialized() => this._isInitialized;

        public TDataHandler GetDataHandler<TDataHandler>()
            where TDataHandler : class, IDynamicGameDataController
        {
            if (!this._isInitialized)
            {
                Debug.LogWarning("GetDataHandler called before initialization");
                return null;
            }

            Type sourceDataType = typeof(TDataHandler);
            return this._dynamicDataHandlers.GetValueOrDefault(sourceDataType) as TDataHandler;
        }

        public void DeleteSingleData(Type dataType) =>
            this._dynamicDataHandlers.GetValueOrDefault(dataType)?.DeleteData();
        
        public void DeleteAllData()
        {
            foreach (IDynamicGameDataController dynamicDataHandler in this._dynamicDataHandlers.Values)
                dynamicDataHandler.DeleteData();
        }
        
        public async UniTask SaveAllDataAsync()
        {
            using (ListPool<UniTask>.Get(out List<UniTask> saveDataTasks))
            {
                foreach (IDynamicGameDataController dynamicDataHandler in this._dynamicDataHandlers.Values)
                {
                    UniTask saveDataTask = dynamicDataHandler.SaveDataAsync();
                    saveDataTasks.Add(saveDataTask);
                }

                await UniTask.WhenAll(saveDataTasks);
            }
        }

        public void SaveAllData()
        {
            foreach (IDynamicGameDataController dynamicDataHandler in this._dynamicDataHandlers.Values)
                dynamicDataHandler.SaveData();
            PlayerPrefs.Save();
        }

        ~DynamicCustomDataManager() => this.Dispose(false);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;

            if (disposing)
                this.ReleaseManagedResources();

            this._isDisposed = true;
        }

        private void ReleaseManagedResources()
        {
            lock (this._lock)
            {
                foreach (IDynamicGameDataController handler in this._dynamicDataHandlers.Values)
                {
                    if (handler is not IDisposable disposable) 
                        continue;
                        
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error disposing handler: {e}");
                    }
                }

                this._dynamicDataHandlers.Clear();
            }
        }
    }
}
