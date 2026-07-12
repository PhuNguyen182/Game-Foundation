using System;

namespace DracoRuan.PrebuildServices.MessageBrokers.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class MessageHandlerFilterAttribute : Attribute
    {
        public bool IsAsync { get; set; }
    }
}