using System;
using Cysharp.Threading.Tasks;

namespace DracoRuan.RemoteConfig
{
    public interface IRemoteConfigService : IDisposable
    {
        public UniTask Initialize();
        public int GetIntValue(string key);
        public long GetLongValue(string key);
        public bool GetBoolValue(string key);
        public double GetDoubleValue(string key);
        public string GetStringValue(string key);
    }
}
