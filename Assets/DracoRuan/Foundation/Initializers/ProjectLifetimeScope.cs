using VContainer;
using VContainer.Unity;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DracoRuan.VContainerMessageBrokerInstallerSupport.Generated;

namespace DracoRuan.Foundation.Initializers
{
    [DefaultExecutionOrder(-50)]
    public class ProjectLifetimeScope : LifetimeScope
    {
        public static Transform LifetimeScopeInstallerRoot;
        
        protected override void Configure(IContainerBuilder builder)
        {
            LifetimeScopeInstallerRoot = this.transform;
            this.RegisterServices(builder).Forget();
        }

        private async UniTask RegisterServices(IContainerBuilder builder)
        {
            await builder.InstallMessageBrokerInstaller();
            await UniTask.CompletedTask;
        }
    }
}
