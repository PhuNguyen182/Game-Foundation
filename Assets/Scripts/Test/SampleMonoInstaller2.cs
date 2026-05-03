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
        private bool _isInstalled;

        private void Awake()
        {
            this.transform.SetParent(ProjectLifetimeScope.LifetimeScopeInstallerRoot);
        }

        public void Install(IContainerBuilder builder)
        {
            this._isInstalled = true;
        }

        public bool IsInstalled() => this._isInstalled;
    }
}