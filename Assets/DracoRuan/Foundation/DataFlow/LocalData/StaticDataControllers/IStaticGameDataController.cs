using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProcessors;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public interface IStaticGameDataController : IDisposable, IInitializableDataController
    {
        public Type SourceDataType { get; }
        public event Action OnDataLoaded;

        public UniTask InitializeData(IDataSequenceProcessor dataSequenceProcessor);
    }
}