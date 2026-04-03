using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.Serialization.CustomDataSerializerServices;

namespace DracoRuan.Foundation.DataFlow.DataProcessors
{
    public class DataProcessor<TData> : IProcessSequence, IProcessSequenceData
        where TData : IGameData
    {
        private readonly string _dataConfigKey;
        private readonly IDataProvider _dataProvider;
        private readonly IDataSerializer<TData> _dataSerializer;
        
        public IGameData GameData { get; private set; }

        public DataProcessor(string dataConfigKey, IDataProvider dataProvider, IDataSerializer<TData> dataSerializer = null)
        {
            this._dataConfigKey = dataConfigKey;
            this._dataProvider = dataProvider;
            this._dataSerializer = dataSerializer ?? new JsonDataSerializer<TData>();
        }
        
        public async UniTask<bool> Process()
        {
            try
            {
                TData result = await this._dataProvider.LoadDataAsync(this._dataConfigKey, this._dataSerializer);
                if (result != null)
                {
                    this.GameData = result;
                    Debug.Log($"Loaded data from path: {_dataConfigKey} successfully !!! Result: {this.GameData}");
                    return true;
                }
                
                Debug.LogError($"Failed to load data from path: {_dataConfigKey}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load data from path: {_dataConfigKey}. More info: {e.Message}");
            }
            
            return false;
        }
    }
}
