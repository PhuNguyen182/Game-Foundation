using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;

namespace DracoRuan.Foundation.DataFlow.LocalData.StaticDataControllers.CSVs
{
    public static class CsvHelperUtil<TRecord, TRecordMap>
        where TRecord : class
        where TRecordMap : ClassMap<TRecord>
    {
        private static readonly CsvConfiguration CsvConfiguration;

        static CsvHelperUtil()
        {
            CsvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
            };
        }

        public static IEnumerable<TRecord> GetRecordsFromCsv(string csvText)
        {
            if (string.IsNullOrEmpty(csvText))
                return Enumerable.Empty<TRecord>();

            StringReader stringReader = new StringReader(csvText);
            CsvReader csvReader = new CsvReader(stringReader, CsvConfiguration);

            try
            {
                csvReader.Context.RegisterClassMap<TRecordMap>();
                IEnumerable<TRecord> records = csvReader.GetRecords<TRecord>().ToArray();
                return records;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                stringReader.Dispose();
                csvReader.Dispose();
            }

            return Enumerable.Empty<TRecord>();
        }
    }
}
