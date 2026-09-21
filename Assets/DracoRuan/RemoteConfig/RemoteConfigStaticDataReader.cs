using System;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using DracoRuan.Foundation.Initializers.Interfaces;

namespace DracoRuan.RemoteConfig
{
    /// <summary>
    /// Adapts this project's <see cref="IRemoteConfigService"/> to the port DataFlow reads through.
    /// </summary>
    /// <remarks>
    /// <para>The adapter lives here, on the game side, rather than in DataFlow: the foundation should
    /// not know which vendor supplies remote config, and — practically — a DataFlow assembly
    /// definition could not reference this namespace anyway.</para>
    ///
    /// <para><b>Reads are defensive.</b> <c>FirebaseRemoteConfigService.Initialize</c> sets its
    /// initialized flag in a <c>finally</c>, so it reports ready even when initialization threw and
    /// its internal handle is still null. Letting that NullReferenceException escape would abort the
    /// whole fallback chain; swallowing it turns the same situation into "remote had nothing", which
    /// is what the chain is built to handle.</para>
    /// </remarks>
    public sealed class RemoteConfigStaticDataReader : IStaticRemoteConfigReader
    {
        private const string LogTag = "RemoteConfigStaticDataReader";

        private readonly IRemoteConfigService _service;

        public RemoteConfigStaticDataReader(IRemoteConfigService service)
        {
            this._service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// A service that does not report its own readiness is assumed ready. The alternative —
        /// assuming it is not — is what made the previous provider wait forever on any implementation
        /// that did not happen to implement <see cref="IAsyncInitializable"/>.
        /// </summary>
        public bool IsReady =>
            this._service is not IAsyncInitializable initializable || initializable.IsInitialized();

        public string GetString(string key)
        {
            try
            {
                return this._service.GetStringValue(key) ?? string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{LogTag}] Reading '{key}' failed: {exception.Message}");
                return string.Empty;
            }
        }
    }
}
