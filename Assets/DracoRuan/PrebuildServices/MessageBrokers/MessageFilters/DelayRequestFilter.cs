using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.MessageFilters
{
    public class DelayRequestFilter : AsyncRequestHandlerFilter<int, int>
    {
        public override async UniTask<int> InvokeAsync(int request, CancellationToken cancellationToken,
            Func<int, CancellationToken, UniTask<int>> next)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(request), cancellationToken: cancellationToken);
            int response = await next(request, cancellationToken);
            return response;
        }
    }
}