using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.MasterDataController;
using DracoRuan.Foundation.DataFlow.ProcessingSequence;
using DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    /// <summary>
    /// This class is the base class for all static data type handlers.
    /// Usually use this class to handle data that does not change in the whole game cycle, like configuration data, etc.
    /// IMPORTANCE: The SourceData class and GameDataHandler class should (or must) be split into 2 individual .cs files
    /// to ensure in the case of using ScriptableObject as the SourceData class will work properly.
    /// </summary>
    /// <typeparam name="TData">Source Data is used to work with</typeparam>
    public abstract class StaticGameDataController<TData> : IStaticGameDataController where TData : class, IGameData
    {
        private bool _isDisposed;
        private IDataSequenceProcessor _dataSequenceProcessor;
        protected abstract TData SourceData { get; set; }
        
        public Type DataType => typeof(TData);
        
        /// <summary>
        /// Retrieve the current data, used for other classes to access the data.
        /// </summary>
        public TData ExposedSourceData => SourceData;
        
        /// <summary>
        /// Define how data is processed in the order that it is defined.
        /// </summary>
        public abstract List<DataProcessSequence> DataProcessSequences { get; }
        
        public async UniTask Initialize()
        {
            this._dataSequenceProcessor = new DataSequenceProcessor();
            foreach (DataProcessSequence dataProcessSequence in this.DataProcessSequences)
            {
                IProcessSequence processSequence = GetDataProcessorByType(dataProcessSequence);
                if (processSequence == null)
                    continue;

                this._dataSequenceProcessor.Append(processSequence);
            }

            await this._dataSequenceProcessor.Execute();
            if (this._dataSequenceProcessor.LatestProcessSequence is IProcessSequenceData processSequenceData)
                this.SourceData = processSequenceData.GameData as TData;
            
            this.OnDataInitialized();
        }
        
        public abstract void InjectDataManager(IMainDataManager mainDataManager);
        
        protected abstract void OnDataInitialized();

        /// <summary>
        /// Get the data key from the GameDataAttribute. If not found, use the type name.
        /// </summary>
        /// <returns>Return the data key</returns>
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
                DataProcessorType.FirebaseRemoteConfig => new FirebaseRemoteConfigDataProcessor(dataKey, DataType),
                DataProcessorType.ResourceScriptableObjects => new ResourceScriptableObjectDataProcessor(dataKey, DataType),
                DataProcessorType.AddressableScriptableObjects => new AddressableScriptableObjectDataProcessor(dataKey, DataType),
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
