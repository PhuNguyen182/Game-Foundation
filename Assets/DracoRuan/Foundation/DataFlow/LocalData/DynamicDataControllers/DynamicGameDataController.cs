using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.MasterDataController;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.Serialization;

namespace DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers
{
    /// <summary>
    /// This class is the base class for all dynamic data type handlers.
    /// Usually use this class to handle data that change frequently, like progression data or player preference data, etc.
    /// NOTE: The SourceData class and GameDataHandler class should be split into 2 individual .cs files to make code more readable.
    /// </summary>
    /// <typeparam name="TData">Source Data is used to work with</typeparam>
    public abstract class DynamicGameDataController<TData> : IDynamicGameDataController,
        IDynamicGameDataControllerEvent<TData> where TData : IGameData
    {
        private bool _isDisposed;
        protected abstract TData SourceData { get; set; }

        /// <summary>
        /// Define how data is serialized and deserialized into the format that the data save service can handle.
        /// For example, JSON, XML, etc.
        /// </summary>
        protected abstract IDataSerializer<TData> DataSerializer { get; }

        /// <summary>
        /// Define where the data is saved and loaded from. For example, PlayerPrefs, File, etc.
        /// </summary>
        protected abstract IDataSaveService<TData> DataSaveService { get; }

        public Type DataType => typeof(TData);

        public abstract event Action<TData> OnDataChanged;

        /// <summary>
        /// Retrieve the current data, used for other classes to access the data.
        /// </summary>
        public TData ExposedSourceData => SourceData;

        public abstract void Initialize();

        public abstract void InjectDataManager(IMainDataManager mainDataManager);

        public async UniTask LoadData() => SourceData = await DataSaveService.LoadData(DataType.Name);

        public UniTask SaveDataAsync() => DataSaveService.SaveDataAsync(DataType.Name, SourceData);

        public void SaveData() => DataSaveService.SaveData(DataType.Name, SourceData);

        public void DeleteData() => DataSaveService.DeleteData(DataType.Name);
        
        protected virtual void ReleaseManagedResources()
        {
            
        }

        protected virtual void ReleaseUnmanagedResources()
        {
            
        }

        private void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;
            
            this.ReleaseUnmanagedResources();
            if (disposing)
                this.ReleaseManagedResources();
            
            this._isDisposed = true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DynamicGameDataController() => this.Dispose(false);
    }
}
