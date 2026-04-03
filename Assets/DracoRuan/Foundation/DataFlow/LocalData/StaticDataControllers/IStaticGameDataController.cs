using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProcessors;
using DracoRuan.Foundation.DataFlow.MasterDataController;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public interface IStaticGameDataController : IDisposable
    {
        public UniTask InitializeData(IDataSequenceProcessor dataSequenceProcessor);
        public void InjectDataManager(IMainDataManager mainDataManager);
    }
}
