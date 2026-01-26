using System;

namespace DracoRuan.Foundation.DataFlow.LocalData
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GameDataAttribute : Attribute
    {
        public string DataKey { get; }
        
        public GameDataAttribute(string dataKey)
        {
            this.DataKey = dataKey;
        }
    }
}
