using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.StaticData.Decoders;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using Object = UnityEngine.Object;

namespace DracoRuan.Foundation.DataFlow.StaticData.Controllers
{
    /// <summary>
    /// The fallback chain, shared by both kinds of static data.
    /// </summary>
    /// <remarks>
    /// <para><b>A source wins only if it loads, decodes <i>and</i> validates.</b> The old chain
    /// stopped at the first source that returned anything, which meant a malformed or stale remote
    /// payload beat the ScriptableObject shipped in the build — and remote is normally the first
    /// link. Treating validation as part of "did this source work" makes a bad push degrade into a
    /// fallback instead of a live incident.</para>
    ///
    /// <para><b>Ownership is tracked per load.</b> Whichever source produced the winning payload is
    /// the one asked to release it, and only a value the decoder created itself is destroyed. The
    /// predecessor kept one provider field and overwrote it every iteration, so cleanup ran against
    /// the last provider built rather than the one that loaded the data.</para>
    /// </remarks>
    public abstract class StaticDataControllerBase<TValue> : IStaticDataController
    {
        private const string LogTag = "StaticData";

        private readonly StaticDataControllerContext _context;

        private Action _onDataLoaded;
        private IStaticDataSource _owningSource;
        private StaticDataPayload _payload;
        private bool _ownsValue;
        private bool _isDisposed;

