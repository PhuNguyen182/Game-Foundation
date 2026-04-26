using VContainer;
using VContainer.Unity;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.Core
{
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
