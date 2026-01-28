#if USE_EXTENDED_ADDRESSABLE
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces
{
    public interface IAssetBundleDownloader
    {
        /// <summary>
        /// This function will download the asset bundle from a single.
        /// </summary>
        /// <param name="key">Key to download, it should be the label of asset bundle</param>
        /// <param name="autoRelease">Should this handle automatic release after done</param>
        /// <param name="clearCacheAfterDownload">Should the old cached bundle be removed</param>
        /// <param name="onProgression">Callback running of progression</param>
        /// <param name="onDownloadComplete">Callback called if the process is completed</param>
        /// <param name="onDownloadFailed">Callback called if the process is failed</param>
        /// <returns></returns>
        public UniTask DownloadAsset(string key, bool autoRelease = true, bool clearCacheAfterDownload = true, 
            Action<float> onProgression = null, Action onDownloadComplete = null, Action onDownloadFailed = null);

        /// <summary>
        /// This function will download the asset bundle from a single.
        /// </summary>
        /// <param name="keys">Keys to download, it should be the labels of asset bundles</param>
        /// <param name="autoRelease">Should this handle automatic release after done</param>
        /// <param name="clearCacheAfterDownload">Should the old cached bundle be removed</param>
        /// <param name="onProgression">Callback running of progression</param>
        /// <param name="onDownloadComplete">Callback called if the process is completed</param>
        /// <param name="onDownloadFailed">Callback called if the process is failed</param>
        /// <returns></returns>
        public UniTask DownloadAsset(List<string> keys, bool autoRelease = true, bool clearCacheAfterDownload = true,
            Action<float> onProgression = null, Action onDownloadComplete = null, Action onDownloadFailed = null);
    }
}
#endif
