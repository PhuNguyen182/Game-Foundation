using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.MasterDataController;
using DracoRuan.Foundation.DataFlow.ProcessingSequence;
using DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor.CSVs;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public abstract class StaticGameDataControllerWithRecord<TData, TRecord> : IStaticGameDataController 
        where TData : SerializableRecordClass<TRecord>, IGameData, new()
    {
        private bool _isDisposed;
        
        private IDataProviderService _dataProviderService;
        
        protected abstract TData SourceData { get; set; }
        protected abstract List<DataProcessSequence> DataProcessSequences { get; }
        
        public TData ExposedSourceData => this.SourceData;
        
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
        
        private IProcessSequence GetDataProcessorByType(DataProcessSequence dataProcessSequence)
        {
            string dataKey = dataProcessSequence.DataKey;
            IProcessSequence processSequence = dataProcessSequence.DataProcessorType switch
            {
                DataProcessorType.ResourceCsv 
                    => new ResourceCsvDataProcessor<TData, TRecord>(dataKey, this._dataProviderService),
                DataProcessorType.AddressableScriptableObjects 
                    => new AddressableCsvDataProcessor<TData, TRecord>(dataKey, this._dataProviderService),
                _ => null    
            };
            
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
