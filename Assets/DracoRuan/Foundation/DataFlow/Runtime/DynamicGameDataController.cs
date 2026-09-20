using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.LocalData;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// Base class for a mutable save repository: one data model, one controller.
    ///
    /// <code>
    /// [DynamicGameDataController(nameof(RiseProgressionDataController))]
    /// public sealed class RiseProgressionDataController : DynamicGameDataController&lt;RiseProgressDataV1&gt;
    /// {
    ///     public RiseProgressionDataController(DataControllerContext context) : base(context) { }
    ///
    ///     public override string DomainId      =&gt; "rise_progression";
    ///     public override int    SchemaVersion =&gt; 1;
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para><b>This is a repository and nothing else</b> — load, expose, persist. Migration used to
    /// live here, which forced every controller to implement migration hooks it had no use for and
    /// meant each controller triggered a global migration pass of its own. Schema upgrades now
    /// happen once, at boot, before any controller runs.</para>
    ///
    /// <para><b>Loading is awaited, not fired off in the constructor.</b> The old constructor started
    /// an un-awaited task, so load failures disappeared silently and the game could reach the first
    /// scene with saves still loading.</para>
    ///
    /// <para><b><typeparamref name="TData"/> is always the current version's class.</b> Older version
    /// classes stay in the codebase but are referenced only by migrators. Pointing this at anything
    /// but the newest shape would make the controller read an old payload into the wrong type, which
    /// MessagePack accepts silently.</para>
    /// </remarks>
    public abstract class DynamicGameDataController<TData> : IDataController
        where TData : class, IGameData, new()
    {
        private const string LogTag = "DataController";

        private readonly DataControllerContext _context;
        private bool _isDisposed;

        protected DynamicGameDataController(DataControllerContext context)
        {
            this._context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Permanent identifier for this repository's save file.
        /// </summary>
        /// <remarks>
        /// Return a constant. Never <c>typeof(TData).Name</c>: renaming or moving the data class
        /// would then orphan the save file on every player's device, with nothing to warn you.
        /// </remarks>
        public abstract string DomainId { get; }

        /// <summary>Schema version this build expects. Bump it whenever the payload's shape changes.</summary>
        public abstract int SchemaVersion { get; }

        public Type DataType => typeof(TData);

        public bool IsInitialized { get; private set; }

        public bool IsDirty { get; private set; }

        /// <summary>The loaded data. Valid only after <see cref="InitializeAsync"/> completes.</summary>
        public TData Data { get; private set; }

        public event Action OnDataLoaded;

        /// <summary>Raised after every change notification, for UI that mirrors the data.</summary>
        public event Action<TData> OnDataChanged;

        /// <summary>
        /// How the last load ended. Useful on a diagnostics screen; <see cref="EnvelopeReadStatus.NotFound"/>
        /// simply means a new player.
        /// </summary>
        public EnvelopeReadStatus LastReadStatus { get; private set; } = EnvelopeReadStatus.NotFound;

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (this.IsInitialized)
                return;

            // Never read the disk before boot-time migration has finished, whatever caused this
            // controller to be constructed early.
            await this._context.Gate.WaitAsync();

            cancellationToken.ThrowIfCancellationRequested();

            this.Load();

            this.IsInitialized = true;
            this.OnAfterLoad();
            this.OnDataLoaded?.Invoke();
            this.OnDataChanged?.Invoke(this.Data);
        }

        /// <summary>
        /// Called once after the data is available. Override to derive runtime state or reconcile
        /// against remote config.
        /// </summary>
        protected virtual void OnAfterLoad()
        {
        }

        /// <summary>Called when no save exists. Override to seed a new player's starting state.</summary>
        protected virtual TData CreateDefault() => new();

        public void MarkDirty()
        {
            if (!this.IsInitialized || this._isDisposed)
                return;

            this.IsDirty = true;
            this._context.Scheduler.MarkDirty(this);
            this.OnDataChanged?.Invoke(this.Data);
        }

        public void Save()
        {
            if (this._isDisposed || this.Data == null)
                return;

            if (!this._context.Gate.IsOpen)
            {
                Debug.LogWarning(
                    $"[{LogTag}] Refusing to save '{this.DomainId}' before migration has completed.");
                return;
            }

            try
            {
                // Serialize on the calling (main) thread. Handing a live object to a background
                // thread races with gameplay mutating its collections, and the result is a torn
                // payload whose checksum is computed over the torn bytes - so it verifies perfectly
                // and the corruption is undetectable.
                byte[] payload = this._context.Codec.Serialize(this.Data);

                long revision = this.ReadCurrentRevision() + 1;

                this._context.Store.Write(
                    this.DomainId,
                    this.SchemaVersion,
                    payload,
                    revision,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    this._context.IdentityProvider.DeviceEpochId);

                this._context.Store.Prune(this.DomainId);

                this.IsDirty = false;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to save '{this.DomainId}': {exception.Message}");
                throw;
            }
        }

        public void Delete()
        {
            this._context.Store.DeleteAll(this.DomainId);
            this._context.Scheduler.Forget(this);

            this.Data = this.CreateDefault();
            this.IsDirty = false;
            this.OnDataChanged?.Invoke(this.Data);
        }

        private void Load()
        {
            EnvelopeReadStatus status =
                this._context.Store.Read(this.DomainId, this.SchemaVersion, out _, out byte[] payload);

            this.LastReadStatus = status;

            if (status == EnvelopeReadStatus.Success)
            {
                try
                {
                    this.Data = this._context.Codec.Deserialize<TData>(payload) ?? this.CreateDefault();
                    return;
                }
                catch (Exception exception)
                {
                    // The checksum passed, so the bytes are intact - this is a schema problem, not
                    // corruption. Keep the file; overwriting it would destroy the evidence and the
                    // player's progress along with it.
                    Debug.LogError(
                        $"[{LogTag}] '{this.DomainId}' v{this.SchemaVersion} is intact but could not be " +
                        $"deserialized into {typeof(TData).Name}: {exception.Message}. " +
                        "Starting from defaults; the existing file has been left untouched.");

                    this.LastReadStatus = EnvelopeReadStatus.ChecksumMismatch;
                    this.Data = this.CreateDefault();
                    return;
                }
            }

            if (status != EnvelopeReadStatus.NotFound)
            {
                Debug.LogError(
                    $"[{LogTag}] Could not load '{this.DomainId}' v{this.SchemaVersion}: {status}. " +
                    "Starting from defaults; the existing file has been left untouched.");
            }

            this.Data = this.CreateDefault();
        }

        private long ReadCurrentRevision() =>
            this._context.Store.ReadHeader(this.DomainId, this.SchemaVersion, out SaveEnvelopeHeader header) ==
            EnvelopeReadStatus.Success
                ? header.Revision
                : 0;

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;
            this.OnDataLoaded = null;
            this.OnDataChanged = null;
            this.Data = null;
        }
    }
}
