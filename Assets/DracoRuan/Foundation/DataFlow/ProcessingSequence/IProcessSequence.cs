using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence
{
    public interface IProcessSequence
    {
        public UniTask<bool> Process();
    }
    
    public interface IProcessSequenceData
    {
        public IGameData GameData { get; }
    }
}