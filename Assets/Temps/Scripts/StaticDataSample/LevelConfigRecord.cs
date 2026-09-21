#if USE_CSV_HELPER
using CsvHelper.Configuration;

namespace Temps.Scripts.StaticDataSample
{
    /// <summary>One row of the sample level table.</summary>
    public sealed class LevelConfigRecord
    {
        public int LevelId { get; set; }
        public string DisplayName { get; set; }
        public int TargetScore { get; set; }
        public float TimeLimitSeconds { get; set; }
    }

    /// <summary>
    /// Maps CSV columns onto <see cref="LevelConfigRecord"/>.
    /// </summary>
    /// <remarks>
    /// Mapped by header name rather than index so inserting a column does not silently shift every
    /// field one to the left — which parses cleanly and produces entirely wrong data.
    /// </remarks>
    public sealed class LevelConfigRecordMap : ClassMap<LevelConfigRecord>
    {
        public LevelConfigRecordMap()
        {
            this.Map(record => record.LevelId).Name("LevelId");
            this.Map(record => record.DisplayName).Name("DisplayName");
            this.Map(record => record.TargetScore).Name("TargetScore");
            this.Map(record => record.TimeLimitSeconds).Name("TimeLimitSeconds");
        }
    }
}
#endif
