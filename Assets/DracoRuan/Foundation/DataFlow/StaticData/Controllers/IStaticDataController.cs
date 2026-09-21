using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DracoRuan.Foundation.DataFlow.StaticData.Controllers
{
    /// <summary>
    /// A read-only repository for one static data table: loads it from the first source that can
    /// supply a valid copy, and exposes it.
    /// </summary>
    /// <remarks>
    /// <para><b>Loading is an awaited step, not something a constructor starts.</b> The previous base
    /// classes called <c>InitializeData(...).Forget()</c> from their constructor. That lost every
    /// exception, gave callers nothing to await, and — because the method read an abstract property —
    /// ran before the subclass's own constructor body had assigned its fields. The save half of
    /// DataFlow removed exactly this pattern; see <c>Runtime/IDataController</c>.</para>
    ///
    /// <para><b><see cref="IsInitialized"/> means "there is valid data".</b> Not "loading finished".
    /// The old flag was set even when every source in the chain had failed and the data was null, so
    /// nothing downstream could tell a loaded table from a broken one.</para>
    /// </remarks>
    public interface IStaticDataController : IDisposable
    {
        /// <summary>
        /// Permanent identifier for this table, used by logs, the registry and Editor tooling.
        /// </summary>
        /// <remarks>Return a constant, never <c>nameof</c>: renaming the class would rename the id.</remarks>
        string DataId { get; }

        /// <summary>The type this controller exposes.</summary>
        Type DataType { get; }

        /// <summary>True once valid data is available.</summary>
        bool IsInitialized { get; }

        /// <summary>Which source won last time, and why the others did not. Never null.</summary>
        StaticDataLoadResult LastLoadResult { get; }

        /// <summary>
        /// Raised when data becomes available. Subscribing after the load has already happened
        /// invokes the handler immediately, so a consumer resolved later in boot cannot miss it —
        /// with a plain event it would subscribe after the event had fired and wait forever.
        /// </summary>
        event Action OnDataLoaded;

        /// <summary>
        /// Runs the fallback chain. Does nothing when already initialized; use
        /// <see cref="ReloadAsync"/> to pick up changed remote values.
        /// </summary>
        UniTask InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs the chain again — after a remote config push, or from an Editor tool.
        /// </summary>
        /// <remarks>
        /// The currently loaded data is kept if the reload fails, so a bad push degrades to "the
        /// config did not change" rather than taking a live session's data away.
        /// </remarks>
        UniTask ReloadAsync(CancellationToken cancellationToken = default);
    }
}
