#if USE_EXTENDED_ADDRESSABLE
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.AssetBundleRuntime
{
    public class AddressableAssetBundleLoader : IAssetBundleLoader
    {
        private AsyncOperationHandle<GameObject> _loadRequest;
        private AsyncOperationHandle<SceneInstance> _sceneLoadRequest;

        public async UniTask LoadScene(string key, LoadSceneMode mode = LoadSceneMode.Single,
            bool activateOnLoad = true)
        {
            this._sceneLoadRequest = Addressables.LoadSceneAsync(key, mode, activateOnLoad);
            await this._sceneLoadRequest;
        }

        public async UniTask UnloadScene(UnloadSceneOptions options = UnloadSceneOptions.UnloadAllEmbeddedSceneObjects,
            bool autoReleaseHandle = true)
        {
            if (this._sceneLoadRequest.IsValid())
            {
                var sceneOperation = Addressables.UnloadSceneAsync(this._sceneLoadRequest, options, autoReleaseHandle);
                await sceneOperation;
            }
        }

        public async UniTask<GameObject> LoadAsset(string key)
        {
            this._loadRequest = Addressables.LoadAssetAsync<GameObject>(key);
            GameObject asset = await _loadRequest;

            if (this._loadRequest.Status == AsyncOperationStatus.Succeeded)
                return asset;

            Addressables.Release(this._loadRequest);
            return null;
        }

        public async UniTask<T> LoadAsset<T>(string key)
        {
            AsyncOperationHandle<T> loadRequest = Addressables.LoadAssetAsync<T>(key);
            T asset = await loadRequest;
            
            if (loadRequest.Status == AsyncOperationStatus.Succeeded)
                return asset;
            
            Addressables.Release(loadRequest);
            return default;
        }

        public async UniTask<T> LoadComponentAsset<T>(string key) where T : Component
        {
            this._loadRequest = Addressables.LoadAssetAsync<GameObject>(key);
            GameObject gameObject = await this._loadRequest;

            if (this._loadRequest.Status == AsyncOperationStatus.Succeeded)
            {
                gameObject.TryGetComponent(out T asset);
                return asset;
            }
            
            Addressables.Release(this._loadRequest);
            return null;
        }

        public void UnloadAsset<T>(T asset) where T : Component
        {
            if (asset)
                Addressables.Release(asset);
        }

        public void Dispose()
        {
            if (this._loadRequest.IsValid())
                Addressables.Release(this._loadRequest);

            if (this._sceneLoadRequest.IsValid())
                Addressables.Release(this._sceneLoadRequest);
        }
    }
}
#endif
