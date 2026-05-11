using System;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers
{
    public interface IDynamicGameDataController : IDisposable, IInitializableDataController
    {
        public int CurrentDataVersion { get; }
        public Type SourceDataType { get; }
        
        public UniTask LoadData();
        public void SaveData();
        public void DeleteData();
    }
}
