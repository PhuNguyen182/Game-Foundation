using System;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers
{
    public interface IDynamicGameDataController : IDisposable, IInitializableDataController
    {
        public int DataVersion { get; }
        public Type SourceDataType { get; }
        
        public UniTask LoadData();
        public UniTask SaveDataAsync();
        public void SaveData();
        public void DeleteData();
    }
}
