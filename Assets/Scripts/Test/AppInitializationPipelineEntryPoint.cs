using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.Utilities.SceneUtils;
using UnityEngine.Pool;
using VContainer.Unity;

namespace Test
{
    public class AppInitializationPipelineEntryPoint : IAsyncStartable
    {
        private readonly IEnumerable<IAsyncInitializable> _asyncInitializableCollection;

        public AppInitializationPipelineEntryPoint(IEnumerable<IAsyncInitializable> initializableCollection)
        {
            this._asyncInitializableCollection = initializableCollection;
        }
        
        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            using (ListPool<UniTask>.Get(out List<UniTask> waitServiceInitializeTasks))
            {
                foreach (IAsyncInitializable initializable in this._asyncInitializableCollection)
                {
                    UniTask waitServiceTask = UniTask.WaitUntil(initializable.IsInitialized, cancellationToken: cancellation);
                    waitServiceInitializeTasks.Add(waitServiceTask);
                }

                if (cancellation.IsCancellationRequested)
                    return;
                
                await UniTask.WhenAll(waitServiceInitializeTasks);
            }

            await SceneUtil.LoadScene("Loading");
        }
    }
}
