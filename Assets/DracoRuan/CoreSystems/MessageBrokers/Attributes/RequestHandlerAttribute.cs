using System;

namespace DracoRuan.CoreSystems.MessageBrokers.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class RequestHandlerAttribute : Attribute
    {
        public Type RequestType { get; set; }
        public Type ResponseType { get; set; }
    }
}