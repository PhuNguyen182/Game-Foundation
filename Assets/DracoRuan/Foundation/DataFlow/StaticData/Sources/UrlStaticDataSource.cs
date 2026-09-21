using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>
    /// Downloads static data from a URL — R2, S3, or any CDN — and caches it on disk.
    /// </summary>
    /// <remarks>
    /// <para>The key is the link itself. The body comes back as text and goes straight into the same
    /// decoder a <c>Resources</c> CSV would use, so moving a table from the build to a bucket is a
    /// registration change and nothing else.</para>
    ///
    /// <para><b>Three things a network source needs that a local one does not.</b> An ETag, so boot
    /// does not re-download an unchanged table every time (a 304 costs a round trip instead of the
    /// whole payload). A disk cache, so the table still exists offline. And its own timeout and
    /// retry budget, because "slow" is a normal state for a network and a fatal one for a local read.</para>
    /// </remarks>
    public sealed class UrlStaticDataSource : IStaticDataSource
    {
        private const string LogTag = "StaticData/Url";
        private const string ETagHeader = "ETag";
        private const string IfNoneMatchHeader = "If-None-Match";
        private const long NotModified = 304;

        private readonly StaticDataDiskCache _cache;
        private readonly int _timeoutSeconds;
        private readonly int _maxAttempts;

        /// <param name="cache">Disk cache. Required — an uncached CDN source is online-only.</param>
        /// <param name="timeoutSeconds">Per-attempt budget handed to UnityWebRequest.</param>
        /// <param name="maxAttempts">
        /// Total attempts including the first. Retries cover the transient failures that make up most
        /// mobile network errors; past a couple of tries the cache is the better answer than a longer
        /// wait on a boot screen.
        /// </param>
        public UrlStaticDataSource(StaticDataDiskCache cache, int timeoutSeconds = 10, int maxAttempts = 2)
        {
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this._timeoutSeconds = Mathf.Max(1, timeoutSeconds);
            this._maxAttempts = Mathf.Max(1, maxAttempts);
        }

        public StaticDataSourceType SourceType => StaticDataSourceType.Url;

        public async UniTask<StaticDataPayload> LoadAsync(string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(key))
                return StaticDataPayload.Missing();

            bool hasCached = this._cache.TryRead(key, out string cachedETag, out string cachedBody);

            for (int attempt = 1; attempt <= this._maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await this.DownloadAsync(key, cachedETag, cachedBody, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown, not a failed source. Must not be mistaken for "try the next source".
                    throw;
                }
                catch (Exception exception) when (attempt < this._maxAttempts)
                {
                    Debug.LogWarning(
                        $"[{LogTag}] '{key}' attempt {attempt}/{this._maxAttempts} failed: {exception.Message}");
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[{LogTag}] '{key}' could not be downloaded: {exception.Message}");
                }
            }

            if (hasCached)
            {
                Debug.Log($"[{LogTag}] Serving '{key}' from the disk cache.");
                return StaticDataPayload.FromText(cachedBody);
            }

            return StaticDataPayload.Missing();
        }

        public void Release(in StaticDataPayload payload)
        {
            // Nothing to release: the payload is a managed string.
        }

        private async UniTask<StaticDataPayload> DownloadAsync(
            string url, string cachedETag, string cachedBody, CancellationToken cancellationToken)
        {
            // `using` is not optional here: UnityWebRequest owns a native download buffer that the
            // GC does not account for, so a leak of it never shows up in a managed memory profile.
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = this._timeoutSeconds;

            if (!string.IsNullOrEmpty(cachedETag) && cachedBody != null)
                request.SetRequestHeader(IfNoneMatchHeader, cachedETag);

            // WithCancellation both awaits correctly and aborts the request when the token fires,
            // rather than leaving it running in the background with nobody waiting on it.
            await request.SendWebRequest().WithCancellation(cancellationToken);

            if (request.responseCode == NotModified && cachedBody != null)
                return StaticDataPayload.FromText(cachedBody);

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"HTTP {request.responseCode}: {request.error}");

            string body = request.downloadHandler.text;
            if (string.IsNullOrEmpty(body))
                return StaticDataPayload.Missing();

            this._cache.Write(url, request.GetResponseHeader(ETagHeader), body);
            return StaticDataPayload.FromText(body);
        }
    }
}
