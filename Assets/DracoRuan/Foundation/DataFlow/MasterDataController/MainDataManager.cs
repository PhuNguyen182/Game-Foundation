using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public class MainDataManager : IMainDataManager
    {
        private bool _isDisposed;
        private IStaticCustomDataManager _staticCustomDataManager;
        private IDynamicCustomDataManager _dynamicCustomDataManager;
        
        public bool IsInitialized { get; private set; }
        
        public MainDataManager()
        {
            this.IsInitialized = false;
            this.InitializeDataHandlers().Forget();
        }
        
        private async UniTask InitializeDataHandlers()
        {
            // Initialize static data first
            this._staticCustomDataManager = new StaticCustomDataManager();
            await this._staticCustomDataManager.InitializeDataControllers(this);
            
            // Then initialize dynamic data later
            this._dynamicCustomDataManager = new DynamicCustomDataManager();
            await this._dynamicCustomDataManager.InitializeDataControllers(this);
            this.IsInitialized = true;
        }

        public TStaticGameDataHandler GetStaticDataController<TStaticGameDataHandler>()
            where TStaticGameDataHandler : class, IStaticGameDataController
            => this._staticCustomDataManager?.GetDataHandler<TStaticGameDataHandler>();

        public TDynamicGameDataHandler GetDynamicDataController<TDynamicGameDataHandler>()
            where TDynamicGameDataHandler : class, IDynamicGameDataController =>
            this._dynamicCustomDataManager?.GetDataHandler<TDynamicGameDataHandler>();

        /// <summary>
        /// Save data asynchronously. Use it when a player is playing the game and wants to save data frequently
        /// or automatically in a period of time.
        /// </summary>
        /// <returns></returns>
        public UniTask SaveAllDataAsync() =>
            this._dynamicCustomDataManager?.SaveAllDataAsync() ?? UniTask.CompletedTask;

        /// <summary>
        /// Save data synchronously. Use it when the player is out of the game or temporarily paused.
        /// </summary>
        public void SaveAllData() => this._dynamicCustomDataManager?.SaveAllData();
        
        public void DeleteSingleData(Type dataType) => this._dynamicCustomDataManager?.DeleteSingleData(dataType);
        
        public void DeleteAllData() => this._dynamicCustomDataManager?.DeleteAllData();
        
        private void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;

            if (disposing)
            {
                this.SaveAllData();
                this._staticCustomDataManager?.Dispose();
                this._dynamicCustomDataManager?.Dispose();
            }
            
            this._isDisposed = true;
        }
        
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
