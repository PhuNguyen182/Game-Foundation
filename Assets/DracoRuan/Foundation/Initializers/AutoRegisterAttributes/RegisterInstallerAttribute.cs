using System;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    /// <summary>
    /// Use this attribute for automatically installing your installer.
    /// </summary>
    /// <param name="InstallerKey"> Use this property to pass the installer identifier.
    /// If your installer is Plain C#, please pass the name of the installer's datatype.
    /// If your installer is MonoBehaviour or ScriptableObject, please pass the correct address that Addressables can load it.</param>>
    /// <param name="LifetimeScopeName"> The name of LifetimeScope that your installer living on</param>
    /// <param name="InstallerInstanceType"> Your installer instance type: Plain C#, MonoBehaviour, ScriptableObject</param>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RegisterInstallerAttribute : Attribute
    {
        public string InstallerKey { get; set; }
        public string LifetimeScopeName { get; set; } = nameof(ProjectLifetimeScope);
        public string InstallerInstanceType { get; set; } = nameof(InstallerType.PlainCSharp);
    }
}
