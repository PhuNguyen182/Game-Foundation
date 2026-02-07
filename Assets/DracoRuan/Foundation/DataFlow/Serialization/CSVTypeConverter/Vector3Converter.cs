using System;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Serialization.CSVTypeConverter
{
    public class Vector3Converter : UnityTypeConverter<Vector3>
    {
        [ThreadStatic]
        private static StringBuilder _stringBuilder;

        protected override Vector3 ConvertFromStringTyped(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrEmpty(text))
                return Vector3.zero;

            Span<float> values = stackalloc float[3];
            int valueIndex = 0;
            int startIndex = 0;

            ReadOnlySpan<char> span = text.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (c is not (',' or ';'))
                    continue;

                if (valueIndex >= 3)
                    return Vector3.zero;

                var numberSpan = span.Slice(startIndex, i - startIndex);
                if (!float.TryParse(numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out values[valueIndex]))
                    return Vector3.zero;

                valueIndex++;
                startIndex = i + 1;
            }

            if (valueIndex >= 3)
                return new Vector3(values[0], values[1], values[2]);

            var lastSpan = span[startIndex..];
            return !float.TryParse(lastSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out values[valueIndex])
                ? Vector3.zero
                : new Vector3(values[0], values[1], values[2]);
        }

        protected override string ConvertToStringTyped(Vector3 value, IWriterRow row, MemberMapData memberMapData)
        {
            _stringBuilder ??= new StringBuilder(32);
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
