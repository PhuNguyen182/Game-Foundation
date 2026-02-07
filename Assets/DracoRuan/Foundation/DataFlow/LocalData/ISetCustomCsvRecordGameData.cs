using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.LocalData
{
    public interface ISetCustomCsvRecordGameData<in TData>
    {
        public void SetCustomGameDataRecords(IEnumerable<TData> recordDataFromCsv);
    }
}