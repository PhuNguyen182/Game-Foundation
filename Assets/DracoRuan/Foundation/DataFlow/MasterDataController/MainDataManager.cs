using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers;
using DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers;
using VContainer.Unity;

namespace DracoRuan.Foundation.DataFlow.MasterDataController
{
    public class MainDataManager : IMainDataManager, IInitializable
    {
        private bool _isDisposed;
        private readonly IStaticCustomDataManager _staticCustomDataManager;
        private readonly IDynamicCustomDataManager _dynamicCustomDataManager;

        public MainDataManager(IStaticCustomDataManager staticCustomDataManager,
            IDynamicCustomDataManager dynamicCustomDataManager)
        {
            this._staticCustomDataManager = staticCustomDataManager;
            this._dynamicCustomDataManager = dynamicCustomDataManager;
        }

        public void Initialize()
        {
            
        }

        public TStaticGameDataHandler GetStaticDataController<TStaticGameDataHandler>()
            where TStaticGameDataHandler : class, IStaticGameDataController
            => this._staticCustomDataManager?.GetDataHandler<TStaticGameDataHandler>();

        public TDynamicGameDataHandler GetDynamicDataController<TDynamicGameDataHandler>()
            where TDynamicGameDataHandler : class, IDynamicGameDataController =>
            this._dynamicCustomDataManager?.GetDataHandler<TDynamicGameDataHandler>();
        
        public UniTask SaveAllDataAsync() =>
            this._dynamicCustomDataManager?.SaveAllDataAsync() ?? UniTask.CompletedTask;
        
        public void SaveAllData() => this._dynamicCustomDataManager?.SaveAllData();
        
        public void DeleteSingleData(Type dataType) => this._dynamicCustomDataManager?.DeleteSingleData(dataType);
        
        public void DeleteAllData() => this._dynamicCustomDataManager?.DeleteAllData();
    }
}
