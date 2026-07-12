using System;

namespace DracoRuan.PrebuildServices.MessageBrokers.CustomEvents.SaveDynamicData
{
    public struct SaveDataMessage
    {
        public Type DynamicDataType;
        public bool SaveAllData;
    }
}