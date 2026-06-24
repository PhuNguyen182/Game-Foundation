using System;
using Random = UnityEngine.Random;

namespace DracoRuan.Utilities.Miscs
{
    public static class EnumUtils
    {
        public static int Count<TEnum>() where TEnum : Enum => GetValues<TEnum>().Length;
        public static TEnum[] GetValues<TEnum>() => (TEnum[])Enum.GetValues(typeof(TEnum));

        public static TEnum GetRandomValue<TEnum>() where TEnum : Enum =>
            GetValues<TEnum>()[Random.Range(0, Count<TEnum>())];

        public static TEnum GetFromString<TEnum>(string valueName) where TEnum : Enum
        {
            TEnum[] values = GetValues<TEnum>();
            foreach (TEnum value in values)
            {
                if (string.CompareOrdinal(value.ToString(), valueName) == 0)
                    return value;
            }

            throw new ArgumentException($"Value '{valueName}' not found in enum {typeof(TEnum).Name}.");
        }
    }
}