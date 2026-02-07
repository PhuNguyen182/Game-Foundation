using System;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Serialization.CSVTypeConverter
{
    public class Vector2Converter : UnityTypeConverter<Vector2>
    {
        [ThreadStatic]
        private static StringBuilder _stringBuilder;

        protected override Vector2 ConvertFromStringTyped(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrEmpty(text))
                return Vector2.zero;

            Span<float> values = stackalloc float[2];
            int valueIndex = 0;
            int startIndex = 0;

            ReadOnlySpan<char> span = text.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (c is not (',' or ';'))
                    continue;

                if (valueIndex >= 2)
                    return Vector2.zero;

                var numberSpan = span.Slice(startIndex, i - startIndex);
                if (!float.TryParse(numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out values[valueIndex]))
                    return Vector2.zero;

                valueIndex++;
                startIndex = i + 1;
            }

            if (valueIndex >= 2)
                return new Vector2(values[0], values[1]);

            var lastSpan = span[startIndex..];
            return !float.TryParse(lastSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out values[valueIndex])
                ? Vector2.zero
                : new Vector2(values[0], values[1]);
        }

        protected override string ConvertToStringTyped(Vector2 value, IWriterRow row, MemberMapData memberMapData)
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
