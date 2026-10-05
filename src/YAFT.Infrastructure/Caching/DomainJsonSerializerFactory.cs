using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using YAFT.Domain.Stocks;

namespace YAFT.Infrastructure.Caching;

/// <summary>
/// Serializzatore JSON per HybridCache che sa gestire i value object del dominio (StockSymbol),
/// così le entità restano libere da attributi di serializzazione. Vale anche per un futuro L2 (Redis).
/// </summary>
internal sealed class DomainJsonSerializerFactory : IHybridCacheSerializerFactory
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new StockSymbolConverter() }
    };

    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        serializer = new Serializer<T>();
        return true;
    }

    private sealed class Serializer<T> : IHybridCacheSerializer<T>
    {
        public T Deserialize(ReadOnlySequence<byte> source)
        {
            var reader = new Utf8JsonReader(source);
            return JsonSerializer.Deserialize<T>(ref reader, Options)!;
        }

        public void Serialize(T value, IBufferWriter<byte> target)
        {
            using var writer = new Utf8JsonWriter(target);
            JsonSerializer.Serialize(writer, value, Options);
        }
    }

    private sealed class StockSymbolConverter : JsonConverter<StockSymbol>
    {
        public override StockSymbol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            StockSymbol.Create(reader.GetString());

        public override void Write(Utf8JsonWriter writer, StockSymbol value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
