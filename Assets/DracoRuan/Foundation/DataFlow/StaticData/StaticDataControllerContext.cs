using System;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;

namespace DracoRuan.Foundation.DataFlow.StaticData
{
    /// <summary>
    /// Everything a static data controller needs, bundled into one injected object.
    /// </summary>
    /// <remarks>
    /// Same reasoning as <c>Runtime/DataControllerContext</c> on the save side: a base class that
    /// takes its dependencies as separate constructor parameters forces every subclass in every game
    /// to accept and forward them, so adding a dependency later — a validation service, a config
    /// version check — means editing every controller that was ever written against this foundation.
    /// Adding a property here costs nothing downstream.
    /// </remarks>
    public sealed class StaticDataControllerContext
    {
        public StaticDataControllerContext(IStaticDataSourceRegistry sources)
        {
            this.Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        }

        /// <summary>Resolves a <see cref="StaticDataSourceType"/> to the source that reads it.</summary>
        public IStaticDataSourceRegistry Sources { get; }
    }
}
