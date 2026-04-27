using VContainer.Unity;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    public interface IAsyncInstallable : IInstaller
    {
        public bool IsInstalled { get; }
    }
}