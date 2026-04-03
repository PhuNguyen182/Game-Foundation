using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.DataProcessors
{
    public interface IDataSequenceProcessor
    {
        public IProcessSequence LatestProcessSequence { get; }
        public IDataSequenceProcessor Append(IProcessSequence processSequence);
        public UniTask Execute();
        public void Clear();
    }
}