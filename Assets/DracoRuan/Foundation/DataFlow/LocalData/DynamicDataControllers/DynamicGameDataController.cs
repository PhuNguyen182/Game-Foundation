using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.MasterDataController;
using DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.SaveSystem;

namespace DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers
{
    public abstract class DynamicGameDataController<TData> : IDynamicGameDataController,
        IDynamicGameDataControllerEvent<TData> where TData : IGameData, IDisposable
    {
        private bool _isDisposed;
        private Type DataType => typeof(TData);
        private IDataProvider _dataProvider;
        
        protected event Action<TData> OnDataChangedInternal;
        protected abstract TData SourceData { get; set; }

        protected abstract IDataSerializer<TData> DataSerializer { get; set; }
        
        protected abstract IDataSaveService DataSaveService { get; set; }

        protected abstract SerializationType SerializationType { get; }
        protected abstract DataProviderType DataProviderType { get; }

        public event Action<TData> OnDataChanged
        {
            add => this.OnDataChangedInternal += value;
            remove => this.OnDataChangedInternal -= value;
        }
        
        public TData ExposedSourceData => this.SourceData;

        #region Initialization

        public virtual void InjectDataManager(IMainDataManager mainDataManager)
        {
            this.DataSerializer = this.GetDataSerializer();
            this.DataSaveService = mainDataManager.DataProviderService.GetDataSaveServiceByType(this.DataProviderType);
            this._dataProvider = mainDataManager.DataProviderService.GetDataProviderByType(this.DataProviderType);
        }

        public abstract void Initialize();
        
        #endregion

        #region Data Save And Load

        public async UniTask LoadData()
        {
            this.SourceData = await this._dataProvider.LoadDataAsync<TData>(DataType.Name);
            this.OnDataChangedInternal?.Invoke(this.SourceData);
        }

        public UniTask SaveDataAsync()
        {
            if (this._dataProvider is IDataSaver dataSaver) 
                dataSaver.SaveData(this.SourceData, this.DataType.Name, this.DataSerializer, this.DataSaveService);
            return UniTask.CompletedTask;
        }

        public void SaveData()
        {
            if (this._dataProvider is IDataSaver dataSaver) 
                dataSaver.SaveData(this.SourceData, this.DataType.Name, this.DataSerializer, this.DataSaveService);
        }

        public void DeleteData()
        {
            this.DataSaveService.DeleteData(this.DataType.Name);
        }

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

        #endregion

        #region Disposing
        
        protected virtual void ReleaseManagedResources()
        {
            this.OnDataChangedInternal = null;
            this.SourceData?.Dispose();
            this.SourceData = default;
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
