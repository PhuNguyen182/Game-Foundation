using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.MasterDataController;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers
{
    public interface IStaticGameDataController
    {
        public UniTask Initialize();
        public void InjectDataManager(IMainDataManager mainDataManager);
    }
}
