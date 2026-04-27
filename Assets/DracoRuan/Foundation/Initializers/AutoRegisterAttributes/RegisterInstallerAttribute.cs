using System;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RegisterInstallerAttribute : Attribute
    {
        public string InstallerKey { get; set; }
        public string LifetimeScopeName  { get; set; }
        public string InstallerInstanceType { get; set; }

        public RegisterInstallerAttribute(string installerKey = null, string lifetimeScopeName = null,
            InstallerType installerType = InstallerType.PlainCSharp)
        {
            this.InstallerKey = installerKey;
            this.LifetimeScopeName = lifetimeScopeName;
            this.InstallerInstanceType = $"{installerType}";
        }
    }
}
