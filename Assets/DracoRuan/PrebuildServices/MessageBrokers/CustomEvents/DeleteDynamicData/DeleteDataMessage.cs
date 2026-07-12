using System;

namespace DracoRuan.PrebuildServices.MessageBrokers.CustomEvents.DeleteDynamicData
{
    public struct DeleteDataMessage
    {
        public Type DynamicDataType;
        public bool DeleteAllData;
    }
}