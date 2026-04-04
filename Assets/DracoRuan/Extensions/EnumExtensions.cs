using System;

namespace DracoRuan.Extensions
{
    public static class EnumExtensions
    {
        public static int TotalElementCount<TEnum>(TEnum enumValue) where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum)).Length;
        }

        public static TEnum[] GetValues<TEnum>() where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum)) as TEnum[];
        }
    }
}
