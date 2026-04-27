using System;
using VContainer;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RegisterServiceAttribute : Attribute
    {
        public string LifetimeScope { get; set; } = nameof(Lifetime.Scoped);
        public string LifetimeScopeName { get; set; } = nameof(ProjectLifetimeScope);
        public string InstallerName { get; set; }
        public bool AsImplementedInterfaces { get; set; }
        public bool IsEntryPoint { get; set; }
        public bool AsSelf { get; set; }
        public string WithKey { get; set; }
    }
}
