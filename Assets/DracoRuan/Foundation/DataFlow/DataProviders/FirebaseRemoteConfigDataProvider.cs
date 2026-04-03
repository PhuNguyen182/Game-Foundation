using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.Serialization;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class FirebaseRemoteConfigDataProvider : IDataProvider
    {
        public async UniTask<TData> LoadDataAsync<TData>(string pathToData, IDataSerializer<TData> serializer = null,
            IDataSaveService dataSaveService = null)
        {
            // TODO: Get data from Firebase Remote Config
            await UniTask.CompletedTask;
            return default;
        }

        public void UnloadData<TData>(TData data)
        {
            
        }
    }
}
