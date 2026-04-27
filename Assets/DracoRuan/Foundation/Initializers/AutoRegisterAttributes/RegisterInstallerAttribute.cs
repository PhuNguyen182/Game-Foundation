using System;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterInstallerAttribute : Attribute
    {
        public readonly string InstallerKey;
        public readonly string LifetimeScopeName;
        public readonly string InstallerInstanceType;

        public RegisterInstallerAttribute(string installerKey, string lifetimeScopeName = null,
            InstallerType installerType = InstallerType.PlainCSharp)
        {
            this.InstallerKey = installerKey;
            this.LifetimeScopeName = lifetimeScopeName;
            this.InstallerInstanceType = $"{installerType}";
        }
    }
}
