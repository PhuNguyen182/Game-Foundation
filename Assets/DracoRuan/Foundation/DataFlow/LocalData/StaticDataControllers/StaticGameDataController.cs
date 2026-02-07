using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.MasterDataController;
using DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor;
using DracoRuan.Foundation.DataFlow.ProcessingSequence;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public abstract class StaticGameDataController<TData> : IStaticGameDataController where TData : class, IGameData
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

        public virtual void InjectDataManager(IMainDataManager mainDataManager)
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
            IProcessSequence processSequence = dataProcessSequence.DataProcessorType switch
            {
                DataProcessorType.FirebaseRemoteConfig 
                    => new FirebaseRemoteConfigDataProcessor<TData>(dataKey),
                DataProcessorType.ResourceScriptableObjects 
                    => new ResourceScriptableObjectDataProcessor<TData>(dataKey, this._dataProviderService),
                DataProcessorType.AddressableScriptableObjects 
                    => new AddressableScriptableObjectDataProcessor<TData>(dataKey, this._dataProviderService),
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
