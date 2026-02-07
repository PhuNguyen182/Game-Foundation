using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.LocalData;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class ResourceScriptableObjectDataProcessor<TData> : IProcessSequence, IProcessSequenceData
    where TData : IGameData
    {
        private readonly IDataProvider _dataProvider;
        private readonly string _dataConfigKey;
        
        public IGameData GameData { get; private set; }
        
        public ResourceScriptableObjectDataProcessor(string dataConfigKey, IDataProviderService dataProviderService)
        {
            this._dataConfigKey = dataConfigKey;
            this._dataProvider = dataProviderService.GetDataProviderByType(DataProviderType.Resources);
        }

        public async UniTask<bool> Process()
        {
            try
            {
                TData result = await this._dataProvider.LoadDataAsync<TData>(this._dataConfigKey);
                if (result != null)
                {
                    this.GameData = result;
                    Debug.Log($"[ResourceScriptableObjectDataProcessor] Loaded data from path: {_dataConfigKey} successfully !!! Result: {this.GameData}");
                    return true;
                }
                
                Debug.LogError($"[ResourceScriptableObjectDataProcessor] Failed to load data from path: {_dataConfigKey}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceScriptableObjectDataProcessor] Failed to load data from path: {_dataConfigKey}. More info: {e.Message}");
            }
            
            return false;
        }
    }
}
