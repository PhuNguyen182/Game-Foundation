using System;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterInstallerAttribute : Attribute
    {
        private readonly Type _installerDataType;
        
        public readonly string InstallerKey;
        public readonly string InstallerDataType;
        public readonly string LifetimeScopeName;
        public readonly string InstallerInstanceType;

        public RegisterInstallerAttribute(string installerKey, Type installerDataType, string lifetimeScopeName = null,
            InstallerType installerType = InstallerType.PlainCSharp)
        {
            this.InstallerKey = installerKey;
            this._installerDataType = installerDataType;
            this.InstallerDataType = nameof(this._installerDataType);
            this.LifetimeScopeName = lifetimeScopeName;
            this.InstallerInstanceType = $"{installerType}";
        }
    }
}
