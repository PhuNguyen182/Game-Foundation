using VContainer;
using DracoRuan.Foundation.Initializers.Interfaces;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.Core
{
    [AutoInstall(InstallerKey = nameof(MessageBrokerInstaller))]
    public class MessageBrokerInstaller : IAsyncInstallable
    {
        private bool _isInstalled;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe(this.OnMessagePipeRegisterOption);
            builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));
            this._isInstalled = true;
        }

        private void OnMessagePipeRegisterOption(MessagePipeOptions options)
        {
            
        }

        public bool IsInstalled() => this._isInstalled;
    }
}
