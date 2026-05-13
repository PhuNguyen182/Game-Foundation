using System;

namespace DracoRuan.Foundation.DataFlow.LocalData.DynamicDataControllers
{
    public interface IDynamicGameDataController : IDisposable, IInitializableDataController
    {
        public int CurrentDataVersion { get; }
        public Type SourceDataType { get; }
        
        public void SaveData();
        public void DeleteData();
    }
}
