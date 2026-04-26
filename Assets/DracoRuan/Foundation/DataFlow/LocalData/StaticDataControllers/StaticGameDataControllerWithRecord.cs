using System;
using System.Collections.Generic;
using CsvHelper.Configuration;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProcessors;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers.CSVs;
using DracoRuan.Foundation.DataFlow.MasterDataController;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public abstract class StaticGameDataControllerWithRecord<TData, TRecord, TRecordMap> : IStaticGameDataController 
        where TData : CustomRecordData<TRecord>, IGameData, new()
        where TRecord : class
        where TRecordMap : ClassMap<TRecord>
    {
        private bool _isDisposed;
        
        private IDataProviderService _dataProviderService;
        
        protected abstract TData SourceData { get; set; }
        protected abstract List<DataProcessSequence> DataProcessSequences { get; }
        
        public TData ExposedSourceData => this.SourceData;
        public int DataVersion => this.SourceData?.Version ?? 0;
        
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

        public void InjectDataManager(IMainDataManager mainDataManager)
        {
            this._dataProviderService = mainDataManager.DataProviderService;
        }
        
        protected abstract void OnDataInitialized();
        
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
            IDataProvider dataProvider =
                this._dataProviderService.GetDataProviderByType(dataProcessSequence.DataSourceType);
            IProcessSequence processSequence =
                new DataProcessorWithRecord<TData, TRecord, TRecordMap>(dataKey, dataProvider);
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
