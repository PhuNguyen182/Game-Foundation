using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers.CSVs
{
    public class CustomRecordData<TRecord> : ISetCustomCsvRecordGameData<TRecord>
    {
        public IEnumerable<TRecord> Records { get; private set; }

        public void SetCustomGameDataRecords(IEnumerable<TRecord> recordDataFromCsv)
        {
            this.Records = recordDataFromCsv;
        }
    }
}