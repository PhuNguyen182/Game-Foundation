using System;
using VContainer;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterServiceAttribute : Attribute
    {
        public readonly string ServiceName;
        public readonly Lifetime LifetimeScope;
        public readonly string LifetimeScopeName;
        public readonly string InstallerName;
        public readonly bool AsImplementInterfaces;
        public readonly bool IsEntryPoint;
        public readonly bool AsSelf;
        public readonly string WithKey;

        public RegisterServiceAttribute(string serviceName, Lifetime lifetime, string lifetimeScopeName = null, 
            string installerName = null, bool asImplementInterfaces = false, bool isEntryPoint = false, 
            bool asSelf = false, string withKey = null)
        {
            this.ServiceName = serviceName;
            this.LifetimeScope = lifetime;
            this.LifetimeScopeName = lifetimeScopeName;
            this.InstallerName = installerName;
            this.AsImplementInterfaces = asImplementInterfaces;
            this.IsEntryPoint = isEntryPoint;
            this.AsSelf = asSelf;
            this.WithKey = withKey;
        }
    }
}
