#if USE_EXTENDED_ADDRESSABLE
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.AssetBundleRuntime
{
    public class AddressableAssetBundleCleaner : IAssetBundleCleaner
    {
        private const string LogTag = "AddressableAssetBundleCleaner";
        
        private readonly IAssetBundleResourceLocator _resourceLocator;
        
        public AddressableAssetBundleCleaner(IAssetBundleResourceLocator resourceLocator)
            => this._resourceLocator = resourceLocator;

        public bool ClearAll()
        {
            try
            {
                bool isClearAll = Caching.ClearCache();
                if (isClearAll)
                    Debug.Log($"[{LogTag}] Cleared all cached asset bundles!");
                else
                    Debug.LogError($"[{LogTag}] Failed to clear all cached asset bundles!");
            
                return isClearAll;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during cache clear: {e.Message}");
            }

            return false;
        }

        public async UniTask ClearDependencyCacheBundles(string key, bool autoRelease = true)
        {
            if (!await this._resourceLocator.IsKeyValid(key))
            {
                Debug.LogError($"[{LogTag}] Addressable key or label '{key}' does not exist.");
                return;
            }

            AsyncOperationHandle<bool> clearDependencyCacheAsync = default;
            try
            {
                clearDependencyCacheAsync = Addressables.ClearDependencyCacheAsync(key, autoRelease);
                bool result = await clearDependencyCacheAsync;

                if (result)
                    Debug.Log($"[{LogTag}] Cleared cache for {key}!");
                else
                    Debug.LogError($"[{LogTag}] Failed to clear cache for {key}!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during cache clear: {e.Message}");
            }
            finally
            {
                if (!autoRelease && clearDependencyCacheAsync.IsValid())
                    Addressables.Release(clearDependencyCacheAsync);
            }
        }

        public async UniTask ClearDependencyCacheBundles(IEnumerable<string> keys, bool autoRelease = true)
        {
            List<string> keysList = keys.ToList();
            if (!await this._resourceLocator.IsKeyValid(keysList))
            {
                Debug.LogError($"[{LogTag}] Addressable key or label '{keysList}' does not exist.");
                return;
            }

            AsyncOperationHandle<bool> clearDependencyCacheAsync = default;
            try
            {
                clearDependencyCacheAsync = Addressables.ClearDependencyCacheAsync(keysList, autoRelease);
                bool result = await clearDependencyCacheAsync;

                if (result)
                    Debug.Log($"[{LogTag}] Cleared cache for {string.Join(", ", keysList)}!");
                else
                    Debug.LogError($"[{LogTag}] Failed to clear cache for {string.Join(", ", keysList)}!");

                if (!autoRelease)
                    Addressables.Release(clearDependencyCacheAsync);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during cache clear: {e.Message}");
            }
            finally
            {
                if (!autoRelease && clearDependencyCacheAsync.IsValid())
                    Addressables.Release(clearDependencyCacheAsync);
            }
        }

        public async UniTask ClearCachedAssetBundles(IEnumerable<string> catalogIds = null)
        {
            AsyncOperationHandle<bool> clearDependencyCacheAsync = default;
            try
            {
                clearDependencyCacheAsync = catalogIds != null
                    ? Addressables.CleanBundleCache(catalogIds)
                    : Addressables.CleanBundleCache();
                bool result = await clearDependencyCacheAsync;

                if (result)
                    Debug.Log($"[{LogTag}] Cleared all cached asset bundles.");
                else
                    Debug.LogError($"[{LogTag}] Failed to clear all cached asset bundles.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during cache clear: {e.Message}");
            }
            finally
            {
                if (clearDependencyCacheAsync.IsValid())
                    Addressables.Release(clearDependencyCacheAsync);
            }
        }
    }
}
#endif
