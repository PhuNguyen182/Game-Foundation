using System;
using System.Collections.Generic;
using CsvHelper.Configuration;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers.CSVs;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.DataProcessors
{
    public class DataProcessorWithRecord<TData, TRecord, TRecordMap> : IProcessSequence, IProcessSequenceData
        where TData : CustomRecordData<TRecord>, IGameData, new()
        where TRecord : class
        where TRecordMap : ClassMap<TRecord>
    {
        private readonly string _dataConfigKey;
        private readonly IDataProvider _dataProvider;
        
        public IGameData GameData { get; private set; }
        
        public DataProcessorWithRecord(string dataConfigKey, IDataProvider dataProvider)
        {
            this._dataConfigKey = dataConfigKey;
            this._dataProvider = dataProvider;
        }

        public async UniTask<bool> Process()
        {
            TextAsset textAsset = await this._dataProvider.LoadDataAsync<TextAsset>(this._dataConfigKey);
            if (!textAsset || string.IsNullOrEmpty(textAsset.text))
                return false;

            try
            {
                string output = textAsset.text ?? string.Empty;
                IEnumerable<TRecord> dataRecords = CsvHelperUtil<TRecord, TRecordMap>.GetRecordsFromCsv(output);
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
                Debug.LogError($"Failed to load data from path: {_dataConfigKey}. More info: {e.Message}");
            }
            
            return false;
        }
    }
}