using System;
using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>Hands out the source for a <see cref="StaticDataSourceType"/>.</summary>
    public interface IStaticDataSourceRegistry
    {
        /// <summary>
        /// The source for <paramref name="sourceType"/>, or null when this build has none — for
        /// instance a chain naming <see cref="StaticDataSourceType.RemoteConfig"/> in a project that
        /// registered no remote config reader. Callers skip a null source and move on.
        /// </summary>
        IStaticDataSource Get(StaticDataSourceType sourceType);
    }

    /// <summary>
    /// Default registry. Builds each source on first use.
    /// </summary>
    /// <remarks>
    /// The predecessor eagerly constructed all five providers as fields whether or not a project used
    /// them, which meant a game with no CDN still paid for one and a game with no remote config still
    /// needed the service registered. Lazy construction also keeps a source's dependencies from being
    /// required until something actually asks for that source.
    /// </remarks>
    public sealed class StaticDataSourceRegistry : IStaticDataSourceRegistry
    {
        private const string LogTag = "StaticData/Sources";

        private readonly Dictionary<StaticDataSourceType, IStaticDataSource> _sources = new();
        private readonly IStaticRemoteConfigReader _remoteConfigReader;
        private readonly StaticDataDiskCache _diskCache;
        private readonly TimeSpan _remoteConfigReadyTimeout;
        private readonly int _urlTimeoutSeconds;

        /// <param name="remoteConfigReader">
        /// Null when the project has no remote config. Chains naming that source then skip it.
        /// </param>
        /// <param name="diskCache">Null falls back to <see cref="StaticDataDiskCache.CreateDefault"/>.</param>
        public StaticDataSourceRegistry(
            IStaticRemoteConfigReader remoteConfigReader = null,
            StaticDataDiskCache diskCache = null,
            float remoteConfigReadyTimeoutSeconds = 10f,
            int urlTimeoutSeconds = 10)
        {
            this._remoteConfigReader = remoteConfigReader;
            this._diskCache = diskCache;
            this._remoteConfigReadyTimeout = TimeSpan.FromSeconds(Mathf.Max(0.1f, remoteConfigReadyTimeoutSeconds));
            this._urlTimeoutSeconds = urlTimeoutSeconds;
        }

        public IStaticDataSource Get(StaticDataSourceType sourceType)
        {
            if (this._sources.TryGetValue(sourceType, out IStaticDataSource existing))
                return existing;

            IStaticDataSource created = this.Create(sourceType);
            if (created != null)
                this._sources[sourceType] = created;

            return created;
        }

        private IStaticDataSource Create(StaticDataSourceType sourceType)
        {
            switch (sourceType)
            {
                case StaticDataSourceType.Resources:
                    return new ResourcesStaticDataSource();

                case StaticDataSourceType.Addressable:
                    return new AddressableStaticDataSource();

                case StaticDataSourceType.Url:
                    return new UrlStaticDataSource(
                        this._diskCache ?? StaticDataDiskCache.CreateDefault(), this._urlTimeoutSeconds);

                case StaticDataSourceType.RemoteConfig:
                    if (this._remoteConfigReader != null)
                        return new RemoteConfigStaticDataSource(
                            this._remoteConfigReader, this._remoteConfigReadyTimeout);

                    Debug.LogWarning(
                        $"[{LogTag}] A data table asked for {nameof(StaticDataSourceType.RemoteConfig)} but no " +
                        $"{nameof(IStaticRemoteConfigReader)} was registered. Skipping that source.");
                    return null;

                default:
                    return null;
            }
        }
    }
}
