using DracoRuan.Foundation.Initializers;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.Foundation.Initializers.Interfaces;
using UnityEngine;
using VContainer;

namespace Test
{
    [AutoInstall(InstallerKey = nameof(SampleMonoInstaller2), InstallerInstanceType = nameof(InstallerType.MonoBehaviour))]
    public class SampleMonoInstaller2 : MonoBehaviour, IAsyncInstallable
    {
        public bool IsInstalled { get; private set; }

        private void Awake()
        {
            this.transform.SetParent(ProjectLifetimeScope.LifetimeScopeInstallerRoot);
        }

        public void Install(IContainerBuilder builder)
        {
            this.IsInstalled = true;
        }
    }
}