#if USE_EXTENDED_ADDRESSABLE
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DracoRuan.CoreSystems.AssetBundleSystem.Runtime.Interfaces
{
    public interface IAssetBundleLoader : IDisposable
    {
        public UniTask LoadScene(string key, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true);

        public UniTask UnloadScene(UnloadSceneOptions options = UnloadSceneOptions.UnloadAllEmbeddedSceneObjects,
            bool autoReleaseHandle = true);

        public UniTask<GameObject> LoadAsset(string key);
        public UniTask<T> LoadAsset<T>(string key);
        public UniTask<T> LoadComponentAsset<T>(string key) where T : Component;
        public void UnloadAsset<T>(T asset) where T : Component;
    }
}
#endif