using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;

namespace DracoRuan.Foundation.DataFlow.StaticData
{
    /// <summary>
    /// One step of a fallback chain: where to look, and what to look for there.
    /// </summary>
    /// <remarks>
    /// <para>The key is per step rather than per table because the same data is addressed differently
    /// by each backend — a remote config key, a path under <c>Resources</c>, an Addressables address,
    /// a URL. Forcing one string to mean all four is what made the old
    /// <c>[GameData(dataKey)]</c> attribute unusable in practice, so it was bypassed and left as dead
    /// code while the real keys were written inline in each controller.</para>
    ///
    /// <para><b>Order is the chain.</b> Declared first is tried first.</para>
    /// </remarks>
    public readonly struct StaticDataSourceBinding
    {
        public StaticDataSourceBinding(StaticDataSourceType sourceType, string key)
        {
            this.SourceType = sourceType;
            this.Key = key;
        }

        public StaticDataSourceType SourceType { get; }

        /// <summary>Interpreted by the source: a config key, a path, an address, or a URL.</summary>
        public string Key { get; }

        public static StaticDataSourceBinding RemoteConfig(string key) =>
            new(StaticDataSourceType.RemoteConfig, key);

        public static StaticDataSourceBinding Resources(string path) =>
            new(StaticDataSourceType.Resources, path);

        public static StaticDataSourceBinding Addressable(string address) =>
            new(StaticDataSourceType.Addressable, address);

        /// <summary>A direct link — R2, S3 or any CDN.</summary>
        public static StaticDataSourceBinding Url(string url) =>
            new(StaticDataSourceType.Url, url);

        /// <summary>Reads as a chain, left to right: <c>Chain(RemoteConfig(..), Addressable(..))</c>.</summary>
        public static IReadOnlyList<StaticDataSourceBinding> Chain(params StaticDataSourceBinding[] bindings) =>
            bindings;
    }
}
