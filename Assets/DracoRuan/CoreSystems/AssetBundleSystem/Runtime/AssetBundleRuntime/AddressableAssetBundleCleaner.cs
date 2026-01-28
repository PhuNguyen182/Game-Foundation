#if USE_EXTENDED_ADDRESSABLE
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.AssetBundleRuntime
{
    public class AddressableAssetBundleCleaner : IAssetBundleCleaner
    {
        private readonly IAssetBundleResourceLocator _resourceLocator;
        
        public AddressableAssetBundleCleaner(IAssetBundleResourceLocator resourceLocator)
            => this._resourceLocator = resourceLocator;

        public bool ClearAll()
        {
            bool isClearAll = Caching.ClearCache();
            if (isClearAll)
                Debug.Log("Cleared all cached asset bundles!");
            else
                Debug.LogError("Failed to clear all cached asset bundles!");
            
            return isClearAll;
        }

        public async UniTask ClearDependencyCacheBundles(string key, bool autoRelease = true)
        {
            if (!await this._resourceLocator.IsKeyValid(key))
            {
                Debug.LogError($"Addressable key or label '{key}' does not exist.");
                return;
            }

            try
            {
                AsyncOperationHandle<bool> clearDependencyCacheAsync =
                    Addressables.ClearDependencyCacheAsync(key, autoRelease);
                bool result = await clearDependencyCacheAsync;

                if (result)
                    Debug.Log($"Cleared cache for {key}!");
                else
                    Debug.LogError($"Failed to clear cache for {key}!");

                if (!autoRelease)
                    Addressables.Release(clearDependencyCacheAsync);
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception during cache clear: {e.Message}");
            }
        }

        public async UniTask ClearDependencyCacheBundles(IEnumerable<string> keys, bool autoRelease = true)
        {
            List<string> keysList = keys.ToList();
            if (!await this._resourceLocator.IsKeyValid(keysList))
            {
                Debug.LogError($"Addressable key or label '{keysList}' does not exist.");
                return;
            }

            try
            {
                AsyncOperationHandle<bool> clearDependencyCacheAsync =
                    Addressables.ClearDependencyCacheAsync(keysList, autoRelease);
                bool result = await clearDependencyCacheAsync;

                if (result)
                    Debug.Log($"Cleared cache for {string.Join(", ", keysList)}!");
                else
                    Debug.LogError($"Failed to clear cache for {string.Join(", ", keysList)}!");

                if (!autoRelease)
                    Addressables.Release(clearDependencyCacheAsync);
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception during cache clear: {e.Message}");
            }
        }

        public async UniTask ClearCachedAssetBundles(IEnumerable<string> catalogIds = null)
        {
            try
            {
                AsyncOperationHandle<bool> clearDependencyCacheAsync = catalogIds != null
                    ? Addressables.CleanBundleCache(catalogIds)
                    : Addressables.CleanBundleCache();
                bool result = await clearDependencyCacheAsync;

                if (result)
                    Debug.Log("Cleared all cached asset bundles.");
                else
                    Debug.LogError("Failed to clear all cached asset bundles.");

                Addressables.Release(clearDependencyCacheAsync);
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception during cache clear: {e.Message}");
            }
        }
    }
}
#endif
