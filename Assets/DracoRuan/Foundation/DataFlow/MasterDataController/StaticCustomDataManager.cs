using System;
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.DataProcessors;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public class StaticCustomDataManager : IStaticCustomDataManager
    {
        private bool _isDisposed;
        private bool _isInitialized;
        
        private readonly object _lock = new();
        private readonly Dictionary<Type, IStaticGameDataController> _staticDataHandlers = new();
        private readonly IDataSequenceProcessor _dataSequenceProcessor = new DataSequenceProcessor();

        public StaticCustomDataManager(IEnumerable<IInitializableDataController> staticDataControllers)
        {
            this._staticDataHandlers.Clear();
            foreach (IInitializableDataController dataController in staticDataControllers)
            {
                if (dataController is IStaticGameDataController staticGameDataController)
                    this._staticDataHandlers.Add(staticGameDataController.SourceDataType, staticGameDataController);
            }
        }

        public TDataHandler GetDataHandler<TDataHandler>()
            where TDataHandler : class, IStaticGameDataController
        {
            lock (this._lock)
            {
                if (!this._isInitialized)
                {
                    Debug.LogWarning("GetDataHandler called before initialization");
                    return null;
                }

                Type sourceDataType = typeof(TDataHandler);
                return this._staticDataHandlers.GetValueOrDefault(sourceDataType) as TDataHandler;
            }
        }

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
