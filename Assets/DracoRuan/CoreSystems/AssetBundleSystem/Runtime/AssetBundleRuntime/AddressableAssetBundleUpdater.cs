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
        private readonly IAssetBundleCleaner _assetBundleCleaner;
        private AsyncOperationHandle<List<string>> _catalogUpdates;
        private AsyncOperationHandle<List<IResourceLocator>> _bundleUpdates;

        public AddressableAssetBundleUpdater(IAssetBundleCleaner assetBundleCleaner)
            => _assetBundleCleaner = assetBundleCleaner;

        public async UniTask<List<string>> CheckForUpdates()
        {
            this._catalogUpdates = Addressables.CheckForCatalogUpdates();
            List<string> catalogUpdateKeys = await this._catalogUpdates;
            return catalogUpdateKeys;
        }

        public async UniTask UpdateCatalogs(bool autoCleanBundleCached = true, bool autoRelease = true,
            bool wantToPreserve = false, Action onUpdateComplete = null, Action onUpdateFailed = null)
        {
            try
            {
                List<string> catalogUpdateKeys = await this.CheckForUpdates();
                this._bundleUpdates =
                    Addressables.UpdateCatalogs(autoCleanBundleCached, catalogUpdateKeys, autoRelease);
                await this._bundleUpdates;

                if (this._bundleUpdates.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log("Updated catalogs successfully.");
                    if (!autoCleanBundleCached)
                    {
                        List<string> preserveUpdateKeys = wantToPreserve ? catalogUpdateKeys : null;
                        await this._assetBundleCleaner.ClearCachedAssetBundles(preserveUpdateKeys);
                    }

                    onUpdateComplete?.Invoke();
                }
                else
                {
                    Debug.Log("Failed to update catalogs.");
                    onUpdateFailed?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception during catalog update: {e.Message}");
                onUpdateFailed?.Invoke();
            }
        }

        public void Dispose()
        {
            if (this._catalogUpdates.IsValid())
                Addressables.Release(this._catalogUpdates);

            if (this._bundleUpdates.IsValid())
                Addressables.Release(this._bundleUpdates);
        }
    }
}
#endif
