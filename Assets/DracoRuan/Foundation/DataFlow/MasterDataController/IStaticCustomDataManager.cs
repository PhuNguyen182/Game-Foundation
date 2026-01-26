using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public interface IStaticCustomDataManager : ICustomDataManager
    {
        public TDataController GetDataHandler<TDataController>() where TDataController : class, IStaticGameDataController;
    }
}