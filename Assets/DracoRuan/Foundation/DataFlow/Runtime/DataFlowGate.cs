using System;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// A one-way latch that stays shut until boot-time migration has finished. Nothing may read or
    /// write a save file before it opens.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a latch instead of ordering the entry points.</b> VContainer's
    /// <c>EntryPointDispatcher</c> resolves the <c>IStartable</c> list long before the
    /// <c>IAsyncStartable</c> list, and resolving <i>constructs</i> those objects — so any
    /// <c>IStartable</c> that takes a data controller in its constructor brings that controller into
    /// existence before migration could possibly have run. Both loop items are also dispatched to
    /// the same <c>PlayerLoopTiming.Startup</c>, and every <c>IAsyncStartable</c> is started in the
    /// same frame and forgotten, so registration order buys no guarantee at all.</para>
    ///
    /// <para>Awaiting this latch is therefore a safety net independent of construction order: if
    /// someone later adds a constructor dependency that pulls a controller in early, the controller
    /// still cannot touch the disk before migration completes.</para>
    ///
    /// <para><b>Failure propagates.</b> If migration cannot complete, the latch faults rather than
    /// opening, so every waiter fails loudly instead of quietly loading an un-migrated save and
    /// overwriting it on the next autosave.</para>
    /// </remarks>
    public sealed class DataFlowGate
    {
        private readonly UniTaskCompletionSource _completionSource = new();

        /// <summary>True once migration has completed successfully.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>True when migration failed; the gate will never open.</summary>
        public bool IsFaulted { get; private set; }

        /// <summary>
        /// Completes when migration has finished. Faults if it failed. Returns immediately once the
        /// gate is already open, so awaiting it repeatedly costs nothing.
        /// </summary>
        public UniTask WaitAsync() => this._completionSource.Task;

        /// <summary>Opens the gate. Safe to call more than once.</summary>
        public void Open()
        {
            if (this.IsFaulted)
                return;

            this.IsOpen = true;
            this._completionSource.TrySetResult();
        }

        /// <summary>Permanently faults the gate so no save is read or written.</summary>
        public void Fail(Exception exception)
        {
            if (this.IsOpen)
                return;

            this.IsFaulted = true;
            this._completionSource.TrySetException(
                exception ?? new InvalidOperationException("Save data migration failed."));
        }
    }
}
