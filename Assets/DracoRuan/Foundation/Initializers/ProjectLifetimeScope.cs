using DracoRuan.VContainerSupport.Generated;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.Foundation.Initializers
{
    public class ProjectLifetimeScope : LifetimeScope
    {
        public static Transform LifetimeScopeInstallerRoot;
        
        protected override void Configure(IContainerBuilder builder)
        {
            LifetimeScopeInstallerRoot = this.transform;
        }
    }
}
