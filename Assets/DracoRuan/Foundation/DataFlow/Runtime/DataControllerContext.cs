using System;
using DracoRuan.Foundation.DataFlow.Core.Serialization;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using DracoRuan.Foundation.DataFlow.Sync;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// Everything a repository needs, bundled into one injected object.
    /// </summary>
    /// <remarks>
    /// <para><b>This exists to stop future changes breaking every game built on the foundation.</b>
    /// The previous base class took four separate constructor parameters, each of which every
    /// subclass had to accept and forward. Adding a fifth dependency — which this rework needed to
    /// do several times — meant editing every controller in every project. Bundling them means a
    /// new dependency is added here and subclasses never change.</para>
    ///
    /// <para>Registered as a singleton; it holds no per-domain state.</para>
    /// </remarks>
    public sealed class DataControllerContext
    {
        public DataControllerContext(
            SaveEnvelopeStore store,
            SaveScheduler scheduler,
            DataFlowGate gate,
            IPayloadCodec codec,
            IPlayerIdentityProvider identityProvider)
        {
            this.Store = store ?? throw new ArgumentNullException(nameof(store));
            this.Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.Gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this.Codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.IdentityProvider = identityProvider ??
                                    throw new ArgumentNullException(nameof(identityProvider));
        }

        /// <summary>Reads and writes versioned save files.</summary>
        public SaveEnvelopeStore Store { get; }

        /// <summary>Batches and flushes writes.</summary>
        public SaveScheduler Scheduler { get; }

        /// <summary>Blocks disk access until boot-time migration has completed.</summary>
        public DataFlowGate Gate { get; }

        /// <summary>Serializes payloads.</summary>
        public IPayloadCodec Codec { get; }

        /// <summary>Identifies the player these saves belong to.</summary>
        public IPlayerIdentityProvider IdentityProvider { get; }
    }
}
