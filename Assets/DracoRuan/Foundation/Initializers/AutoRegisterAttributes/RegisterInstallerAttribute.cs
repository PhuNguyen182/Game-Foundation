using System;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterInstallerAttribute : Attribute
    {
        public readonly string InstallerName;
        public readonly string LifetimeScopeName;

        public RegisterInstallerAttribute(string installerName = null, string lifetimeScopeName = null)
        {
            this.InstallerName = installerName;
            this.LifetimeScopeName = lifetimeScopeName;
        }
    }
}
