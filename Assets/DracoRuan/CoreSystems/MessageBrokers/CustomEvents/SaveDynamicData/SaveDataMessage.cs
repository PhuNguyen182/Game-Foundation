using System;

namespace DracoRuan.CoreSystems.MessageBrokers.CustomEvents.SaveDynamicData
{
    public struct SaveDataMessage
    {
        public Type DynamicDataType;
        public bool SaveAllData;
    }
}