using VContainer;
using VContainer.Unity;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.Initializers
{
    [DefaultExecutionOrder(-50)]
    public class ProjectLifetimeScope : LifetimeScope
    {
        public static Transform LifetimeScopeInstallerRoot;
        
        protected override void Configure(IContainerBuilder builder)
        {
            LifetimeScopeInstallerRoot = this.transform;
            builder.RegisterEntryPoint<AppInitializationPipelineEntryPoint>(Lifetime.Scoped);
            this.RegisterServicesAndInstaller(builder).Forget();
        }

        private async UniTask RegisterServicesAndInstaller(IContainerBuilder builder)
        {
            await UniTask.CompletedTask;
        }
    }
}
