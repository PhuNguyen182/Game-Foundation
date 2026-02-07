using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class ResourcesDataProvider : IDataProvider
    {
        public async UniTask<TData> LoadDataAsync<TData>(string pathToData, 
            IDataSerializer<TData> serializer = null, IDataSaveService dataSaveService = null)
        {
            Object asset = null;
            try
            {
                asset = await Resources.LoadAsync(pathToData);
                if (asset is TData result)
                {
                    Debug.Log(
                        $"[ResourcesProvider] [{typeof(TData)}] Loaded data from path: {pathToData} successfully !!!");
                    return result;
                }

                Debug.LogError($"[ResourcesProvider] [{typeof(TData)}] Data from {pathToData} is mismatched !!!");
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ResourcesProvider] [{typeof(TData)}] Failed to loaded target data from {pathToData}. More info: {e.Message}");
            }
            finally
            {
                if (asset)
                    Resources.UnloadAsset(asset);
            }

            return default;
        }

        public void UnloadData<TData>(TData data)
        {
            try
            {
                if (data is not Object obj)
                {
                    Debug.LogError(
                        $"[ResourcesProvider] [{typeof(TData)}] Data type {data.GetType().Name} is not a UnityEngine.Object.");
                    return;
                }

                Resources.UnloadAsset(obj);
                Debug.Log($"[ResourcesProvider] [{typeof(TData)}] Unload data {data.GetType().Name} by Resources");
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[ResourcesProvider] [{typeof(TData)}] Failed to unload data {data.GetType().Name} by Resources. More info: {e.Message}");
            }
        }
    }
}
