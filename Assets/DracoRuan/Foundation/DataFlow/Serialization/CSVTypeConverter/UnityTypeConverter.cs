using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace DracoRuan.Foundation.DataFlow.Serialization.CSVTypeConverter
{
    public abstract class UnityTypeConverter<T> : ITypeConverter where T : struct
    {
        public object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            return this.ConvertFromStringTyped(text, row, memberMapData);
        }

        public string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value is T typed)
                return this.ConvertToStringTyped(typed, row, memberMapData);
            return string.Empty;
        }
        
        protected abstract T ConvertFromStringTyped(string text, IReaderRow row, MemberMapData memberMapData);
        protected abstract string ConvertToStringTyped(T value, IWriterRow row, MemberMapData memberMapData);

    }
}
