#if USE_EXTENDED_ADDRESSABLE
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.AssetBundleRuntime
{
    public class AddressableAssetBundleUpdater : IAssetBundleUpdater
    {
        private const string LogTag = "AddressableAssetBundleUpdater";
        
        private readonly IAssetBundleCleaner _assetBundleCleaner;

        public AddressableAssetBundleUpdater(IAssetBundleCleaner assetBundleCleaner)
            => _assetBundleCleaner = assetBundleCleaner;

        public async UniTask<List<string>> CheckForUpdates()
        {
            AsyncOperationHandle<List<string>> handle = default;
            try
            {
                handle = Addressables.CheckForCatalogUpdates();
                List<string> catalogUpdateKeys = await handle;
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[{LogTag}] Catalog update check completed successfully. Updated catalog keys: {catalogUpdateKeys}");
                    return catalogUpdateKeys;
                }
                
                Debug.LogError($"[{LogTag}] Catalog update check failed or nothing to update.");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during catalog update check: {e.Message}");
                return null;
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        public async UniTask UpdateCatalogs(bool autoCleanBundleCached = true, bool autoRelease = true,
            bool wantToPreserve = false, Action onUpdateComplete = null, Action onUpdateFailed = null)
        {
            AsyncOperationHandle<List<IResourceLocator>> handle = default;
            try
            {
                List<string> catalogUpdateKeys = await this.CheckForUpdates();
                handle = Addressables.UpdateCatalogs(autoCleanBundleCached, catalogUpdateKeys, autoRelease);
                await handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[{LogTag}] Updated catalogs successfully.");
                    if (!autoCleanBundleCached)
                    {
                        List<string> preserveUpdateKeys = wantToPreserve ? catalogUpdateKeys : null;
                        await this._assetBundleCleaner.ClearCachedAssetBundles(preserveUpdateKeys);
                    }

                    onUpdateComplete?.Invoke();
                }
                else
                {
                    Debug.Log($"[{LogTag}] Failed to update catalogs or nothing to update.");
                    onUpdateFailed?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during catalog update: {e.Message}");
                onUpdateFailed?.Invoke();
            }
            finally
            {
                if (!autoRelease && handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        public void Dispose()
        {
        }
    }
}
#endif
