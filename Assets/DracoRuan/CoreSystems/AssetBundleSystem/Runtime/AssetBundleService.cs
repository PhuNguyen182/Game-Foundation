#if USE_EXTENDED_ADDRESSABLE
using System;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.AssetBundleRuntime;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime
{
    public class AssetBundleService : IAssetBundleService
    {
        private const string LogTag = "AssetBundleService";
        
        private AsyncOperationHandle<IResourceLocator> _initializeHandle;

        public IAssetBundleLoader AssetBundleLoader { get; }
        public IAssetBundleDownloader AssetBundleDownloader { get; }
        public IAssetBundleCleaner AssetBundleCleaner { get; }
        public IAssetBundleUpdater AssetBundleUpdater { get; }
        public IAssetBundleResourceLocator AssetBundleResourceLocator { get; }

        public AssetBundleService()
        {
            this.AssetBundleLoader = new AddressableAssetBundleLoader();
            this.AssetBundleResourceLocator = new AddressableAssetBundleResourceLocator();
            this.AssetBundleCleaner = new AddressableAssetBundleCleaner(this.AssetBundleResourceLocator);
            this.AssetBundleDownloader =
                new AddressableAssetBundleDownloader(this.AssetBundleResourceLocator, this.AssetBundleCleaner);
            this.AssetBundleUpdater = new AddressableAssetBundleUpdater(this.AssetBundleCleaner);
        }

        public async UniTask<bool> Initialize(Action onInitializationComplete = null,
            Action onInitializationFailed = null)
        {
            try
            {
                this._initializeHandle = Addressables.InitializeAsync();
                await this._initializeHandle;

                if (this._initializeHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[{LogTag}] Addressable initialized successfully.");
                    onInitializationComplete?.Invoke();
                    return true;
                }

                Debug.LogError($"[{LogTag}] Addressable initialization failed.");
                onInitializationFailed?.Invoke();
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] Exception during Addressable initialization: {ex.Message}");
                onInitializationFailed?.Invoke();
                return false;
            }
        }

        public async UniTask<bool> InitializeAsync(UniTask onInitializationComplete,
            UniTask onInitializationFailed)
        {
            try
            {
                this._initializeHandle = Addressables.InitializeAsync();
                await this._initializeHandle;

                if (this._initializeHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[{LogTag}] Addressable initialized successfully.");
                    await onInitializationComplete;
                    return true;
                }

                Debug.LogError($"[{LogTag}] Addressable initialization failed.");
                await onInitializationFailed;
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] Exception during Addressable initialization: {ex.Message}");
                await onInitializationFailed;
                return false;
            }
        }

        public void Dispose()
        {
            this.AssetBundleLoader.Dispose();
            this.AssetBundleUpdater.Dispose();

            if (this._initializeHandle.IsValid())
                Addressables.Release(this._initializeHandle);
        }
    }
}
#endif
