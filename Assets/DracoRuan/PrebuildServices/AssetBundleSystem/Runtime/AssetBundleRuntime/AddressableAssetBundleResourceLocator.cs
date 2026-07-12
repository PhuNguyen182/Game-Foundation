#if USE_EXTENDED_ADDRESSABLE
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.AssetBundleRuntime
{
    public class AddressableAssetBundleResourceLocator : IAssetBundleResourceLocator
    {
        private const string LogTag = "AddressableAssetBundleResourceLocator";
        
        public async UniTask<bool> IsKeyValid(string key)
        {
            bool exists = await KeyOrLabelExistsAsync(key);
            return exists;
        }

        public async UniTask<bool> IsKeyValid(List<string> keys)
        {
            bool exists = await KeyOrLabelExistsAsync(keys);
            return exists;
        }

        public async UniTask<long> GetDownloadSize(string key)
        {
            bool keyExists = await IsKeyValid(key);
            if (!keyExists)
            {
                Debug.LogError($"[{LogTag}] Addressable key '{key}' does not exist.");
                return -1;
            }
            
            long downloadSize = await GetDownloadSizeAsync(key);
            return downloadSize;
        }

        public async UniTask<long> GetDownloadSize(List<string> keys)
        {
            bool keyExists = await IsKeyValid(keys);
            if (!keyExists)
            {
                Debug.LogError($"[{LogTag}] Addressable key '{string.Join(", ", keys)}' does not exist.");
                return -1;
            }
            
            long downloadSize = await GetDownloadSizeAsync(keys);
            return downloadSize;
        }

        private static async UniTask<long> GetDownloadSizeAsync(object key)
        {
            AsyncOperationHandle<long> sizeHandle = default;
            AsyncOperationHandle<IList<IResourceLocation>> locationHandle = default;
            try
            {
                locationHandle = Addressables.LoadResourceLocationsAsync(key);
                IList<IResourceLocation> locations = await locationHandle;

                if (locationHandle.Status != AsyncOperationStatus.Succeeded || locations is not { Count: > 0 })
                {
                    Debug.LogError($"[{LogTag}] Failed to get resource locations for {key}");
                    return -1;
                }

                sizeHandle = Addressables.GetDownloadSizeAsync(locations);
                long sizeToDownload = await sizeHandle;
                if (sizeHandle.Status != AsyncOperationStatus.Succeeded) 
                    return -1;
                
                Debug.Log($"[{LogTag}] Download size for {key}: {sizeToDownload}");
                return sizeToDownload;

            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during download size calculation: {e.Message}");
                return -1;
            }
            finally
            {
                if (locationHandle.IsValid())
                    Addressables.Release(locationHandle);
                if (sizeHandle.IsValid())
                    Addressables.Release(sizeHandle);
            }
        }

        private static async UniTask<bool> KeyOrLabelExistsAsync(object keyOrLabel)
        {
            AsyncOperationHandle<IList<IResourceLocation>> handle = default;
            try
            {
                handle = Addressables.LoadResourceLocationsAsync(keyOrLabel);
                await handle;
                bool exists = handle is { Status: AsyncOperationStatus.Succeeded, Result: { Count: > 0 } };

                if (!exists)
                    Debug.LogError($"[{LogTag}] Addressable key or label {keyOrLabel} not exist!");
                else
                    Debug.Log($"[{LogTag}] Addressable key or label {keyOrLabel} exist!");
                
                return exists;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during key or label existence check: {e.Message}");
                return false;
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }
    }
}
#endif
