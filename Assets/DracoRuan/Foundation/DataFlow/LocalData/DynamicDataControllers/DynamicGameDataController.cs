using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.MasterDataController;
using DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using MessagePipe;

namespace DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers
{
    public abstract class DynamicGameDataController<TData> : IDynamicGameDataController,
        IDynamicGameDataControllerEvent<TData> where TData : class, IGameData, IDisposable, new()
    {
        private readonly SaveDataEvent _saveDataEvent;
        private readonly DeleteDataEvent _deleteDataEvent;
        private readonly CancellationToken _cancellationToken;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private bool _isDisposed;
        private bool _isDataInitialized;
        
        public Type SourceDataType => typeof(TData);
        private IDataProvider _dataProvider;
        
        protected event Action<TData> OnDataChangedInternal;
        protected abstract TData SourceData { get; set; }

        protected abstract IDataSerializer<TData> DataSerializer { get; set; }
        
        protected abstract IDataSaveService DataSaveService { get; set; }

        protected abstract SerializationType SerializationType { get; }
        protected abstract DataSourceType DataSourceType { get; }
        
        public int DataVersion => this.SourceData?.DataVersion ?? 0;

        public event Action<TData> OnDataChanged
        {
            add => this.OnDataChangedInternal += value;
            remove => this.OnDataChangedInternal -= value;
        }
        
        public TData ExposedSourceData => this.SourceData;

        public bool IsDataControllerIInitialized() => this._isDataInitialized;

        #region Initialization

        protected DynamicGameDataController(IDataProviderService dataProviderService, SaveDataEvent saveDataEvent,
            DeleteDataEvent deleteDataEvent)
        {
            this._isDataInitialized = false;
            this._cancellationTokenSource = new CancellationTokenSource();
            this._cancellationToken = this._cancellationTokenSource.Token;
            
            this.DataSerializer = this.GetDataSerializer();
            this.DataSaveService = dataProviderService.GetDataSaveServiceByType(this.DataSourceType);
            this._dataProvider = dataProviderService.GetDataProviderByType(this.DataSourceType);
            this._deleteDataEvent = deleteDataEvent;
            this._saveDataEvent = saveDataEvent;
            this.SubscribeDataEvents();
        }

        private void SubscribeDataEvents()
        {
            this._saveDataEvent.SaveDataSubscriber
                .Subscribe(this.OnSaveDataMessageReceived)
                .AddTo(this._cancellationToken);
            this._deleteDataEvent.DeleteDataSubscriber
                .Subscribe(this.OnDeleteDataMessageReceived)
                .AddTo(this._cancellationToken);
        }

        private void OnSaveDataMessageReceived(SaveDataMessage message)
        {
            if (!message.SaveAllData && message.DynamicDataType != this.SourceDataType) 
                return;
            
            Debug.Log($"Save data {nameof(this.SourceDataType)}");
            this.SaveData();
        }

        private void OnDeleteDataMessageReceived(DeleteDataMessage message)
        {
            if (!message.DeleteAllData && message.DynamicDataType != this.SourceDataType) 
                return;
            
            Debug.Log($"Delete data {nameof(this.SourceDataType)}");
            this.DeleteData();
        }
        
        #endregion

        #region Data Save And Load

        public async UniTask LoadData()
        {
            this.SourceData =
                await this._dataProvider.LoadDataAsync(SourceDataType.Name, this.DataSerializer, this.DataSaveService);
            this.SourceData ??= new TData();
            this.OnDataChangedInternal?.Invoke(this.SourceData);
            this._isDataInitialized = true;
        }

        public UniTask SaveDataAsync()
        {
            if (this._dataProvider is IDataSaver dataSaver) 
                dataSaver.SaveData(this.SourceData, this.SourceDataType.Name, this.DataSerializer, this.DataSaveService);
            return UniTask.CompletedTask;
        }

        public void SaveData()
        {
            if (this._dataProvider is IDataSaver dataSaver) 
                dataSaver.SaveData(this.SourceData, this.SourceDataType.Name, this.DataSerializer, this.DataSaveService);
        }

        public void DeleteData()
        {
            this.DataSaveService.DeleteData(this.SourceDataType.Name);
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
            this._cancellationTokenSource?.Cancel();
            this._cancellationTokenSource?.Dispose();
            this.OnDataChangedInternal = null;
            this.SourceData?.Dispose();
            this.SourceData = null;
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
