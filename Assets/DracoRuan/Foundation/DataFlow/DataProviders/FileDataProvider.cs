using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.Serialization;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class FileDataProvider : IDataProvider, IDataSaver
    {
        public async UniTask<TData> LoadDataAsync<TData>(string pathToData, 
            IDataSerializer<TData> serializer = null, IDataSaveService dataSaveService = null)
        {
            try
            {
                if (dataSaveService == null)
                {
                    Debug.LogError($"[FileDataProvider] [{typeof(TData)}] No save service provided for this type");
                    return default;
                }

                if (serializer == null)
                {
                    Debug.LogError($"[FileDataProvider] [{typeof(TData)}] No serializer provided for this type");
                    return default;
                }
                
                string serializedData = await dataSaveService.LoadData(pathToData);
                TData result = serializer.Deserialize(serializedData);
                Debug.Log($"[FileDataProvider] [{typeof(TData)}] Loaded data from path: {pathToData} successfully !!!");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[FileDataProvider] [{typeof(TData)}] Failed to load data from path: {pathToData}. More info: {e.Message}");
            }
            
            return default;
        }

        public void UnloadData<TData>(TData data)
        {
            
        }

        public void SaveData<TData>(TData data, string pathToData, 
            IDataSerializer<TData> dataSerializer = null, IDataSaveService dataSaveService = null)
        {
            try
            {
                if (dataSaveService == null)
                {
                    Debug.LogError($"[FileDataProvider] [{typeof(TData)}] No save service provided for this type");
                    return;
                }

                if (dataSerializer == null)
                {
                    Debug.LogError($"[FileDataProvider] [{typeof(TData)}] No serializer provided for this type");
                    return;
                }

                object serializedData = dataSerializer.Serialize(data);
                dataSaveService.SaveData(pathToData, serializedData);
                Debug.Log($"[FileDataProvider] [{typeof(TData)}] Saved data to path: {pathToData} successfully !!!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileDataProvider] [{typeof(TData)}] Failed to save data to path: {pathToData}. More info: {e.Message}");
            }
        }
    }
}
