using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.MasterDataController;

namespace DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers
{
    public interface IDynamicGameDataController : IDisposable
    {
        public void Initialize();
        public void InjectDataManager(IMainDataManager mainDataManager);
        public UniTask LoadData();
        public UniTask SaveDataAsync();
        public void SaveData();
        public void DeleteData();
    }
}
