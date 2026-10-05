using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Application.Stocks;
using YAFT.Domain.Stocks;
using YAFT.Domain.Watchlist;

namespace YAFT.Application.Tests;

internal sealed class FakeCurrentUser(string? userId) : ICurrentUser
{
    public string? UserId { get; } = userId;
}

internal sealed class FakeMarketData : IMarketDataProvider
{
    /// Prezzi noti: ogni altro simbolo risulta inesistente.
    public Dictionary<string, decimal> Prices { get; } = new();
    public bool Unavailable { get; set; }
    public List<string> QuoteCalls { get; } = [];
    public (StockListType Type, string Region, int Count)? LastList { get; private set; }
    public (string Query, int Count)? LastSearch { get; private set; }

    public Task<Result<StockQuote>> GetQuoteAsync(StockSymbol symbol, CancellationToken ct)
    {
        lock (QuoteCalls) QuoteCalls.Add(symbol.Value);

        Result<StockQuote> result = Unavailable
            ? StockErrors.Unavailable
            : Prices.TryGetValue(symbol.Value, out var price)
                ? new StockQuote(symbol, symbol.Value, "USD", "NMS", price, price, null, null, null, null)
                : StockErrors.NotFound(symbol.Value);
        return Task.FromResult(result);
    }

    public Task<Result<PriceHistory>> GetHistoryAsync(StockSymbol symbol, string range, string interval, CancellationToken ct) =>
        Task.FromResult<Result<PriceHistory>>(new PriceHistory(symbol, "USD", range, interval, []));

    public Task<Result<IReadOnlyList<SymbolSearchResult>>> SearchAsync(string query, int count, CancellationToken ct)
    {
        LastSearch = (query, count);
        return Task.FromResult(Result<IReadOnlyList<SymbolSearchResult>>.Success(
            [new SymbolSearchResult("ENI.MI", "Eni S.p.A.", "Milan", "EQUITY")]));
    }

    public Task<Result<IReadOnlyList<SymbolSearchResult>>> GetListAsync(StockListType listType, string region, int count, CancellationToken ct)
    {
        LastList = (listType, region, count);
        return Task.FromResult(Result<IReadOnlyList<SymbolSearchResult>>.Success(
            [new SymbolSearchResult("NVDA", "NVIDIA", "NasdaqGS", "EQUITY", 120m, 1.5m)]));
    }
}

internal sealed class InMemoryWatchlist : IWatchlistRepository
{
    public List<WatchlistItem> Items { get; } = [];

    public Task<IReadOnlyList<WatchlistItem>> GetByOwnerAsync(string ownerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WatchlistItem>>(Items.Where(i => i.OwnerId == ownerId).ToList());

    public Task<bool> ExistsAsync(string ownerId, StockSymbol symbol, CancellationToken ct) =>
        Task.FromResult(Items.Any(i => i.OwnerId == ownerId && i.Symbol == symbol));

    public Task<bool> AddAsync(WatchlistItem item, CancellationToken ct)
    {
        if (Items.Any(i => i.OwnerId == item.OwnerId && i.Symbol == item.Symbol))
            return Task.FromResult(false);
        Items.Add(item);
        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(string ownerId, StockSymbol symbol, CancellationToken ct) =>
        Task.FromResult(Items.RemoveAll(i => i.OwnerId == ownerId && i.Symbol == symbol) > 0);
}
