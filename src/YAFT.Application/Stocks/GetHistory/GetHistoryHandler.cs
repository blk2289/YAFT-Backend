using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Domain.Stocks;

namespace YAFT.Application.Stocks.GetHistory;

public sealed record GetHistoryQuery(string Symbol, string? Range, string? Interval);

public sealed class GetHistoryHandler(IMarketDataProvider provider)
{
    public async Task<Result<PriceHistoryDto>> HandleAsync(GetHistoryQuery query, CancellationToken ct)
    {
        if (!StockSymbol.TryCreate(query.Symbol, out var symbol))
            return StockErrors.InvalidSymbol(query.Symbol);

        var range = string.IsNullOrWhiteSpace(query.Range) ? "1mo" : query.Range.Trim().ToLowerInvariant();
        var interval = string.IsNullOrWhiteSpace(query.Interval) ? "1d" : query.Interval.Trim().ToLowerInvariant();

        if (!HistoryRange.IsValid(range, interval))
            return new Error("stock.history_params_invalid",
                $"Range ammessi: {string.Join(", ", HistoryRange.Ranges)}. " +
                $"Intervalli ammessi: {string.Join(", ", HistoryRange.Intervals)}.",
                ErrorType.Validation);

        var result = await provider.GetHistoryAsync(symbol!, range, interval, ct);
        return result.IsSuccess ? PriceHistoryDto.From(result.Value) : result.Error!;
    }
}
