using System;

namespace DracoRuan.Utilities.Miscs
{
    public static class NumericUtils
    {
        public static byte[] IntToBytes(int value) => BitConverter.GetBytes(value);
        
        public static byte[] LongToByte(long value) => BitConverter.GetBytes(value);
        
        public static int BytesToInt(byte[] code) => BitConverter.ToInt32(code, 0);
        
        public static long BytesToLong(byte[] code) => BitConverter.ToInt64(code, 0);
    }
}
