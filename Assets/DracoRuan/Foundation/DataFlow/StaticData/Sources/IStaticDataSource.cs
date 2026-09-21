using System.Threading;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>
    /// One place static data can be read from.
    /// </summary>
    /// <remarks>
    /// <para>The old <c>IDataProvider</c> took a serializer and a save service on every call — a
    /// leaky union of what each source happened to need, with most implementations ignoring both.
    /// A source here does one thing: turn a key into bytes-or-asset. Interpreting that payload is
    /// the decoder's job, so adding a CDN source costs one class and touches nothing else.</para>
    ///
    /// <para><b>Missing is not failure.</b> "This source has no value for this key" is the normal
    /// case for every source but the last one in a chain, and must return
    /// <see cref="StaticDataPayload.Missing"/> rather than throw or log an error. Only a genuine
    /// fault — malformed response, IO error — throws.</para>
    /// </remarks>
    public interface IStaticDataSource
    {
        StaticDataSourceType SourceType { get; }

        /// <summary>
        /// Reads <paramref name="key"/>. Interpretation of the key is the source's own: a Resources
        /// path, an Addressables address, a remote config key, or a URL.
        /// </summary>
        /// <exception cref="System.OperationCanceledException">
        /// The token was cancelled. Callers must let this propagate rather than treating it as a
        /// missing value, or a shutdown turns into a silent fallback to stale data.
        /// </exception>
        UniTask<StaticDataPayload> LoadAsync(string key, CancellationToken cancellationToken);

        /// <summary>
        /// Releases a payload this source produced. Safe to call with
        /// <see cref="StaticDataPayload.Missing"/> and safe to call twice.
        /// </summary>
        void Release(in StaticDataPayload payload);
    }
}
