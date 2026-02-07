using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.LocalData;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public class SerializableRecordClass<TRecord> : ISetCustomCsvRecordGameData<TRecord>
    {
        public IEnumerable<TRecord> Records { get; private set; }

        public void SetCustomGameDataRecords(IEnumerable<TRecord> recordDataFromCsv)
        {
            this.Records = recordDataFromCsv;
        }
    }
}