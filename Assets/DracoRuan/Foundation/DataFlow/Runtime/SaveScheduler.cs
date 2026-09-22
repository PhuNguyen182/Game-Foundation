using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;
using DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// Persists dirty repositories on a timer, and flushes everything when the app is about to go
    /// away.
    /// </summary>
    /// <remarks>
    /// <para><b>The problem this solves.</b> Previously nothing wrote a save unless gameplay code
    /// explicitly asked, and there was no pause or quit hook anywhere in the project. On mobile,
    /// where players leave via the task switcher and the OS kills the process without warning, that
    /// is routine data loss rather than an edge case.</para>
    ///
    /// <para><b>Shutdown flushes synchronously.</b> Android can freeze a process within a few
    /// hundred milliseconds of backgrounding, so an async write started at that point may simply
    /// never resume. The periodic path can afford to be gentle; the shutdown path cannot.</para>
    ///
    /// <para><b>Nothing is written before the gate opens.</b> An autosave firing mid-migration would
    /// write an un-migrated payload over a migrated one and undo the upgrade permanently.</para>
    ///
    /// <para>Ticks through the project's existing <see cref="IUpdateHandler"/> service instead of
    /// adding another MonoBehaviour <c>Update</c>.</para>
    /// </remarks>
    public sealed class SaveScheduler : VContainer.Unity.IStartable, IUpdateHandler, IDisposable
    {
        private const string LogTag = "SaveScheduler";

        private readonly HashSet<IDataController> _dirty = new();
        private readonly List<IDataController> _flushBuffer = new();
        private readonly DataFlowGate _gate;
        private readonly float _autoSaveIntervalSeconds;

        private DataFlowLifecycleRelay _relay;
        private float _elapsedSeconds;
        private bool _isDisposed;
        private bool _isRegistered;

        /// <param name="gate">Blocks any write until boot-time migration has completed.</param>
        /// <param name="autoSaveIntervalSeconds">
        /// Seconds between periodic flushes. Zero or less disables the timer, leaving only the
        /// suspend and quit flushes.
        /// </param>
        public SaveScheduler(DataFlowGate gate, float autoSaveIntervalSeconds = 0f)
        {
            this._gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this._autoSaveIntervalSeconds = autoSaveIntervalSeconds;
        }

        /// <summary>Number of repositories with unsaved changes.</summary>
        public int PendingCount => this._dirty.Count;

        /// <summary>
        /// Starts ticking and subscribes to the lifecycle callbacks.
        /// </summary>
        /// <remarks>
        /// This is <c>IStartable.Start</c>, so the container calls it — nothing about autosave or
        /// flush-on-suspend depends on an application remembering to start the scheduler by hand.
        /// It runs before migration has finished, which is harmless: <see cref="FlushAll"/> refuses
        /// to write until the gate opens.
        /// </remarks>
        public void Start()
        {
            if (this._isRegistered || this._isDisposed)
                return;

            UpdateServiceManager.RegisterUpdateHandler(this);

            this._relay = DataFlowLifecycleRelay.GetOrCreate();
            this._relay.OnSuspending += this.FlushAll;
            this._relay.OnQuitting += this.FlushAll;

            this._isRegistered = true;
        }

        /// <summary>Queues a repository to be written at the next flush.</summary>
        public void MarkDirty(IDataController controller)
        {
            if (controller == null || this._isDisposed)
                return;

            this._dirty.Add(controller);
        }

        /// <summary>Removes a repository from the queue, e.g. after its data was deleted.</summary>
        public void Forget(IDataController controller)
        {
            if (controller != null)
                this._dirty.Remove(controller);
        }

        void IUpdateHandler.Tick(float deltaTime)
        {
            if (this._autoSaveIntervalSeconds <= 0f || this._dirty.Count == 0)
                return;

            this._elapsedSeconds += deltaTime;
            if (this._elapsedSeconds < this._autoSaveIntervalSeconds)
                return;

            this._elapsedSeconds = 0f;
            this.FlushAll();
        }

        /// <summary>
        /// Writes every dirty repository, synchronously.
        /// </summary>
        /// <remarks>
        /// One repository failing must not strand the rest, so each is written inside its own
        /// try/catch and dropped from the queue either way — a repository that always throws would
        /// otherwise be retried on every single flush forever.
        /// </remarks>
        public void FlushAll()
        {
            if (this._isDisposed || this._dirty.Count == 0)
                return;

            if (!this._gate.IsOpen)
            {
                Debug.LogWarning(
                    $"[{LogTag}] Skipping flush: save data migration has not completed. " +
                    "Writing now would overwrite migrated data with an un-migrated payload.");
                return;
            }

            // Copy first: Save() can mark other repositories dirty.
            this._flushBuffer.Clear();
            this._flushBuffer.AddRange(this._dirty);
            this._dirty.Clear();

            foreach (IDataController controller in this._flushBuffer)
            {
                try
                {
                    controller.Save();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[{LogTag}] Failed to save '{controller.DomainId}': {exception.Message}");
                }
            }

            this._flushBuffer.Clear();
            this._elapsedSeconds = 0f;
        }

        public void Dispose()
        {
            if (this._isDisposed)
                return;

            // Flush before tearing down, and before the container disposes the repositories
            // themselves - by then their data is already released.
            this.FlushAll();

            if (this._isRegistered)
            {
                UpdateServiceManager.DeregisterUpdateHandler(this);

                if (this._relay != null)
                {
                    this._relay.OnSuspending -= this.FlushAll;
                    this._relay.OnQuitting -= this.FlushAll;
                }

                this._isRegistered = false;
            }

            this._dirty.Clear();
            this._isDisposed = true;
        }
    }
}