using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.Serialization;
using DracoRuan.Foundation.DataFlow.TypeCreator;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class PlayerPrefDataProvider : IDataProvider, IDataSaver
    {
        public async UniTask<TData> LoadDataAsync<TData>(string pathToData, 
            IDataSerializer<TData> serializer = null, IDataSaveService dataSaveService = null)
        {
            try
            {
                if (serializer == null)
                {
                    Debug.LogError($"[PlayerPrefProvider] [{typeof(TData)}] No serializer provided for this type");
                    return default;
                }

                if (dataSaveService == null)
                {
                    Debug.LogError($"[PlayerPrefProvider] [{typeof(TData)}] No save service provided for this type");
                    return default;
                }
                
                byte[] savedSerializedData = dataSaveService.LoadData(pathToData);
                if (savedSerializedData != null)
                {
                    TData result = serializer.Deserialize(savedSerializedData);
                    Debug.Log($"[PlayerPrefProvider] [{typeof(TData)}] Loaded data from path: {pathToData} successfully !!!");
                    return result;
                }

                Debug.Log($"[PlayerPrefProvider] [{typeof(TData)}] This data is empty, create new one now!");
                TData newData = TypeFactory.Create<TData>();
                this.SaveData(newData, pathToData, serializer, dataSaveService);
                return newData;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[PlayerPrefProvider] [{typeof(TData)}] Failed to load data from path: {pathToData}. More info: {e.Message}");
            }

            await UniTask.CompletedTask;
            return default;
        }
        
        public byte[] LoadRawDataAsync(string pathToData, IDataSaveService dataSaveService = null)
        {
            byte[] savedSerializedData = dataSaveService?.LoadData(pathToData);
            return savedSerializedData;
        }

        public void UnloadData<TData>(TData data)
        {
            
        }

        public void SaveData<TData>(TData data, string pathToData, IDataSerializer<TData> dataSerializer = null,
            IDataSaveService dataSaveService = null)
        {
            try
            {
                if (dataSaveService == null)
                {
                    Debug.LogError($"[PlayerPrefProvider] [{typeof(TData)}] No save service provided for this type");
                    return;
                }

                if (dataSerializer == null)
                {
                    Debug.LogError($"[PlayerPrefProvider] [{typeof(TData)}] No serializer provided for this type");
                    return;
                }

                object serializedData = dataSerializer.Serialize(data);
                dataSaveService.SaveData(pathToData, serializedData as byte[]);
                Debug.Log($"[PlayerPrefProvider] [{typeof(TData)}] Saved data to path: {pathToData} successfully !!!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerPrefProvider] [{typeof(TData)}] Failed to save data to path: {pathToData}. More info: {e.Message}");
            }
        }
    }
}
