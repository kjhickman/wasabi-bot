using System.Buffers;
using System.Buffers.Binary;
using Microsoft.Extensions.Caching.Hybrid;

namespace WasabiBot.Api.Core.Serialization;

internal sealed class BooleanHybridCacheSerializer : IHybridCacheSerializer<bool>
{
    public bool Deserialize(ReadOnlySequence<byte> source)
    {
        return source.FirstSpan[0] != 0;
    }

    public void Serialize(bool value, IBufferWriter<byte> target)
    {
        target.GetSpan(1)[0] = value ? (byte)1 : (byte)0;
        target.Advance(1);
    }
}

internal sealed class UInt64ArrayHybridCacheSerializer : IHybridCacheSerializer<ulong[]>
{
    public ulong[] Deserialize(ReadOnlySequence<byte> source)
    {
        var values = new ulong[source.Length / sizeof(ulong)];
        var index = 0;
        foreach (var memory in source)
        {
            var span = memory.Span;
            while (span.Length >= sizeof(ulong))
            {
                values[index++] = BinaryPrimitives.ReadUInt64LittleEndian(span);
                span = span[sizeof(ulong)..];
            }
        }

        return values;
    }

    public void Serialize(ulong[] value, IBufferWriter<byte> target)
    {
        var span = target.GetSpan(value.Length * sizeof(ulong));
        for (var i = 0; i < value.Length; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(span[(i * sizeof(ulong))..], value[i]);
        }

        target.Advance(value.Length * sizeof(ulong));
    }
}
