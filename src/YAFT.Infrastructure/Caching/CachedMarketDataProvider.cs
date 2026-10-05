using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Domain.Stocks;

namespace YAFT.Infrastructure.Caching;

/// <summary>
/// Decorator con HybridCache attorno al provider reale: le richieste concorrenti sulla stessa chiave
/// producono una sola chiamata a Yahoo e gli errori non vengono messi in cache.
/// </summary>
internal sealed class CachedMarketDataProvider(
    IMarketDataProvider inner,
    HybridCache cache,
    IOptions<MarketDataCacheOptions> options) : IMarketDataProvider
{
    private readonly MarketDataCacheOptions _ttl = options.Value;

    public Task<Result<StockQuote>> GetQuoteAsync(StockSymbol symbol, CancellationToken ct) =>
        GetOrFetchAsync($"quote:{symbol.Value}", _ttl.QuoteTtl,
            token => inner.GetQuoteAsync(symbol, token), ct);

    public Task<Result<PriceHistory>> GetHistoryAsync(
        StockSymbol symbol, string range, string interval, CancellationToken ct) =>
        GetOrFetchAsync($"history:{symbol.Value}:{range}:{interval}", _ttl.HistoryTtl,
            token => inner.GetHistoryAsync(symbol, range, interval, token), ct);

    public Task<Result<IReadOnlyList<SymbolSearchResult>>> SearchAsync(
        string query, int count, CancellationToken ct) =>
        GetOrFetchAsync($"search:{query.Trim().ToLowerInvariant()}:{count}", _ttl.SearchTtl,
            token => inner.SearchAsync(query, count, token), ct);

    public Task<Result<IReadOnlyList<SymbolSearchResult>>> GetListAsync(
        StockListType listType, string region, int count, CancellationToken ct) =>
        GetOrFetchAsync($"list:{listType}:{region}:{count}", _ttl.ListTtl,
            token => inner.GetListAsync(listType, region, count, token), ct);

    private async Task<Result<T>> GetOrFetchAsync<T>(
        string key, TimeSpan ttl, Func<CancellationToken, Task<Result<T>>> fetch, CancellationToken ct)
    {
        try
        {
            return await cache.GetOrCreateAsync(
                key,
                fetch,
                static async (f, token) =>
                {
                    var result = await f(token);
                    // Un'eccezione nella factory impedisce la scrittura in cache.
                    return result.IsSuccess ? result.Value : throw new ProviderErrorException(result.Error!);
                },
                new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = ttl },
                cancellationToken: ct);
        }
        catch (ProviderErrorException ex)
        {
            return ex.Error;
        }
    }

    private sealed class ProviderErrorException(Error error) : Exception(error.Description)
    {
        public Error Error { get; } = error;
    }
}
