using System;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public interface IMainDataManager
    {
        public TStaticGameDataController GetStaticDataController<TStaticGameDataController>()
            where TStaticGameDataController : class, IStaticGameDataController;

        public TDynamicGameDataController GetDynamicDataController<TDynamicGameDataController>()
            where TDynamicGameDataController : class, IDynamicGameDataController;

        public void SaveAllData();
        public void DeleteSingleData(Type dataType);
        public void DeleteAllData();
    }
}