using DracoRuan.Foundation.DataFlow.LocalData;
using DracoRuan.Foundation.DataFlow.Runtime;

namespace Temps.Scripts.TestRiseProgressData
{
    /// <summary>
    /// Repository for rise progression data.
    /// </summary>
    /// <remarks>
    /// The whole repository: a domain id, a schema version, and a constructor that forwards one
    /// context. Migration, versioning, atomic writes and autosave are all handled by the base class
    /// and the boot pipeline, so a controller only describes what makes it different.
    /// </remarks>
    [DynamicGameDataController(Id)]
    public sealed class RiseProgressionDataController : DynamicGameDataController<RiseProgressDataV1>
    {
        /// <summary>
        /// Permanent save identifier. A constant rather than a type name: renaming or moving the
        /// data class must never orphan a player's save file.
        /// </summary>
        public const string Id = "rise_progression";

        public RiseProgressionDataController(DataControllerContext context) : base(context)
        {
        }

        public override string DomainId => Id;

        public override int SchemaVersion => 1;
    }
}