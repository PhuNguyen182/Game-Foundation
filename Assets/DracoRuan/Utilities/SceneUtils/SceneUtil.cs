using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace DracoRuan.Utilities.SceneUtils
{
    public static class SceneUtil
    {
        public static async UniTask LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            AsyncOperation loadSceneOperationOperation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (loadSceneOperationOperation == null)
                return;
            
            while (!loadSceneOperationOperation.isDone)
            {
                await UniTask.NextFrame();
            }
        }
        
        public static async UniTask LoadScene(string sceneName, SceneActivation activation, LoadSceneMode mode = LoadSceneMode.Single)
        {
            AsyncOperation loadSceneOperationOperation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (loadSceneOperationOperation == null)
                return;

            activation.SceneOperation = loadSceneOperationOperation;
            activation.AllowSceneActive = false;
            while (!loadSceneOperationOperation.isDone)
            {
                await UniTask.NextFrame();
            }
        }
    }
}
