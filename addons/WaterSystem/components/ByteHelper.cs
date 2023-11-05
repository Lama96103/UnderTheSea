using System;
using System.Linq;

namespace WaterSystem;


public static class ByteHelper
{
    public static byte[] Combine(params byte[][] arrays)
    {
        byte[] ret = new byte[arrays.Sum(x => x.Length)];
        int offset = 0;
        foreach (byte[] data in arrays)
        {
            Buffer.BlockCopy(data, 0, ret, offset, data.Length);
            offset += data.Length;
        }
        return ret;
    }

    public static byte[] GetBytes<T>(params T[] input) where T : struct
    {
        byte[] result = new byte[input.Length * SizeOf<T>()];
        Buffer.BlockCopy(input, 0, result, 0, result.Length);
        return result;
    }

    public static int SizeOf<T>() where T : struct
    {
        return System.Runtime.InteropServices.Marshal.SizeOf(default(T));
    }
}