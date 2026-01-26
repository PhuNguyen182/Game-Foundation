using System;
using DracoRuan.Foundation.DataFlow.LocalData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class ResourceScriptableObjectDataProcessor : IProcessSequence, IProcessSequenceData
    {
        private readonly Type _desiredDataType;
        private readonly string _dataConfigKey;
        
        public IGameData GameData { get; private set; }
        public bool IsFinished { get; private set; }
        
        public ResourceScriptableObjectDataProcessor(string dataConfigKey, Type desiredDataType)
        {
            this._dataConfigKey = dataConfigKey;
            this._desiredDataType = desiredDataType;
        }

        public bool Process()
        {
            Object data = Resources.Load(this._dataConfigKey);
            if (!data || !this._desiredDataType.IsInstanceOfType(data) ||
                !typeof(IGameData).IsAssignableFrom(this._desiredDataType)) 
                return false;
            
            this.GameData = data as IGameData;
            return true;
        }
    }
}
