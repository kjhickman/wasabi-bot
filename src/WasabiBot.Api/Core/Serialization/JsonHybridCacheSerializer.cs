using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Caching.Hybrid;

namespace WasabiBot.Api.Core.Serialization;

internal sealed class JsonHybridCacheSerializer<T>(JsonTypeInfo<T> typeInfo) : IHybridCacheSerializer<T>
{
    public T Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new Utf8JsonReader(source);
        return JsonSerializer.Deserialize(ref reader, typeInfo)!;
    }

    public void Serialize(T value, IBufferWriter<byte> target)
    {
        using var writer = new Utf8JsonWriter(target);
        JsonSerializer.Serialize(writer, value, typeInfo);
    }
}
