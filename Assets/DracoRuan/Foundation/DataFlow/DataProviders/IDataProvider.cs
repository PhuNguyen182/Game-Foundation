using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.Serialization;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public interface IDataProvider
    {
        public UniTask<TData> LoadDataAsync<TData>(string pathToData,
            IDataSerializer<TData> serializer = null, IDataSaveService dataSaveService = null);

        public void UnloadData<TData>(TData data);
    }
    
    public interface IDataSaver
    {
        public void SaveData<TData>(TData data, string pathToData,
            IDataSerializer<TData> dataSerializer = null, IDataSaveService dataSaveService = null);
    }
}
