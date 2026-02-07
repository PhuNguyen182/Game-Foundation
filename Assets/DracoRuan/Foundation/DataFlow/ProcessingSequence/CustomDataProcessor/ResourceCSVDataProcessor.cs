using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.LocalData;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class ResourceCsvDataProcessor<TData, TRecord> : IProcessSequence, IProcessSequenceData
        where TData : SerializableRecordClass<TRecord>, IGameData, new()
    {
        private readonly string _dataConfigKey;
        private readonly IDataProvider _dataProvider;
        
        public IGameData GameData { get; private set; }

        public ResourceCsvDataProcessor(string dataConfigKey, IDataProviderService dataProviderService)
        {
            this._dataConfigKey = dataConfigKey;
            this._dataProvider = dataProviderService.GetDataProviderByType(DataProviderType.Resources);
        }

        public async UniTask<bool> Process()
        {
            TextAsset textAsset = await this._dataProvider.LoadDataAsync<TextAsset>(_dataConfigKey);
            if (!textAsset || string.IsNullOrEmpty(textAsset.text))
                return false;

            try
            {
                string output = textAsset.text ?? string.Empty;
                IEnumerable<TRecord> dataRecords = CsvHelperUtil<TRecord>.ParseCsv(output);
                if (dataRecords == null)
                    return false;

                this.GameData = new TData();
                if (this.GameData is not ISetCustomCsvRecordGameData<TRecord> customGameDataSetter)
                    return false;

                customGameDataSetter.SetCustomGameDataRecords(dataRecords);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceCsvDataProcessor] Failed to load data from path: {_dataConfigKey}. More info: {e.Message}");
            }
            
            return false;
        }
    }
}