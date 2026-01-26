using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence
{
    public class DataSequenceProcessor : IDataSequenceProcessor
    {
        private readonly Queue<IProcessSequence> _processSequences = new();
        public IProcessSequence LatestProcessSequence { get; private set; }

        public IDataSequenceProcessor Append(IProcessSequence processSequence)
        {
            this._processSequences.Enqueue(processSequence);
            return this;
        }
        
        public async UniTask Execute()
        {
            await UniTask.CompletedTask;
            foreach (IProcessSequence processSequence in _processSequences)
            {
                bool isSuccess = processSequence.Process();
                this.LatestProcessSequence = processSequence;
                if (isSuccess)
                    break;
            }
            
            this._processSequences.Clear();
        }
    }
}
