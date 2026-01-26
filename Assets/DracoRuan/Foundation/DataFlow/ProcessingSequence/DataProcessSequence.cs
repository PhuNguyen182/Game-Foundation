namespace DracoRuan.Foundation.DataFlow.ProcessingSequence
{
    public struct DataProcessSequence
    {
        public readonly string DataKey;
        public readonly DataProcessorType DataProcessorType;
        
        public DataProcessSequence(string dataKey, DataProcessorType dataProcessorType)
        {
            DataKey = dataKey;
            DataProcessorType = dataProcessorType;
        }
    }
}
