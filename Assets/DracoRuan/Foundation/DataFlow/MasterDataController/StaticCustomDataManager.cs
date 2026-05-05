using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProcessors;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers;
using DracoRuan.Foundation.Initializers.Interfaces;
using UnityEngine.Pool;
using ZLinq;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public class StaticCustomDataManager : IStaticCustomDataManager, IAsyncInitializable
    {
        private bool _isDisposed;
        private bool _isInitialized;
        
        private readonly object _lock;
        private readonly Dictionary<Type, IStaticGameDataController> _staticDataHandlers = new();
        private readonly IDataSequenceProcessor _dataSequenceProcessor = new DataSequenceProcessor();

        public StaticCustomDataManager(IEnumerable<IInitializableDataController> staticDataControllers)
        {
            this._lock = new object();
            this._staticDataHandlers.Clear();
            List<IInitializableDataController> dataControllers = staticDataControllers.AsValueEnumerable().ToList();
            foreach (IInitializableDataController dataController in dataControllers)
            {
                if (dataController is IStaticGameDataController staticGameDataController)
                    this._staticDataHandlers.Add(staticGameDataController.SourceDataType, staticGameDataController);
            }

            WaitAllDataControllerInitialized().Forget();
            return;

            async UniTask WaitAllDataControllerInitialized()
            {
                using (ListPool<UniTask>.Get(out List<UniTask> waitTasks))
                {
                    foreach (IInitializableDataController dataController in dataControllers)
                    {
                        if (dataController is not IStaticGameDataController) 
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

        public TDataHandler GetDataHandler<TDataHandler>()
            where TDataHandler : class, IStaticGameDataController
        {
            if (!this._isInitialized)
            {
                Debug.LogWarning("GetDataHandler called before initialization");
                return null;
            }

            Type sourceDataType = typeof(TDataHandler);
            lock (this._lock)
            {
                return this._staticDataHandlers.GetValueOrDefault(sourceDataType) as TDataHandler;
            }
        }

        public bool IsInitialized() => this._isInitialized;

        private void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;

            if (disposing)
                this.ReleaseManagedResources();

            this._isDisposed = true;
        }

        ~StaticCustomDataManager() => this.Dispose(false);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        private void ReleaseManagedResources()
        {
            this._dataSequenceProcessor.Clear();
            lock (this._lock)
            {
                foreach (IStaticGameDataController handler in this._staticDataHandlers.Values)
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

                this._staticDataHandlers.Clear();
            }
        }
    }
}
