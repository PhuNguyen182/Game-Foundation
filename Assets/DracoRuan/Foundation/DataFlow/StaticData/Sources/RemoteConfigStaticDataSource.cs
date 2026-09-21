using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>
    /// Reads static data from remote config, as a raw string under a key.
    /// </summary>
    /// <remarks>
    /// <para><b>The wait for readiness is bounded.</b> The previous implementation awaited
    /// <c>UniTask.WaitUntil(IsInitialized)</c> with no timeout and no cancellation token. Remote
    /// config is the <i>first</i> source in a normal chain, so a service that never finished
    /// initializing — offline, SDK failure, or simply never started — left every config table waiting
    /// forever and the game sitting on a black screen with nothing in the log. A timeout here means
    /// the worst case is falling back to the shipped asset, which is exactly what a fallback chain
    /// is for.</para>
    /// </remarks>
    public sealed class RemoteConfigStaticDataSource : IStaticDataSource
    {
        private const string LogTag = "StaticData/RemoteConfig";

        private readonly IStaticRemoteConfigReader _reader;
        private readonly TimeSpan _readyTimeout;

        public RemoteConfigStaticDataSource(IStaticRemoteConfigReader reader, TimeSpan readyTimeout)
        {
            this._reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this._readyTimeout = readyTimeout;
        }

        public StaticDataSourceType SourceType => StaticDataSourceType.RemoteConfig;

        public async UniTask<StaticDataPayload> LoadAsync(string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(key))
                return StaticDataPayload.Missing();

            if (!await this.WaitUntilReadyAsync(cancellationToken))
                return StaticDataPayload.Missing();

            string value;
            try
            {
                value = this._reader.GetString(key);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{LogTag}] Reading '{key}' threw: {exception.Message}");
                return StaticDataPayload.Missing();
            }

            // An unset key comes back empty. That is a miss, not a failure - FromText maps it to
            // Missing so the chain falls through quietly instead of logging an error on a healthy run.
            return StaticDataPayload.FromText(value);
        }

        public void Release(in StaticDataPayload payload)
        {
            // Nothing to release: the payload is a string the service already owned.
        }

        private async UniTask<bool> WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            if (this._reader.IsReady)
                return true;

            try
            {
                await UniTask
                    .WaitUntil(() => this._reader.IsReady, cancellationToken: cancellationToken)
                    .Timeout(this._readyTimeout);

                return true;
            }
            catch (TimeoutException)
            {
                // Name the service and the budget. Without this the symptom is config silently
                // coming from the local asset with no indication that remote was ever tried.
                Debug.LogWarning(
                    $"[{LogTag}] Remote config was not ready within {this._readyTimeout.TotalSeconds:0.#}s. " +
                    "Falling back to the next source.");

                return false;
            }
        }
    }
}
