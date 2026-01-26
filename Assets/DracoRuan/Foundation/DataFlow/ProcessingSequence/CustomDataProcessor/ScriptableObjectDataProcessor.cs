using System;
using DracoRuan.Foundation.DataFlow.LocalData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class ScriptableObjectDataProcessor : IProcessSequence, IProcessSequenceData
    {
        private readonly Type _desiredDataType;
        private readonly string _dataConfigKey;
        
        public IGameData GameData { get; private set; }
        public bool IsFinished { get; private set; }
        
        public ScriptableObjectDataProcessor(string dataConfigKey, Type desiredDataType)
        {
            this._dataConfigKey = dataConfigKey;
            this._desiredDataType = desiredDataType;
        }

        public void Process()
        {
            // Use load from the Resources folder to get the scriptable object for target config data
            Object data = Resources.Load(this._dataConfigKey);
            if (!data || !this._desiredDataType.IsInstanceOfType(data) ||
                !typeof(IGameData).IsAssignableFrom(this._desiredDataType)) 
                return;
            
            this.GameData = data as IGameData;
            this.IsFinished = true;
        }
    }
}
