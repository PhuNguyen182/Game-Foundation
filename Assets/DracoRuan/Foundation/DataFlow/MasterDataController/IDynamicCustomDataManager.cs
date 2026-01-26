using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public interface IDynamicCustomDataManager : ICustomDataManager
    {
        public TDataController GetDataHandler<TDataController>() where TDataController : class, IDynamicGameDataController;
        public void SaveAllData();
        public void DeleteAllData();
        public void DeleteSingleData(Type dataType);
        public UniTask SaveAllDataAsync();
    }
}