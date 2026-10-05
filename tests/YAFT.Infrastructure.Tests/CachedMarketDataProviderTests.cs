using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Application.Stocks;
using YAFT.Domain.Stocks;
using YAFT.Infrastructure.Caching;

namespace YAFT.Infrastructure.Tests;

public class CachedMarketDataProviderTests
{
    private readonly CountingProvider _inner = new();
    private readonly CachedMarketDataProvider _sut;

    public CachedMarketDataProviderTests()
    {
        var services = new ServiceCollection();
        services.AddHybridCache().AddSerializerFactory<DomainJsonSerializerFactory>();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();

        _sut = new CachedMarketDataProvider(_inner, cache, Options.Create(new MarketDataCacheOptions()));
    }

    private static StockSymbol Aapl => StockSymbol.Create("AAPL");

    [Fact]
    public async Task Seconda_richiesta_servita_dalla_cache()
    {
        var first = await _sut.GetQuoteAsync(Aapl, CancellationToken.None);
        var second = await _sut.GetQuoteAsync(Aapl, CancellationToken.None);

        Assert.Equal(1, _inner.QuoteCalls);
        Assert.Equal(first.Value.Price, second.Value.Price);
        Assert.Equal(Aapl, second.Value.Symbol);
        Assert.Equal(first.Value.ChangePercent, second.Value.ChangePercent);
    }

    [Fact]
    public async Task Gli_errori_non_vengono_messi_in_cache()
    {
        _inner.Fail = true;
        var failed = await _sut.GetQuoteAsync(Aapl, CancellationToken.None);
        Assert.Equal(ErrorType.Unavailable, failed.Error!.Type);

        _inner.Fail = false;
        var ok = await _sut.GetQuoteAsync(Aapl, CancellationToken.None);

        Assert.True(ok.IsSuccess);
        Assert.Equal(2, _inner.QuoteCalls);
    }

    [Fact]
    public async Task Richieste_concorrenti_producono_una_sola_chiamata()
    {
        _inner.Delay = TimeSpan.FromMilliseconds(200);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => _sut.GetQuoteAsync(Aapl, CancellationToken.None)));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal(1, _inner.QuoteCalls);
    }

    [Fact]
    public async Task Chiavi_diverse_per_simboli_e_liste_diverse()
    {
        await _sut.GetQuoteAsync(Aapl, CancellationToken.None);
        await _sut.GetQuoteAsync(StockSymbol.Create("MSFT"), CancellationToken.None);
        await _sut.GetListAsync(StockListType.MostActives, "US", 25, CancellationToken.None);
        await _sut.GetListAsync(StockListType.MostActives, "US", 25, CancellationToken.None);
        await _sut.GetListAsync(StockListType.MostActives, "IT", 25, CancellationToken.None);

        Assert.Equal(2, _inner.QuoteCalls);
        Assert.Equal(2, _inner.ListCalls);
    }

    [Fact]
    public async Task Storico_e_liste_sopravvivono_alla_serializzazione()
    {
        await _sut.GetHistoryAsync(Aapl, "1mo", "1d", CancellationToken.None);
        var history = await _sut.GetHistoryAsync(Aapl, "1mo", "1d", CancellationToken.None);

        Assert.Equal(Aapl, history.Value.Symbol);
        Assert.Equal(2, history.Value.Points.Count);
        Assert.Equal(101m, history.Value.Points[1].Close);

        await _sut.SearchAsync("Eni", 10, CancellationToken.None);
        var search = await _sut.SearchAsync("eni", 10, CancellationToken.None);
        Assert.Equal("ENI.MI", search.Value.Single().Symbol);
        Assert.Equal(1, _inner.SearchCalls);
    }

    private sealed class CountingProvider : IMarketDataProvider
    {
        private int _quoteCalls, _listCalls, _searchCalls;
        public int QuoteCalls => _quoteCalls;
        public int ListCalls => _listCalls;
        public int SearchCalls => _searchCalls;
        public bool Fail { get; set; }
        public TimeSpan Delay { get; set; }

        public async Task<Result<StockQuote>> GetQuoteAsync(StockSymbol symbol, CancellationToken ct)
        {
            Interlocked.Increment(ref _quoteCalls);
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
            if (Fail) return StockErrors.Unavailable;
            return new StockQuote(symbol, "Name", "USD", "NMS", 110m, 100m, 111m, 99m, 1000, DateTimeOffset.UtcNow);
        }

        public Task<Result<PriceHistory>> GetHistoryAsync(StockSymbol symbol, string range, string interval, CancellationToken ct) =>
            Task.FromResult<Result<PriceHistory>>(new PriceHistory(symbol, "USD", range, interval,
            [
                new PricePoint(DateTimeOffset.UtcNow.AddDays(-1), 99m, 102m, 98m, 100m, 10),
                new PricePoint(DateTimeOffset.UtcNow, 100m, 103m, 99m, 101m, null)
            ]));

        public Task<Result<IReadOnlyList<SymbolSearchResult>>> SearchAsync(string query, int count, CancellationToken ct)
        {
            Interlocked.Increment(ref _searchCalls);
            return Task.FromResult(Result<IReadOnlyList<SymbolSearchResult>>.Success(
                [new SymbolSearchResult("ENI.MI", "Eni", "Milan", "EQUITY")]));
        }

        public Task<Result<IReadOnlyList<SymbolSearchResult>>> GetListAsync(StockListType listType, string region, int count, CancellationToken ct)
        {
            Interlocked.Increment(ref _listCalls);
            return Task.FromResult(Result<IReadOnlyList<SymbolSearchResult>>.Success(
                [new SymbolSearchResult("NVDA", "NVIDIA", "NasdaqGS", "EQUITY", 120m, 1.5m)]));
        }
    }
}
