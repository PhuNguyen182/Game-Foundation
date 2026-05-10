using System;
using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Snapshot
{
    [Serializable]
    public class DictionaryWrapper
    {
        public List<string> keys = new();
        public List<string> values = new();
    }
}