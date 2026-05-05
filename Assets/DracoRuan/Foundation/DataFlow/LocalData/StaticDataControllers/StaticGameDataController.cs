using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProcessors;
using DracoRuan.Foundation.DataFlow.DataProviders;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public abstract class StaticGameDataController<TData> : IStaticGameDataController 
        where TData : class, IGameData
    {
        private readonly IDataProviderService _dataProviderService;
        private IDataProvider _dataProvider;
        
        private bool _isDisposed;
        private bool _isDataInitialized;
        
        protected abstract TData SourceData { get; set; }
        protected abstract List<DataProcessSequence> DataProcessSequences { get; }

        public Type SourceDataType => typeof(TData);
        public TData ExposedSourceData => this.SourceData;
        public int DataVersion => this.SourceData?.DataVersion ?? 0;

        protected StaticGameDataController(IDataProviderService dataProviderService)
        {
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
            {
                this.SourceData = processSequenceData.GameData as TData;
                this._isDataInitialized = true;
            }
            
            this.OnDataInitialized();
        }
        
        protected abstract void OnDataInitialized();

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
            this._dataProvider = this._dataProviderService.GetDataProviderByType(dataProcessSequence.DataSourceType);
            IProcessSequence processSequence = new DataProcessor<TData>(dataKey, this._dataProvider);
            return processSequence;
        }
        
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

        ~StaticGameDataController() => this.Dispose(false);
    }
}
