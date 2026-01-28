using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class ResourceScriptableObjectDataProcessor : IProcessSequence, IProcessSequenceData
    {
        private readonly Type _desiredDataType;
        private readonly string _dataConfigKey;
        private readonly bool _isAssignableFromDesiredType;
        
        public IGameData GameData { get; private set; }
        
        public ResourceScriptableObjectDataProcessor(string dataConfigKey, Type desiredDataType)
        {
            this._dataConfigKey = dataConfigKey;
            this._desiredDataType = desiredDataType;
            this._isAssignableFromDesiredType = typeof(IGameData).IsAssignableFrom(desiredDataType);
        }

        public async UniTask<bool> Process()
        {
            var loadedData = await Resources.LoadAsync<ScriptableObject>(this._dataConfigKey);
            ScriptableObject data = loadedData as ScriptableObject;
            if (!data || !this._desiredDataType.IsInstanceOfType(data) || !this._isAssignableFromDesiredType)
            {
                Debug.LogError($"Failed to load data from resource: {this._dataConfigKey}");
                return false;
            }

            if (data is not IGameData gameData)
            {
                Debug.LogError($"This resource is not compatible with IGameData: {this._dataConfigKey}");
                return false;
            }
            
            this.GameData = gameData;
            Debug.Log($"Loaded data from resource: {this._dataConfigKey}");
            return true;
        }
    }
}
