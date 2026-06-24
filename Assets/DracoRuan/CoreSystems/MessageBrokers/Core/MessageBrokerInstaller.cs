using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData;
using MessagePipe;
using VContainer.Unity;
using VContainer;

namespace DracoRuan.CoreSystems.MessageBrokers.Core
{
    [AutoInstall(InstallerKey = nameof(MessageBrokerInstaller))]
    public class MessageBrokerInstaller : IInstaller
    {
        private IContainerBuilder _builder;

        public void Install(IContainerBuilder builder)
        {
            this._builder = builder;
            this.RegisterCustomServices(builder);
            builder.Register<EventFactory>(Lifetime.Scoped);
            builder.RegisterMessagePipe(this.OnMessagePipeRegisterOption);
            builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));
        }

        private void OnMessagePipeRegisterOption(MessagePipeOptions options)
        {
            
        }

        private void RegisterCustomServices(IContainerBuilder builder)
        {
            builder.Register<SaveDataEvent>(Lifetime.Singleton);
            builder.Register<DeleteDataEvent>(Lifetime.Singleton);
        }
    }
}
