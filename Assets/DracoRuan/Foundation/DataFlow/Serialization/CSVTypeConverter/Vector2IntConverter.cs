using System;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Serialization.CSVTypeConverter
{
    public class Vector2IntConverter : UnityTypeConverter<Vector2Int>
    {
        [ThreadStatic]
        private static StringBuilder _stringBuilder;

        protected override Vector2Int ConvertFromStringTyped(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrEmpty(text))
                return Vector2Int.zero;

            Span<int> values = stackalloc int[2];
            int valueIndex = 0;
            int startIndex = 0;

            ReadOnlySpan<char> span = text.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (c is not (',' or ';'))
                    continue;

                if (valueIndex >= 2)
                    return Vector2Int.zero;

                var numberSpan = span.Slice(startIndex, i - startIndex);
                if (!int.TryParse(numberSpan, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out values[valueIndex]))
                    return Vector2Int.zero;

                valueIndex++;
                startIndex = i + 1;
            }

            if (valueIndex >= 2)
                return new Vector2Int(values[0], values[1]);

            ReadOnlySpan<char> lastSpan = span[startIndex..];
            return !int.TryParse(lastSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out values[valueIndex])
                ? Vector2Int.zero
                : new Vector2Int(values[0], values[1]);
        }

        protected override string ConvertToStringTyped(Vector2Int value, IWriterRow row, MemberMapData memberMapData)
        {
            _stringBuilder ??= new StringBuilder(32);
            _stringBuilder.Clear();

            _stringBuilder.Append(value.x);
            _stringBuilder.Append(',');
            _stringBuilder.Append(value.y);

            return _stringBuilder.ToString();
        }
    }
}
