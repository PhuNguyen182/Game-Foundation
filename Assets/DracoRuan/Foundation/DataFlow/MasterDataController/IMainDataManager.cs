using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.DataProviders;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public interface IMainDataManager : IDisposable
    {
        public bool IsInitialized { get; }
        public IDataProviderService DataProviderService { get; }
        
        public TStaticGameDataController GetStaticDataController<TStaticGameDataController>()
            where TStaticGameDataController : class, IStaticGameDataController;

        public TDynamicGameDataController GetDynamicDataController<TDynamicGameDataController>()
            where TDynamicGameDataController : class, IDynamicGameDataController;

        public void SaveAllData();
        public UniTask SaveAllDataAsync();
        
        public void DeleteSingleData(Type dataType);
        public void DeleteAllData();
    }
}