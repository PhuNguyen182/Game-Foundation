using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Orchestrator;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices;
using DracoRuan.Foundation.DataFlow.DataMigration.Migrator;
using DracoRuan.Foundation.DataFlow.DataProviders;
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
        private readonly IDataSaveService _dataSaveService;
        private readonly IDataSerializer<TData> _dataSerializer;
        private readonly CancellationToken _cancellationToken;
        private readonly DataMigrationOrchestrator _migrationOrchestrator;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IDataProvider _dataProvider;
        
        private bool _isDisposed;
        private bool _isDataInitialized;
        
        public Type SourceDataType => typeof(TData);

        protected MigrationContext MigrationContext;
        protected event Action<TData> OnDataChangedInternal;
        protected abstract TData SourceData { get; set; }
        
        public abstract int CurrentDataVersion { get; }
        public event Action OnDataLoaded;

        public event Action<TData> OnDataChanged
        {
            add => this.OnDataChangedInternal += value;
            remove => this.OnDataChangedInternal -= value;
        }
        
        public TData ExposedSourceData => this.SourceData;

        #region Initialization

        protected DynamicGameDataController(IDataProviderService dataProviderService, SaveDataEvent saveDataEvent,
            DeleteDataEvent deleteDataEvent, DataMigrationOrchestrator dataMigrationOrchestrator)
        {
            this._isDataInitialized = false;
            this._cancellationTokenSource = new CancellationTokenSource();
            this._cancellationToken = this._cancellationTokenSource.Token;
            this._migrationOrchestrator = dataMigrationOrchestrator;

            this._dataSerializer = new MessagePackBinaryDataSerializer<TData>();
            this._dataSaveService = dataProviderService.GetDataSaveServiceByType(DataSourceType.File);
            this._dataProvider = dataProviderService.GetDataProviderByType(DataSourceType.File);
            this._deleteDataEvent = deleteDataEvent;
            this._saveDataEvent = saveDataEvent;
            this.SubscribeDataEvents();
            this.MigrateData().Forget();
        }
        
        public bool IsDataControllerIInitialized() => this._isDataInitialized;

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

        private async UniTask MigrateData()
        {
            byte[] mostRecentSavedRawData = this.LoadRawData();
            await UniTask.WaitUntil(this._migrationOrchestrator.AllDataMigratorsRegistered,
                cancellationToken: this._cancellationToken);
            
            string domain = nameof(TData);
            int mostRecentDataVersion = this.GetMostRecentDataVersion();
            if (mostRecentDataVersion >= this.CurrentDataVersion)
            {
                this.SourceData = this._dataSerializer.Deserialize(mostRecentSavedRawData);
            }
            else
            {
                this.MigrationContext = new MigrationContext
                {
                    PlayerId = domain,
                    CurrentVersion = mostRecentDataVersion,
                    TargetVersion = this.CurrentDataVersion
                };

                this.MigrationContext.SetRawData(mostRecentDataVersion, mostRecentSavedRawData);
                MigrationResult migrationResult = await this._migrationOrchestrator.MigrateData(this.MigrationContext);
                if (migrationResult.IsSuccess)
                    this.LoadDataFromLatestVersion(this.MigrationContext);
            }

            this.OnDataChangedInternal?.Invoke(this.SourceData);
            this.OnDataLoaded?.Invoke();
            this._isDataInitialized = true;
        }

        protected abstract void LoadDataFromLatestVersion(MigrationContext migrationContext);
        
        #endregion

        #region Load Data

        private byte[] LoadRawData()
        {
            this.SourceData = new TData();
            int mostRecentDataVersion = this.GetMostRecentDataVersion();
            string dataSavePath = this.GetDataSaveKeyByVersion(mostRecentDataVersion);
            byte[] mostRecentSavedRawData = this._dataSaveService.LoadData(dataSavePath);
            return mostRecentSavedRawData;
        }
        
        #endregion

        #region Save Data
        
        public void SaveData()
        {
            if (this._dataProvider is not IDataSaver dataSaver) 
                return;
            
            int currentDataVersion = this.SourceData.DataVersion;
            string dataSavePath = this.GetDataSaveKeyByVersion(currentDataVersion);
            
            // Đoạn này có thể chấp nhận vì data runtime có cùng câu trúc với persistant data
            dataSaver.SaveData(this.SourceData, dataSavePath, this._dataSerializer,
                this._dataSaveService);
        }

        public void DeleteData()
        {
            this._dataSaveService.DeleteData(this.SourceDataType.Name);
        }

        private int GetMostRecentDataVersion()
        {
            int currentDataVersion = this.CurrentDataVersion;
            while (currentDataVersion > 1)
            {
                string dataSavePath = this.GetDataSaveKeyByVersion(currentDataVersion);
                if (this._dataSaveService.IsDataExist(dataSavePath))
                    break;
                
                currentDataVersion -= 1;
            }
            
            return currentDataVersion;
        }

        private string GetDataSaveKeyByVersion(int version)
        {
            string dataSavePath = $"{this.SourceDataType.Name}_v{version}";
            return dataSavePath;
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
