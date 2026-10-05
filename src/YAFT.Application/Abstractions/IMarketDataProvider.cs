using YAFT.Application.Common;
using YAFT.Domain.Stocks;

namespace YAFT.Application.Abstractions;

/// <summary>Porta verso la sorgente dati di mercato (Yahoo Finance).</summary>
public interface IMarketDataProvider
{
    Task<Result<StockQuote>> GetQuoteAsync(StockSymbol symbol, CancellationToken ct);

    Task<Result<PriceHistory>> GetHistoryAsync(
        StockSymbol symbol, string range, string interval, CancellationToken ct);

    Task<Result<IReadOnlyList<SymbolSearchResult>>> SearchAsync(
        string query, int count, CancellationToken ct);

    Task<Result<IReadOnlyList<SymbolSearchResult>>> GetListAsync(
        StockListType listType, string region, int count, CancellationToken ct);
}
