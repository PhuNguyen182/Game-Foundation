#if USE_EXTENDED_ADDRESSABLE
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.AssetBundleRuntime
{
    public class AddressableAssetBundleDownloader : IAssetBundleDownloader
    {
        private const string LogTag = "AddressableAssetBundleDownloader";
        
        private readonly IAssetBundleCleaner _bundleCleaner;
        private readonly IAssetBundleResourceLocator _bundleResourceLocator;

        public AddressableAssetBundleDownloader(IAssetBundleResourceLocator bundleResourceLocator,
            IAssetBundleCleaner bundleCleaner)
        {
            this._bundleCleaner = bundleCleaner;
            this._bundleResourceLocator = bundleResourceLocator;
        }

        public async UniTask DownloadAsset(string key, bool autoRelease = true, bool clearCacheAfterDownload = true
            , Action<float> onProgression = null, Action onDownloadComplete = null, Action onDownloadFailed = null)
        {
            long downloadSize = await this._bundleResourceLocator.GetDownloadSize(key);
            Debug.Log($"[{LogTag}] Download size: {downloadSize}");

            if (downloadSize <= 0)
            {
                Debug.LogError($"[{LogTag}] There is nothing to download for key: {key}.");
                return;
            }

            await this.DownloadBundleAsync(key, autoRelease, clearCacheAfterDownload, onProgression, onDownloadComplete,
                onDownloadFailed);
        }

        public async UniTask DownloadAsset(List<string> keys, bool autoRelease = true, bool clearCacheAfterDownload = true,
            Action<float> onProgression = null, Action onDownloadComplete = null, Action onDownloadFailed = null)
        {
            long downloadSize = await this._bundleResourceLocator.GetDownloadSize(keys);
            Debug.Log($"[{LogTag}] Download size: {downloadSize} bytes");

            if (downloadSize <= 0)
            {
                Debug.LogError($"[{LogTag}] There is nothing to download for key: {string.Join(", ", keys)}.");
                return;
            }

            await this.DownloadBundleAsync(keys, autoRelease, clearCacheAfterDownload, onProgression, onDownloadComplete,
                onDownloadFailed);
        }

        private async UniTask DownloadBundleAsync(object key, bool autoRelease = true, bool clearCacheAfterDownload = true,
            Action<float> onProgression = null, Action onDownloadComplete = null, Action onDownloadFailed = null)
        {
            AsyncOperationHandle downloadHandle = default;
            try
            {
                downloadHandle = Addressables.DownloadDependenciesAsync(key, autoRelease);
                while (!downloadHandle.IsDone)
                {
                    onProgression?.Invoke(downloadHandle.PercentComplete);
                    await UniTask.NextFrame();
                }

                if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[{LogTag}] Downloaded remote bundle(s) for {key} successfully.");
                    onProgression?.Invoke(1.0f);
                    onDownloadComplete?.Invoke();
                    
                    if (clearCacheAfterDownload) // Clear cache after download
                    {
                        switch (key)
                        {
                            case string clearKey:
                                await this._bundleCleaner.ClearDependencyCacheBundles(clearKey);
                                break;
                            case List<string> clearKeys:
                                await this._bundleCleaner.ClearDependencyCacheBundles(clearKeys);
                                break;
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[{LogTag}] Failed to download remote bundle(s) for {key}.");
                    onDownloadFailed?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Exception during bundle download: {e.Message}");
                onDownloadFailed?.Invoke();
            }
            finally
            {
                if (!autoRelease && downloadHandle.IsValid())
                    Addressables.Release(downloadHandle);
            }
        }
    }
}
#endif
