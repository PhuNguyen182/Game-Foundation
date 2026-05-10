using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Migrator
{
    public class MigrationContext
    {
        private readonly Dictionary<string, object> _data = new();
        private readonly Dictionary<string, object> _sharedData = new();
        
        public int PlayerId { get; set; }
        public int CurrentVersion { get; set; }
        public int TargetVersion { get; set; }

        public TData GetData<TData>(string dataKey) where TData : class
        {
            object rawData = this._data[dataKey];
            TData result = (TData)rawData;
            return result;
        }

        public void SetData<TData>(string dataKey, TData data) where TData : class
            => this._data[dataKey] = data;
        
        public TSharedData GetSharedData<TSharedData>(string dataKey)
        {
            object rawData = this._sharedData[dataKey];
            TSharedData result = (TSharedData)rawData;
            return result;
        }

        public void SetSharedData<TSharedData>(string dataKey, TSharedData data)
            => this._sharedData[dataKey] = data;
    }
}
