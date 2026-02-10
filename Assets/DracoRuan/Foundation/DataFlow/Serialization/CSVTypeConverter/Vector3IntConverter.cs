using System;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Serialization.CSVTypeConverter
{
    public class Vector3IntConverter : UnityTypeConverter<Vector3Int>
    {
        [ThreadStatic]
        private static StringBuilder _stringBuilder;

        protected override Vector3Int ConvertFromStringTyped(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrEmpty(text))
                return Vector3Int.zero;

            Span<int> values = stackalloc int[3];
            int valueIndex = 0;
            int startIndex = 0;

            ReadOnlySpan<char> span = text.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (c is not (',' or ';'))
                    continue;

                if (valueIndex >= 3)
                    return Vector3Int.zero;

                var numberSpan = span.Slice(startIndex, i - startIndex);
                if (!int.TryParse(numberSpan, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out values[valueIndex]))
                    return Vector3Int.zero;

                valueIndex++;
                startIndex = i + 1;
            }

            if (valueIndex >= 3)
                return new Vector3Int(values[0], values[1], values[2]);

            ReadOnlySpan<char> lastSpan = span[startIndex..];
            return !int.TryParse(lastSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out values[valueIndex])
                ? Vector3Int.zero
                : new Vector3Int(values[0], values[1], values[2]);
        }

        protected override string ConvertToStringTyped(Vector3Int value, IWriterRow row, MemberMapData memberMapData)
        {
            _stringBuilder ??= new StringBuilder(48);
            _stringBuilder.Clear();

            _stringBuilder.Append(value.x);
            _stringBuilder.Append(',');
            _stringBuilder.Append(value.y);
            _stringBuilder.Append(',');
            _stringBuilder.Append(value.z);

            return _stringBuilder.ToString();
        }
    }
}
