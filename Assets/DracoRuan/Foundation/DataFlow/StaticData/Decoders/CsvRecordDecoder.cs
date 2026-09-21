#if USE_CSV_HELPER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using ZLinq;

namespace DracoRuan.Foundation.DataFlow.StaticData.Decoders
{
    /// <summary>
    /// Parses a CSV payload into records, whatever source the text came from.
    /// </summary>
    /// <remarks>
    /// <para><b>A parse failure is not an empty table.</b> The predecessor returned
    /// <c>Array.Empty</c> for both, and the caller treated any non-null result as success — so a
    /// malformed CSV ended the fallback chain with zero rows and the game ran on an empty config
    /// table with nothing in the log to say why. Here the two outcomes are different values.</para>
    ///
    /// <para>Records are materialised with <c>ToArray()</c> before the reader is disposed.
    /// <c>CsvReader.GetRecords</c> is lazily evaluated and reads from the underlying
    /// <see cref="StringReader"/> on enumeration, so returning it directly hands back a sequence that
    /// throws the moment anyone touches it.</para>
    /// </remarks>
    public sealed class CsvRecordDecoder<TRecord, TRecordMap> : IStaticDataDecoder<IReadOnlyList<TRecord>>
        where TRecord : class
        where TRecordMap : ClassMap<TRecord>
    {
        private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ","
        };

        public DecodeResult<IReadOnlyList<TRecord>> Decode(in StaticDataPayload payload)
        {
            if (payload.Kind != StaticDataPayloadKind.Text)
                return DecodeResult<IReadOnlyList<TRecord>>.Failure(
                    $"Expected a text payload for {typeof(TRecord).Name} records, got {payload.Kind}.");

            try
            {
                using StringReader stringReader = new(payload.Text);
                using CsvReader csvReader = new(stringReader, Configuration);
                csvReader.Context.RegisterClassMap<TRecordMap>();

                TRecord[] records = csvReader.GetRecords<TRecord>().AsValueEnumerable().ToArray();

                // Zero rows is a real, successful answer here - a table can legitimately be empty,
                // and the controller decides whether that is acceptable for its domain.
                return DecodeResult<IReadOnlyList<TRecord>>.Success(records, ownsValue: false);
            }
            catch (Exception exception)
            {
                return DecodeResult<IReadOnlyList<TRecord>>.Failure(
                    $"CSV did not parse as {typeof(TRecord).Name}: {exception.Message}");
            }
        }
    }
}
#endif
