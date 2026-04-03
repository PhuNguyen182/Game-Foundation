using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.Serialization;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class FirebaseRemoteConfigDataProvider : IDataProvider
    {
        private const string LogKey = "FirebaseRemoteConfigDataProvider";
        
        public async UniTask<TData> LoadDataAsync<TData>(string pathToData, IDataSerializer<TData> serializer = null,
            IDataSaveService dataSaveService = null)
        {
            try
            {
                if (serializer == null)
                {
                    Debug.LogError($"{LogKey} Serializer of type {typeof(IDataSerializer<TData>)} cannot be null.");
                    return default;
                }
                    
                TData data = serializer.Deserialize(pathToData);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            
            await UniTask.CompletedTask;
            return default;
        }

        public void UnloadData<TData>(TData data)
        {
            
        }
    }
}
