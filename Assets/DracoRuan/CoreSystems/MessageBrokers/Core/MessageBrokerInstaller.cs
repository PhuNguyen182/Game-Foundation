using VContainer;
using DracoRuan.Foundation.Initializers;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.Foundation.Initializers.Interfaces;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.Core
{
    [RegisterInstaller(nameof(MessageBrokerInstaller), nameof(ProjectLifetimeScope))]
    public class MessageBrokerInstaller : IAsyncInstallable
    {
        public bool IsInstalled { get; private set; }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe(this.OnMessagePipeRegisterOption);
            builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));
            this.IsInstalled = true;
        }

        private void OnMessagePipeRegisterOption(MessagePipeOptions options)
        {
            
        }
    }
}
