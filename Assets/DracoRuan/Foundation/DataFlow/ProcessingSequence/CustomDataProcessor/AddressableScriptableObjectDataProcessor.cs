using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class AddressableScriptableObjectDataProcessor : IProcessSequence, IProcessSequenceData
    {
        private readonly Type _desiredDataType;
        private readonly string _dataConfigKey;
        private readonly bool _isAssignableFromDesiredType;
        
        public IGameData GameData { get; private set; }
        
        public AddressableScriptableObjectDataProcessor(string dataConfigKey, Type desiredDataType)
        {
            this._dataConfigKey = dataConfigKey;
            this._desiredDataType = desiredDataType;
            this._isAssignableFromDesiredType = typeof(IGameData).IsAssignableFrom(desiredDataType);
        }

        public async UniTask<bool> Process()
        {
            ScriptableObject result = await Addressables.LoadAssetAsync<ScriptableObject>(this._dataConfigKey);
            if (result && this._desiredDataType.IsInstanceOfType(result) && this._isAssignableFromDesiredType)
            {
                Debug.LogError($"Failed to load data from addressable: {this._dataConfigKey}");
                return false;
            }
            
            this.GameData = result as IGameData;
            Debug.Log($"Loaded data from addressable: {this._dataConfigKey}");
            return true;

        }
    }
}