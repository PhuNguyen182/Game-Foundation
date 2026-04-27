using DracoRuan.Foundation.Initializers;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.Foundation.Initializers.Interfaces;
using UnityEngine;
using VContainer;

namespace Test
{
    [RegisterInstaller(InstallerKey = nameof(SampleMonoInstaller2), LifetimeScopeName = nameof(ProjectLifetimeScope), 
        InstallerInstanceType = nameof(InstallerType.MonoBehaviour))]
    public class SampleMonoInstaller2 : MonoBehaviour, IAsyncInstallable
    {
        public bool IsInstalled { get; private set; }

        public void Install(IContainerBuilder builder)
        {
            this.IsInstalled = true;
        }
    }
}