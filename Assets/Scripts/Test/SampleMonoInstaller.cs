using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.Foundation.Initializers.Interfaces;
using UnityEngine.Pool;
using UnityEngine;
using VContainer;

namespace Test
{
    [AutoInstall(InstallerKey = nameof(SampleMonoInstaller), 
        InstallerInstanceType = nameof(InstallerType.MonoBehaviour))]
    public class SampleMonoInstaller : MonoBehaviour, IAsyncInstallable
    {
        public bool IsInstalled { get; private set; }

        public void Install(IContainerBuilder builder)
        {
            this.IsInstalled = true;
        }
    }

    public class PoolExample
    {
        public async UniTask WaitTest()
        {
            using (ListPool<UniTask>.Get(out List<UniTask> loadTasks))
            {
                for (int i = 0; i < 10; i++)
                {
                    loadTasks.Add(UniTask.CompletedTask);
                }

                await UniTask.WhenAll(loadTasks);
            }
        }
    }
}