using System.Collections.Generic;
using DracoRuan.CoreSystems.MessageBrokers.Attributes;
using DracoRuan.CoreSystems.MessageBrokers.MessageFilters;
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
        private IContainerBuilder _builder;

        public void Install(IContainerBuilder builder)
        {
            this._builder = builder;
            builder.RegisterMessagePipe(this.OnMessagePipeRegisterOption);
            builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));
            this._isInstalled = true;
        }

        private void OnMessagePipeRegisterOption(MessagePipeOptions options)
        {
            //this._builder.RegisterRe<int, bool, RequestAllHandler>(options);
            //this._builder.RegisterMessageHandlerFilter<ChangedIntValueFilter>();
        }

        public bool IsInstalled() => this._isInstalled;
    }

    [Attributes.MessageHandlerFilter(IAsync = false)]
    public class ChangedIntValueFilter : ChangedValueFilter<int>
    {
        
    }

    [RequestAllHandler(RequestType = typeof(int), ResponseType = typeof(bool))]
    public class RequestAllHandler : IRequestAllHandler<int, bool>
    {
        public bool[] InvokeAll(int request)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<bool> InvokeAllLazy(int request)
        {
            throw new System.NotImplementedException();
        }
    }
}
