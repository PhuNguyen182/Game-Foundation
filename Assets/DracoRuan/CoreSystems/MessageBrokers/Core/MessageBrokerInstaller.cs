using VContainer;
using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData;
using DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData;
using DracoRuan.Foundation.Initializers.Interfaces;
using MessagePipe;

namespace DracoRuan.CoreSystems.MessageBrokers.Core
{
    [AutoInstall(InstallerKey = nameof(MessageBrokerInstaller))]
    public class MessageBrokerInstaller : IAsyncInstallable
    {
        private bool _isInstalled;
        private IContainerBuilder _builder;

        public void Install(IContainerBuilder builder)
        {
            this._builder = builder;
            this.RegisterCustomServices(builder);
            builder.Register<EventFactory>(Lifetime.Singleton);
            builder.RegisterMessagePipe(this.OnMessagePipeRegisterOption);
            builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));
            this._isInstalled = true;
        }

        private void OnMessagePipeRegisterOption(MessagePipeOptions options)
        {
            
        }

        private void RegisterCustomServices(IContainerBuilder builder)
        {
            builder.Register<SaveDataEvent>(Lifetime.Singleton);
            builder.Register<DeleteDataEvent>(Lifetime.Singleton);
        }

        public bool IsInstalled() => this._isInstalled;
    }
}
