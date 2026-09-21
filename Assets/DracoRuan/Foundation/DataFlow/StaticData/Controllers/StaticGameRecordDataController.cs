#if USE_CSV_HELPER
using System;
using System.Collections.Generic;
using CsvHelper.Configuration;
using DracoRuan.Foundation.DataFlow.StaticData.Decoders;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;

namespace DracoRuan.Foundation.DataFlow.StaticData.Controllers
{
    /// <summary>
    /// Base class for a table of records parsed from CSV, indexed by a key of your choosing.
    ///
    /// <code>
    /// [StaticDataController(BrainProgramDayController.Id)]
    /// public sealed class BrainProgramDayController
    ///     : StaticRecordDataController&lt;BrainProgramDayRecord, BrainProgramDayRecordMap, int&gt;
    /// {
    ///     public const string Id = "brain_program_day";
    ///
    ///     public BrainProgramDayController(StaticDataControllerContext context) : base(context) { }
    ///
    ///     public override string DataId =&gt; Id;
    ///
    ///     protected override int GetRecordKey(BrainProgramDayRecord record) =&gt; record.Id;
    ///
    ///     protected override IReadOnlyList&lt;StaticDataSourceBinding&gt; Sources { get; } =
    ///         StaticDataSourceBinding.Chain(
    ///             StaticDataSourceBinding.Url("https://cdn.example.com/brain_program_day.csv"),
    ///             StaticDataSourceBinding.Addressable("Configs/BrainProgramDayConfigData"),
    ///             StaticDataSourceBinding.Resources("BrainProgramDayConfigData"));
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para><b>The index is built here, not by each subclass.</b> The predecessor exposed records as
    /// a bare <c>IEnumerable&lt;T&gt;</c> and left every controller to write its own
    /// <c>RefineDataFromSourceData()</c> — so each one reimplemented the same dictionary, and none of
    /// them checked for duplicate ids. A table with a repeated id would silently keep whichever row
    /// the implementation happened to write last.</para>
    ///
    /// <para><b>Deliberately not a ScriptableObject.</b> Records arrive as CSV text from every source
    /// this branch supports, so a container asset would exist only to be filled in at runtime and
    /// never authored. That removed the old <c>CustomRecordData&lt;T&gt;</c> +
    /// <c>ISetCustomCsvRecordGameData&lt;T&gt;</c> pair along with the <c>new()</c> constraint they
    /// forced on every data class.</para>
    /// </remarks>
    public abstract class StaticRecordDataController<TRecord, TRecordMap, TKey>
        : StaticDataControllerBase<IReadOnlyList<TRecord>>
        where TRecord : class
        where TRecordMap : ClassMap<TRecord>
    {
        private static readonly TRecord[] NoRecords = new TRecord[0];
        private static readonly Dictionary<TKey, TRecord> NoIndex = new();

        private IStaticDataDecoder<IReadOnlyList<TRecord>> _csvDecoder;
        private Dictionary<TKey, TRecord> _recordsById = NoIndex;

        protected StaticRecordDataController(StaticDataControllerContext context) : base(context)
        {
        }

        public override Type DataType => typeof(TRecord);

        /// <summary>Every row, in file order. Empty rather than null before the load completes.</summary>
        public IReadOnlyList<TRecord> Records => this.Value ?? NoRecords;

        /// <summary>Rows by key. Empty rather than null before the load completes.</summary>
        public IReadOnlyDictionary<TKey, TRecord> RecordsById => this._recordsById;

        /// <summary>The primary key of a row. Must be unique across the table.</summary>
        protected abstract TKey GetRecordKey(TRecord record);

        public bool TryGet(TKey key, out TRecord record) => this._recordsById.TryGetValue(key, out record);

        protected override IStaticDataDecoder<IReadOnlyList<TRecord>> GetDecoder(StaticDataPayloadKind payloadKind)
        {
            // CSV is text wherever it came from - a Resources TextAsset, an Addressables TextAsset, or
            // a body downloaded from a bucket. Adding S3 alongside R2 needs no change here.
            return payloadKind == StaticDataPayloadKind.Text
                ? this._csvDecoder ??= new CsvRecordDecoder<TRecord, TRecordMap>()
                : null;
        }

        /// <summary>
        /// Builds the index. Throwing rejects this source and lets the chain fall through, which is
        /// the right outcome: a table with duplicate ids is not one the game can run on, and the next
        /// source may well hold a good copy.
        /// </summary>
        protected override void OnValueAdopted(IReadOnlyList<TRecord> value)
        {
            Dictionary<TKey, TRecord> index = new(value.Count);

            for (int rowIndex = 0; rowIndex < value.Count; rowIndex++)
            {
                TRecord record = value[rowIndex];
                if (record == null)
                    continue;

                TKey key = this.GetRecordKey(record);
                if (key == null)
                    throw new InvalidOperationException(
                        $"'{this.DataId}' row {rowIndex} has no key.");

                if (!index.TryAdd(key, record))
                    throw new InvalidOperationException(
                        $"'{this.DataId}' has more than one row with key '{key}' (row {rowIndex}). " +
                        "Keys must be unique; the table would otherwise resolve to whichever row came last.");
            }

            this._recordsById = index;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this._recordsById = NoIndex;

            base.Dispose(disposing);
        }
    }
}
#endif
