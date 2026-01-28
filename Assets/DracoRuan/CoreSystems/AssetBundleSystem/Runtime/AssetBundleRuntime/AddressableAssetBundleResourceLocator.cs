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
                Debug.LogError($"Addressable key '{key}' does not exist.");
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
                Debug.LogError($"Addressable key '{string.Join(", ", keys)}' does not exist.");
                return -1;
            }
            
            long downloadSize = await GetDownloadSizeAsync(keys);
            return downloadSize;
        }

        private static async UniTask<long> GetDownloadSizeAsync(object key)
        {
            try
            {
                AsyncOperationHandle<IList<IResourceLocation>> locationsHandle =
                    Addressables.LoadResourceLocationsAsync(key);
                IList<IResourceLocation> locations = await locationsHandle;

                if (locationsHandle.Status != AsyncOperationStatus.Succeeded || locations is not { Count: > 0 })
                {
                    Debug.LogError($"Failed to get resource locations for {key}");
                    Addressables.Release(locationsHandle);
                    return -1;
                }

                AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(locations);
                long sizeToDownload = await sizeHandle;
                Addressables.Release(locationsHandle);
                Addressables.Release(sizeHandle);
                return sizeToDownload;
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception during download size calculation: {e.Message}");
                return -1;
            }
        }

        private static async UniTask<bool> KeyOrLabelExistsAsync(object keyOrLabel)
        {
            try
            {
                AsyncOperationHandle<IList<IResourceLocation>> handle =
                    Addressables.LoadResourceLocationsAsync(keyOrLabel);
                await handle;
                bool exists = handle is { Status: AsyncOperationStatus.Succeeded, Result: { Count: > 0 } };

                if (!exists)
                    Debug.LogError($"Addressable key or label {keyOrLabel} not exist!");
                else
                    Debug.Log($"Addressable key or label {keyOrLabel} exist!");

                Addressables.Release(handle);
                return exists;
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception during key or label existence check: {e.Message}");
                return false;
            }
        }
    }
}
#endif
