using System.Collections.Generic;

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
        
        public void Execute()
        {
            foreach (IProcessSequence processSequence in _processSequences)
            {
                processSequence.Process();
                this.LatestProcessSequence = processSequence;
                if (processSequence.IsFinished)
                    break;
            }
            
            this._processSequences.Clear();
        }
    }
}
