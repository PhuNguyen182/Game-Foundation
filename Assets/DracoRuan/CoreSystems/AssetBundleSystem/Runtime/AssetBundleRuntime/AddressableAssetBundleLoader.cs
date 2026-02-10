#if USE_EXTENDED_ADDRESSABLE
using System;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.AssetBundleRuntime
{
    public class AddressableAssetBundleLoader : IAssetBundleLoader
    {
        private const string LogTag = "AddressableAssetBundleLoader";
        
        public async UniTask LoadScene(string key, LoadSceneMode mode = LoadSceneMode.Single,
            bool activateOnLoad = true)
        {
            AsyncOperationHandle<SceneInstance> handle = default;
            try
            {
                handle = Addressables.LoadSceneAsync(key, mode, activateOnLoad);
                await handle;
                Debug.Log($"[{LogTag}] Loaded scene {key} successfully !!!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Error loading scene {key}: {e.Message}");
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        public async UniTask UnloadScene(SceneInstance sceneInstance, UnloadSceneOptions options = UnloadSceneOptions.UnloadAllEmbeddedSceneObjects,
            bool autoReleaseHandle = true)
        {
            AsyncOperationHandle<SceneInstance> handle = default;
            try
            {
                handle = Addressables.UnloadSceneAsync(sceneInstance, options, autoReleaseHandle);
                await handle;
                Debug.Log($"[{LogTag}] Unloaded scene {sceneInstance.Scene.name} successfully !!!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Error unloading scene {sceneInstance.Scene.name}: {e.Message}");
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        public async UniTask<GameObject> LoadAsset(string key)
        {
            AsyncOperationHandle<GameObject> handle = default;
            try
            {
                handle = Addressables.LoadAssetAsync<GameObject>(key);
                GameObject asset = await handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[{LogTag}] Loaded GameObject {key} successfully !!!");
                    return asset;
                }
                
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Error loading Game Object {key}: {e.Message}");
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }

            return null;
        }

        public async UniTask<T> LoadAsset<T>(string key)
        {
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            try
            {
                handle = Addressables.LoadAssetAsync<T>(key);
                T asset = await handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[{LogTag}] Loaded asset {key} successfully !!!");
                    return asset;
                }
                
                Debug.LogError($"[{LogTag}] Error loading asset {key}: {handle.Status}");
                return default;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Error loading asset {key}: {e.Message}");
                return default;
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        public async UniTask<T> LoadComponentAsset<T>(string key) where T : Object
        {
            AsyncOperationHandle<T> handle = default;
            try
            {
                handle = Addressables.LoadAssetAsync<T>(key);
                T instance = await handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                    return instance;

                Debug.LogError($"[{LogTag}] Error loading component {key}: {handle.Status}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Error loading component {key}: {e.Message}");
                return null;
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        public void UnloadAsset<T>(T asset) where T : Object
        {
            try
            {
                Addressables.Release(asset);
                Debug.Log($"[{LogTag}] [{typeof(T)}] Unloaded asset {asset.GetType().Name} successfully !!!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}] Error unloading asset {asset}: {e.Message}");
            }
        }

        public void Dispose()
        {
        }
    }
}
#endif
