using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.DataProcessors
{
    public class DataSequenceProcessor : IDataSequenceProcessor, IDisposable

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
            foreach (IProcessSequence processSequence in this._processSequences)
            {
                bool isSuccess = await processSequence.Process();
                this.LatestProcessSequence = processSequence;
                if (isSuccess)
                    break;
            }

            this._processSequences.Clear();
        }

        public void Clear()
        {
            this._processSequences.Clear();
            this.LatestProcessSequence = null;
        }

        private void ReleaseUnmanagedResources()
        {
            this.Clear();
        }

        public void Dispose()
        {
            this.ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~DataSequenceProcessor()
        {
            this.ReleaseUnmanagedResources();
        }
    }
}
