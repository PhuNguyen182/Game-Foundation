using DracoRuan.Foundation.Initializers;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Test
{
    [RegisterInstaller(InstallerKey = nameof(SampleMonoInstaller), LifetimeScopeName = nameof(ProjectLifetimeScope), 
        InstallerInstanceType = nameof(InstallerType.MonoBehaviour))]
    public class SampleMonoInstaller : MonoBehaviour, IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            
        }
    }
}