        protected StaticDataControllerBase(StaticDataControllerContext context)
        {
            this._context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public abstract string DataId { get; }

        public abstract Type DataType { get; }

        /// <summary>
        /// The fallback chain, in order. Read when loading starts, never from a constructor — a base
        /// constructor that touched this would run before the subclass had assigned its own fields.
        /// </summary>
        protected abstract IReadOnlyList<StaticDataSourceBinding> Sources { get; }

        /// <summary>
        /// The decoder for a payload of this shape, or null to reject it and move down the chain.
        /// </summary>
        /// <remarks>
        /// Keyed on what actually arrived rather than on which source it came from. The same
        /// Addressables address can hold a <c>.asset</c> or a <c>.json</c> TextAsset, and only the
        /// payload says which — a controller that picked its decoder from the source type would fail
        /// on the shape it did not expect.
        /// </remarks>
        protected abstract IStaticDataDecoder<TValue> GetDecoder(StaticDataPayloadKind payloadKind);

        /// <summary>The loaded value. Meaningful only once <see cref="IsInitialized"/> is true.</summary>
        protected TValue Value { get; private set; }

        public TValue Data => this.Value;

        public bool IsInitialized { get; private set; }

        public StaticDataLoadResult LastLoadResult { get; private set; } = StaticDataLoadResult.NotLoaded;

        public event Action OnDataLoaded
        {
            add
            {
                if (value == null)
                    return;

                this._onDataLoaded += value;

                // Fire straight away for a late subscriber. Loading is kicked off by the boot
                // pipeline, so a consumer constructed afterwards would otherwise subscribe to an
                // event that has already fired and wait for a second one that never comes.
                if (this.IsInitialized)
                    value();
            }
            remove => this._onDataLoaded -= value;
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken = default) =>
            this.IsInitialized ? UniTask.CompletedTask : this.LoadAsync(cancellationToken);

        public UniTask ReloadAsync(CancellationToken cancellationToken = default) =>
            this.LoadAsync(cancellationToken);

        /// <summary>
        /// Rejects data this controller considers unusable, so the chain carries on to the next
        /// source. Override for the checks a table cannot be correct without — a required row, a
        /// rate that must sum to one, an id referenced from another table.
        /// </summary>
        protected virtual bool Validate(TValue value, out string failureReason)
        {
            failureReason = null;
            return true;
        }

        /// <summary>
        /// Called with the accepted value before <see cref="IsInitialized"/> flips, for derived state
        /// such as an index. Throwing here rejects the value exactly as failing validation would.
        /// </summary>
        protected virtual void OnValueAdopted(TValue value)
        {
        }

        private async UniTask LoadAsync(CancellationToken cancellationToken)
        {
            if (this._isDisposed)
                throw new ObjectDisposedException(this.GetType().Name);

            IReadOnlyList<StaticDataSourceBinding> bindings = this.Sources;
            if (bindings == null || bindings.Count == 0)
            {
                Debug.LogError($"[{LogTag}] '{this.DataId}' declares no sources, so it can never load.");
                this.LastLoadResult = StaticDataLoadResult.NotLoaded;
                return;
            }

            List<StaticDataSourceAttempt> attempts = new(bindings.Count);

            for (int index = 0; index < bindings.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                StaticDataSourceBinding binding = bindings[index];
                if (await this.TryLoadFromAsync(binding, attempts, cancellationToken))
                {
                    for (int remaining = index + 1; remaining < bindings.Count; remaining++)
                        attempts.Add(new StaticDataSourceAttempt(bindings[remaining].SourceType,
                            bindings[remaining].Key, StaticDataAttemptOutcome.NotAttempted));

                    this.LastLoadResult = new StaticDataLoadResult(true, binding.SourceType, attempts);
                    this._onDataLoaded?.Invoke();
                    return;
                }
            }

            this.LastLoadResult = new StaticDataLoadResult(false, StaticDataSourceType.None, attempts);

            // Only report once the whole chain is exhausted. Logging per miss turns a healthy boot -
            // where remote simply has no override and the shipped asset is used - into a red console.
            Debug.LogError(
                $"[{LogTag}] '{this.DataId}' could not be loaded from any source. {this.LastLoadResult.Describe()}");
        }

        private async UniTask<bool> TryLoadFromAsync(
            StaticDataSourceBinding binding, List<StaticDataSourceAttempt> attempts,
            CancellationToken cancellationToken)
        {
            IStaticDataSource source = this._context.Sources.Get(binding.SourceType);
            if (source == null)
            {
                attempts.Add(new StaticDataSourceAttempt(binding.SourceType, binding.Key,
                    StaticDataAttemptOutcome.SourceUnavailable));
                return false;
            }

            // Cancellation propagates: a shutdown must not look like "this source had nothing", or
            // the chain would carry on loading during teardown.
            StaticDataPayload payload = await source.LoadAsync(binding.Key, cancellationToken);

            if (!payload.HasValue)
            {
                attempts.Add(new StaticDataSourceAttempt(binding.SourceType, binding.Key,
                    StaticDataAttemptOutcome.Missing));
                return false;
            }

            IStaticDataDecoder<TValue> decoder = this.GetDecoder(payload.Kind);
            if (decoder == null)
            {
                source.Release(payload);
                attempts.Add(new StaticDataSourceAttempt(binding.SourceType, binding.Key,
                    StaticDataAttemptOutcome.DecodeFailed, "No decoder for this source."));
                return false;
            }

            DecodeResult<TValue> decoded = decoder.Decode(payload);
            if (!decoded.Succeeded)
            {
                source.Release(payload);
                attempts.Add(new StaticDataSourceAttempt(binding.SourceType, binding.Key,
                    StaticDataAttemptOutcome.DecodeFailed, decoded.FailureReason));
                return false;
            }

            if (!this.Validate(decoded.Value, out string failureReason))
            {
                ReleaseLoaded(source, payload, decoded.Value, decoded.OwnsValue);
                attempts.Add(new StaticDataSourceAttempt(binding.SourceType, binding.Key,
                    StaticDataAttemptOutcome.ValidationFailed, failureReason));
                return false;
            }

            try
            {
                this.OnValueAdopted(decoded.Value);
            }
            catch (Exception exception)
            {
                // Index building failing means the table is unusable in practice, so treat it the
                // same as failing validation rather than leaving a controller half-built.
                ReleaseLoaded(source, payload, decoded.Value, decoded.OwnsValue);
                attempts.Add(new StaticDataSourceAttempt(binding.SourceType, binding.Key,
                    StaticDataAttemptOutcome.ValidationFailed, exception.Message));
                return false;
            }

            this.Adopt(source, payload, decoded.Value, decoded.OwnsValue);
            attempts.Add(new StaticDataSourceAttempt(binding.SourceType, binding.Key,
                StaticDataAttemptOutcome.Loaded));

            return true;
        }

        /// <summary>
        /// Swaps in the new value and only then releases what it replaced, so a reload never leaves
        /// a window where consumers can observe released data.
        /// </summary>
        private void Adopt(IStaticDataSource source, in StaticDataPayload payload, TValue value, bool ownsValue)
        {
            IStaticDataSource previousSource = this._owningSource;
            StaticDataPayload previousPayload = this._payload;
            TValue previousValue = this.Value;
            bool previousOwnsValue = this._ownsValue;
            bool hadValue = this.IsInitialized;

            this._owningSource = source;
            this._payload = payload;
            this.Value = value;
            this._ownsValue = ownsValue;
            this.IsInitialized = true;

            if (hadValue)
                ReleaseLoaded(previousSource, previousPayload, previousValue, previousOwnsValue);
        }

        private static void ReleaseLoaded(IStaticDataSource source, in StaticDataPayload payload,
            TValue value, bool ownsValue)
        {
            // Destroy only what a decoder created - a ScriptableObject built from JSON. An asset that
            // came from Resources or Addressables is released through its source; destroying it would
            // tear down the project asset itself and null every other reference to it.
            if (ownsValue && value is Object unityObject)
                StaticDataObjectLifetime.Destroy(unityObject);

            source?.Release(payload);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._isDisposed)
                return;

            this._isDisposed = true;

            if (!disposing)
                return;

            ReleaseLoaded(this._owningSource, this._payload, this.Value, this._ownsValue);

            this._owningSource = null;
            this._payload = StaticDataPayload.Missing();
            this.Value = default;
            this._ownsValue = false;
            this.IsInitialized = false;
            this._onDataLoaded = null;
        }

        // No finalizer. Nothing here is unmanaged - the Addressables handle and the ScriptableObject
        // are both managed - and a finalizer would only add every controller to the finalization
        // queue while touching managed objects that may already be gone.
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}