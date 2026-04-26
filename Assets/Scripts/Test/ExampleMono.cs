using System;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

public class ExampleMono : MonoBehaviour
{
    [SerializeField] private bool check;
    [SerializeField] private float timeout = 3f;
    [SerializeField] private ScaleAnimation scaleAnimation;

    private Transform _lifetimeScopeTransform;
    
    private void Start()
    {
        this.TestAsync().Forget();
        this.GetComponent<IInstaller>();
    }

    private async UniTask TestAsync()
    {
        try
        {
            await UniTask.WaitUntil(() => check, cancellationToken: this.destroyCancellationToken)
                .Timeout(TimeSpan.FromSeconds(timeout));
        }
        catch (TimeoutException timeoutException)
        {
            Debug.LogException(timeoutException);
        }
    }

    private async UniTask<T> TestLoadAsset<T>(string assetKey) where T : Component
    {
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(assetKey);
        await handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject instance = Instantiate(handle.Result);
            if (instance.TryGetComponent(out T component))
                return component;
        }

        return null;
    }
}
