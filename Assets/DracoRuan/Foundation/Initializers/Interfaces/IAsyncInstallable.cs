using VContainer.Unity;

namespace DracoRuan.Foundation.Initializers.Interfaces
{
    public interface IAsyncInstallable : IInstaller
    {
        public bool IsInstalled();
    }
}