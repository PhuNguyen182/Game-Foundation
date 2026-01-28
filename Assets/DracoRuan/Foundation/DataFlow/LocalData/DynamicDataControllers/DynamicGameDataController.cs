using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.MasterDataController;
using DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService;
using DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.SaveSystem;

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
        private Type DataType => typeof(TData);
        
        protected event Action<TData> OnDataChangedInternal;
        protected abstract TData SourceData { get; set; }

        /// <summary>
        /// Define how data is serialized and deserialized into the format that the data save service can handle.
        /// For example, JSON, XML, etc.
        /// </summary>
        protected abstract IDataSerializer<TData> DataSerializer { get; set; }

        /// <summary>
        /// Define where the data is saved and loaded from. For example, PlayerPrefs, File, etc.
        /// </summary>
        protected abstract IDataSaveService<TData> DataSaveService { get; set; }
        
        public abstract SerializationType SerializationType { get; }
        public abstract DataStorageType DataStorageType { get; }

        public event Action<TData> OnDataChanged
        {
            add => this.OnDataChangedInternal += value;
            remove => this.OnDataChangedInternal -= value;
        }

        /// <summary>
        /// Retrieve the current data, used for other classes to access the data.
        /// </summary>
        public TData ExposedSourceData => this.SourceData;

        #region Initialization
        
        public void RegisterDataService()
        {
            this.DataSerializer = this.GetDataSerializer();
            this.DataSaveService = this.GetDataSaveService(this.DataSerializer);
        }

        #endregion

        public abstract void Initialize();

        public abstract void InjectDataManager(IMainDataManager mainDataManager);

        #region Data Save And Load

        public async UniTask LoadData()
        {
            this.SourceData = await this.DataSaveService.LoadData(DataType.Name);
            this.OnDataChangedInternal?.Invoke(this.SourceData);
        }

        public UniTask SaveDataAsync() => this.DataSaveService.SaveDataAsync(this.DataType.Name, this.SourceData);

        public void SaveData() => this.DataSaveService.SaveData(this.DataType.Name, this.SourceData);

        public void DeleteData() => this.DataSaveService.DeleteData(this.DataType.Name);

        #endregion

        #region Data Service Factory

        private IDataSerializer<TData> GetDataSerializer()
        {
            return this.SerializationType switch
            {
                SerializationType.Json => new JsonDataSerializer<TData>(),
                SerializationType.EncryptedJson => new EncryptedJsonDataSerializer<TData>(),
                SerializationType.Binary => new BinaryDataSerializer<TData>(),
                _ => null
            };
        }

        private IDataSaveService<TData> GetDataSaveService(IDataSerializer<TData> dataSerializer)
        {
            return this.DataStorageType switch
            {
                DataStorageType.PlayerPrefs => new PlayerPrefDataSaveService<TData>(dataSerializer),
                DataStorageType.File => new FileDataSaveService<TData>(dataSerializer),
                _ => null
            };
        }

        #endregion

        #region Disposing
        
        protected virtual void ReleaseManagedResources()
        {
            this.OnDataChangedInternal = null;
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
        
        #endregion
    }
}
