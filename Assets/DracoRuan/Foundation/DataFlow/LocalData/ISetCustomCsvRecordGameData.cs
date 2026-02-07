using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.LocalData
{
    public interface ISetCustomCsvRecordGameData
    {
        public void SetCustomGameDataRecords<TData>(IEnumerable<TData> recordDataFromCsv);
    }
}