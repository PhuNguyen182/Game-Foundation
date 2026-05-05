using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProcessors;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public interface IStaticGameDataController : IDisposable, IInitializableDataController
    {
        public int DataVersion { get; }
        public Type SourceDataType { get; }
        
        public UniTask InitializeData(IDataSequenceProcessor dataSequenceProcessor);
    }
}
