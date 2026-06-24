#if USE_CSV_HELPER
using System;
using System.Collections.Generic;
using CsvHelper.Configuration;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProcessors;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers.CSVs;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public abstract class StaticGameDataControllerWithRecord<TData, TRecord, TRecordMap> : IStaticGameDataController 
        where TData : CustomRecordData<TRecord>, IGameData, new()
        where TRecord : class
        where TRecordMap : ClassMap<TRecord>
    {
        private bool _isDisposed;
        private bool _isDataInitialized;
        
        private readonly IDataProviderService _dataProviderService;
        private IDataProvider _dataProvider;
        
        protected abstract TData SourceData { get; set; }
        protected abstract List<DataProcessSequence> DataProcessSequences { get; }
        
        public TData ExposedSourceData => this.SourceData;
        public int DataVersion => this.SourceData?.DataVersion ?? 0;
        public Type SourceDataType => typeof(TData);
        public event Action OnDataLoaded;
        
        protected StaticGameDataControllerWithRecord(IDataProviderService dataProviderService)
        {
            this._isDataInitialized = false;
            this._dataProviderService = dataProviderService;
        }

        public bool IsDataControllerIInitialized() => this._isDataInitialized;

        public async UniTask InitializeData(IDataSequenceProcessor dataSequenceProcessor)
        {
            dataSequenceProcessor.Clear();
            foreach (DataProcessSequence dataProcessSequence in this.DataProcessSequences)
            {
                IProcessSequence processSequence = GetDataProcessorByType(dataProcessSequence);
                if (processSequence == null)
                    continue;

                dataSequenceProcessor.Append(processSequence);
            }

            await dataSequenceProcessor.Execute();
            if (dataSequenceProcessor.LatestProcessSequence is IProcessSequenceData processSequenceData)
                this.SourceData = processSequenceData.GameData as TData;
            
            this.OnDataInitialized();
        }

        protected virtual void OnDataInitialized()
        {
            this.RefineDataFromSourceData();
            this.OnDataLoaded?.Invoke();
            this._isDataInitialized = true;
        }

        protected abstract void RefineDataFromSourceData();
        
        protected void CleanupUnusedData()
        {
            this._dataProvider?.UnloadData(this.SourceData);
        }
        
        protected string GetDataKey()
        {
            GameDataAttribute attribute = GetAttribute<TData>();
            return attribute?.DataKey ?? typeof(TData).Name;
        }

        private static GameDataAttribute GetAttribute<T>() where T : IGameData =>
            (GameDataAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(GameDataAttribute));

        private IProcessSequence GetDataProcessorByType(DataProcessSequence dataProcessSequence)
        {
            string dataKey = dataProcessSequence.DataKey;
            this._dataProvider =
                this._dataProviderService.GetDataProviderByType(dataProcessSequence.DataSourceType);
            IProcessSequence processSequence =
                new DataProcessorWithRecord<TData, TRecord, TRecordMap>(dataKey, this._dataProvider);
            return processSequence;
        }

        protected virtual void ReleaseManagedResources()
        {
            
        }
        
        protected virtual void ReleaseUnmanagedResources()
        {
            
        }

        protected virtual void Dispose(bool disposing)
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

        ~StaticGameDataControllerWithRecord() => this.Dispose(false);
    }
}
#endif
