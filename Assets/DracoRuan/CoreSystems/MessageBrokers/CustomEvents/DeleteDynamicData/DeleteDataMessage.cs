using System;

namespace DracoRuan.CoreSystems.MessageBrokers.CustomEvents.DeleteDynamicData
{
    public struct DeleteDataMessage
    {
        public Type DynamicDataType;
        public bool DeleteAllData;
    }
}