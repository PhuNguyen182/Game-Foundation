using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using UnityEngine.Pool;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Test
{
    [AutoInstall(InstallerKey = nameof(SampleMonoInstaller), InstallerInstanceType = nameof(InstallerType.MonoBehaviour))]
    public class SampleMonoInstaller : MonoBehaviour, IInstaller
    {
        private bool _isInstalled;

        public void Install(IContainerBuilder builder)
        {
            this._isInstalled = true;
        }

        public bool IsInstalled() => this._isInstalled;
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