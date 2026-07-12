using System;

namespace DracoRuan.CoreSystems.MessageBrokers.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class RequestHandlerFilterAttribute : Attribute
    {
        public bool IsAsync { get; set; }
    }
}