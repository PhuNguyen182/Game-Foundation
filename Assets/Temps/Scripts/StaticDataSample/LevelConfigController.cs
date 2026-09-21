#if USE_CSV_HELPER
using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.StaticData;
using DracoRuan.Foundation.DataFlow.StaticData.Controllers;

namespace Temps.Scripts.StaticDataSample
{
    /// <summary>
    /// Sample of the record branch: a CSV table indexed by level id.
    /// </summary>
    /// <remarks>
    /// <para>The chain below is the shape a live game ends up with — a bucket the designers can
    /// re-upload to, an Addressables copy that ships with a content update, and a Resources copy that
    /// can never be missing. Every link feeds the same CSV decoder, so swapping R2 for S3 is one line
    /// here and nothing anywhere else.</para>
    ///
    /// <para>The URL link is commented out rather than pointed at a placeholder: an unreachable host
    /// would make every boot pay the download timeout before falling through.</para>
    /// </remarks>
    [StaticDataId(Id)]
    public sealed class LevelConfigController
        : StaticRecordDataController<LevelConfigRecord, LevelConfigRecordMap, int>
    {
        public const string Id = "level_config";

        public LevelConfigController(StaticDataControllerContext context) : base(context)
        {
        }

        public override string DataId => Id;

        protected override IReadOnlyList<StaticDataSourceBinding> Sources { get; } =
            StaticDataSourceBinding.Chain(
                // StaticDataSourceBinding.Url("https://<bucket>.r2.cloudflarestorage.com/level_config.csv"),
                StaticDataSourceBinding.Addressable("Configs/LevelConfigData"),
                StaticDataSourceBinding.Resources("Configs/LevelConfigData"));

        protected override int GetRecordKey(LevelConfigRecord record) => record.LevelId;

        protected override bool Validate(IReadOnlyList<LevelConfigRecord> records, out string failureReason)
        {
            if (records.Count == 0)
            {
                // Empty parses fine, so only the table's owner can say whether it is acceptable.
                // Here it is not: a level table with no levels leaves nothing to play.
                failureReason = "The level table is empty.";
                return false;
            }

            failureReason = null;
            return true;
        }
    }
}
#endif
