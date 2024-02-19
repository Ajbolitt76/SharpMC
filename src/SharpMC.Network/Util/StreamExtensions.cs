using System;
using System.Buffers;
using System.IO;

namespace SharpMC.Network.Util;

public static class StreamExtensions
{
    public static void WriteSequence(this Stream str, ReadOnlySequence<byte> source)
    {
        if (source.IsSingleSegment)
        {
            ReadOnlySpan<byte> span = source.First.Span;
            str.Write(span);
        }
        else
        {
            SequencePosition position = source.Start;
            while (source.TryGet(ref position, out ReadOnlyMemory<byte> memory))
            {
                ReadOnlySpan<byte> span = memory.Span;
                str.Write(span);
                if (position.GetObject() == null)
                {
                    break;
                }
            }
        }
    }
}