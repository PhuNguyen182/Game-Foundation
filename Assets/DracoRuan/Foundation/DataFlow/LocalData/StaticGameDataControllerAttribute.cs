using System;

namespace DracoRuan.Foundation.DataFlow.LocalData
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class StaticGameDataControllerAttribute : Attribute
    {
        public string DataControllerKey { get; }
        
        public StaticGameDataControllerAttribute(string dataControllerKey)
        {
            this.DataControllerKey = dataControllerKey;
        }
    }
}