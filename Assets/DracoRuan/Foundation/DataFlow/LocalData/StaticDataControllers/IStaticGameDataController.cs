using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.MasterDataController;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public interface IStaticGameDataController : IDisposable
    {
        public UniTask Initialize();
        public void InjectDataManager(IMainDataManager mainDataManager);
    }
}
