using System;

namespace SharpMC.Network.Util
{
    public static class BinaryTool
    {
        public static int[] ToIntArray(long value)
        {
            Span<byte> sp = stackalloc byte[8];
            BitConverter.TryWriteBytes(sp, value);
            var first = BitConverter.ToInt32(sp[4..]);
            var second = BitConverter.ToInt32(sp[..4]);
            return new[] {first, second};
        }

        public static long ToLong(int[] value)
        {
            var first = (uint) value[0];
            var second = (uint) value[1];
            var unsignedKey = ((ulong) first << 32) | second;
            return (long) unsignedKey;
        }
    }
}