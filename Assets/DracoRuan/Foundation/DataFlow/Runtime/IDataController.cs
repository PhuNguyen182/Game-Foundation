using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.Runtime
{
    /// <summary>
    /// A save repository: owns one data model, loads it, exposes it, and persists it.
    /// </summary>
    /// <remarks>
    /// Loading is an explicit awaited step rather than something a constructor kicks off. The old
    /// design started a fire-and-forget task in the constructor, which meant exceptions vanished,
    /// nothing could await readiness, and the game could reach the first scene with saves still
    /// half-loaded.
    /// </remarks>
    public interface IDataController : IDisposable
    {
        /// <summary>Permanent identifier for this repository's save file.</summary>
        string DomainId { get; }

        /// <summary>Schema version this build expects.</summary>
        int SchemaVersion { get; }

        /// <summary>The data model's type.</summary>
        Type DataType { get; }

        /// <summary>True once <see cref="InitializeAsync"/> has completed.</summary>
        bool IsInitialized { get; }

        /// <summary>True when there are unsaved changes.</summary>
        bool IsDirty { get; }

        /// <summary>Raised after the data has been loaded or reloaded.</summary>
        event Action OnDataLoaded;

        /// <summary>
        /// Loads the save, or creates defaults when there is none. Waits for boot-time migration
        /// before touching the disk.
        /// </summary>
        UniTask InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>Marks the data as changed so the scheduler will persist it.</summary>
        void MarkDirty();

        /// <summary>
        /// Persists immediately, on the calling thread. Used by the shutdown path, where the
        /// process may be frozen within a few hundred milliseconds and an async write would never
        /// resume.
        /// </summary>
        void Save();

        /// <summary>Deletes every saved version of this domain.</summary>
        void Delete();
    }
}
