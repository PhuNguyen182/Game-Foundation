using System;

namespace DracoRuan.Foundation.DataFlow.LocalData
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class DynamicGameDataControllerAttribute : Attribute
    {
        public string DataControllerKey { get; }
        
        public DynamicGameDataControllerAttribute(string dataControllerKey)
        {
            this.DataControllerKey = dataControllerKey;
        }
    }
}