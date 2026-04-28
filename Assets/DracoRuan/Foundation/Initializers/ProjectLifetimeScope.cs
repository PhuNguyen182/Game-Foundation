using Cysharp.Threading.Tasks;
using DracoRuan.VContainerInstallerSupport.Generated;
using DracoRuan.VContainerProjectLifetimeScopeEntryPointRegisterSupport.Generated;
using DracoRuan.VContainerProjectLifetimeScopeServiceRegisterSupport.Generated;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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
            await builder.AutoRegisterAllAvailableInstallers();
            builder.AutoRegisterAllProjectLifetimeScopeEntryPoints();
            builder.AutoRegisterAllProjectLifetimeScopeServices();
        }
    }
}
