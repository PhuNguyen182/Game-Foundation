using DracoRuan.Foundation.DataFlow.DataProviders;

namespace DracoRuan.Foundation.DataFlow.DataProcessors
{
    public struct DataProcessSequence
    {
        public readonly string DataKey;
        public readonly DataSourceType DataSourceType;
        
        public DataProcessSequence(string dataKey, DataSourceType dataSourceType)
        {
            this.DataKey = dataKey;
            this.DataSourceType = dataSourceType;
        }
    }
}
