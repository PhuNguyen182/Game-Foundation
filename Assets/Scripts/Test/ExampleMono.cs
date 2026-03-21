using System;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.UISystem.Animations.ViewAnimation.DOTweenAnimation.AnimationActing.AnimationElements;
using UnityEngine;

public class ExampleMono : MonoBehaviour
{
    [SerializeField] private bool check;
    [SerializeField] private float timeout = 3f;
    [SerializeField] private ScaleAnimation scaleAnimation;
    
    private void Start()
    {
        this.TestAsync().Forget();
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
}
