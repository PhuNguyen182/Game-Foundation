using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using DracoRuan.Foundation.DataFlow.Serialization;
using UnityEngine.AddressableAssets;

namespace DracoRuan.Foundation.DataFlow.DataProviders
{
    public class AddressableDataProvider : IDataProvider
    {
        public async UniTask<TData> LoadDataAsync<TData>(string pathToData, 
            IDataSerializer<TData> serializer = null, IDataSaveService dataSaveService = null)
        {
            AsyncOperationHandle<TData> operationHandle = default;
            try
            {
                operationHandle = Addressables.LoadAssetAsync<TData>(pathToData);
                while (!operationHandle.IsDone)
                    await UniTask.NextFrame();

                if (operationHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    TData result = operationHandle.Result;
                    Debug.Log(
                        $"[AddressableProvider] [{typeof(TData)}] Loaded data from path: {pathToData} successfully !!! Result: {result}");
                    return result;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[AddressableProvider] [{typeof(TData)}] Failed to load data from path: {pathToData}. More info: {e.Message}");
            }
            finally
            {
                if (operationHandle.IsValid())
                    Addressables.Release(operationHandle);
            }

            return default;
        }

        public void UnloadData<TData>(TData data)
        {
            try
            {
                Addressables.Release(data);
                Debug.Log(
                    $"[AddressableProvider] [{typeof(TData)}] Unloaded data {data.GetType().Name} successfully !!!");
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[AddressableProvider] [{typeof(TData)}] Failed to unload data {data.GetType().Name}. More info: {e.Message}");
            }
        }
    }
}
