using VContainer;
using VContainer.Unity;
using DracoRuan.Foundation.Initializers;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.Core
{
    [RegisterInstaller(nameof(MessageBrokerInstaller), typeof(MessageBrokerInstaller), nameof(ProjectLifetimeScope))]
    public class MessageBrokerInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe(this.OnMessagePipeRegisterOption);
            builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));
        }

        private void OnMessagePipeRegisterOption(MessagePipeOptions options)
        {
            
        }
    }
}
