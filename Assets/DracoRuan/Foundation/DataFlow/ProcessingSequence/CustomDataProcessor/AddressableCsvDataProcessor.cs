using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class AddressableCsvDataProcessor<TData> : IProcessSequence, IProcessSequenceData
        where TData : class, IGameData, ISetCustomCsvRecordGameData, new()
    {
        private readonly string _dataConfigKey;
        
        public AddressableCsvDataProcessor(string dataConfigKey)
        {
            this._dataConfigKey = dataConfigKey;
        }

        public IGameData GameData { get; private set; }

        public async UniTask<bool> Process()
        {
            Object csvText = await Resources.LoadAsync<TextAsset>(_dataConfigKey);
            TextAsset textAsset = csvText as TextAsset;
            if (!textAsset || string.IsNullOrEmpty(textAsset.text))
                return false;

            try
            {
                string output = textAsset.text ?? string.Empty;
                IEnumerable<TData> dataRecords = CsvHelperUtil<TData>.ParseCsv(output);
                if (dataRecords == null)
                    return false;

                this.GameData = new TData();
                if (this.GameData is not ISetCustomCsvRecordGameData customGameDataSetter)
                    return false;

                customGameDataSetter.SetCustomGameDataRecords(dataRecords);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceCsvDataProcessor] Failed to load data from path: {_dataConfigKey}. More info: {e.Message}");
            }
            finally
            {
                Resources.UnloadAsset(textAsset);
            }
            
            return false;
        }
    }
}