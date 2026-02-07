using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper;

namespace DracoRuan.Foundation.DataFlow.ProcessingSequence.CustomDataProcessor
{
    public static class CsvHelperUtil<TRecord>
    {
        private const string GetRecordsMethodName = "GetRecords";

        public static readonly CsvConfiguration CsvConfiguration;
        public static readonly Func<CsvReader, IEnumerable<TRecord>> GetRecordsFunc;

        static CsvHelperUtil()
        {
            CsvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };

            var method = typeof(CsvReader).GetMethod(GetRecordsMethodName, Type.EmptyTypes)
                ?.MakeGenericMethod(typeof(TRecord));

            if (method != null)
            {
                GetRecordsFunc = (Func<CsvReader, IEnumerable<TRecord>>)
                    Delegate.CreateDelegate(typeof(Func<CsvReader, IEnumerable<TRecord>>), method);
            }
        }
        
        public static IEnumerable<TRecord> ParseCsv(string csvText)
        {
            if (string.IsNullOrEmpty(csvText) || GetRecordsFunc == null)
                return Enumerable.Empty<TRecord>();

            using StringReader stringReader = new(csvText);
            using CsvReader csvReader = new(stringReader, CsvConfiguration);
            return GetRecordsFunc(csvReader);
        }
    }
}
