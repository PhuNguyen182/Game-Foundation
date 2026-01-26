namespace DracoRuan.Foundation.DataFlow.ProcessingSequence
{
    public interface IDataSequenceProcessor
    {
        public IProcessSequence LatestProcessSequence { get; }
        public IDataSequenceProcessor Append(IProcessSequence processSequence);
        public void Execute();
    }
}