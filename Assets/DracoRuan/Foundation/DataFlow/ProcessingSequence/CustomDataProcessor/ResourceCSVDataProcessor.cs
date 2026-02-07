using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;
using CsvHelper;
using CsvHelper.Configuration;
using DracoRuan.Foundation.DataFlow.TypeCreator;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class ResourceCsvDataProcessor : IProcessSequence, IProcessSequenceData
    {
        private readonly string _dataConfigKey;
        private readonly Type _desiredDataType;
        private readonly MethodInfo _getRecordsMethodInfo;
        
        public ResourceCsvDataProcessor(string dataConfigKey, Type desiredDataType)
        {
            this._dataConfigKey = dataConfigKey;
            this._desiredDataType = desiredDataType;
            this._getRecordsMethodInfo = typeof(CsvReader).GetMethod("GetRecords", Type.EmptyTypes)
                ?.MakeGenericMethod(desiredDataType);
        }
        
        public IGameData GameData { get; private set; }

        public async UniTask<bool> Process()
        {
            var csvText = await Resources.LoadAsync<TextAsset>(this._dataConfigKey);
            TextAsset textAsset = csvText as TextAsset;
            CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };

            string output = textAsset?.text ?? string.Empty;
            using var reader = new StringReader(output);
            using var csv = new CsvReader(reader, config);
            var dataRecords = this._getRecordsMethodInfo?.Invoke(csv, null);
            if (dataRecords == null)
                return false;

            this.GameData = TypeFactory.Create(this._desiredDataType) as IGameData;
            if (this.GameData is not ISetCustomGameData customGameDataSetter) 
                return false;

            customGameDataSetter.SetCustomGameData(dataRecords);
            return true;
        }

        public class Foo : IGameData, ISetCustomGameData
        {
            public int Version { get; set; }
            public void SetCustomGameData(object gameData)
            {
                throw new NotImplementedException();
            }
        }
    }
}