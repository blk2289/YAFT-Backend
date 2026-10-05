namespace YAFT.Domain.Stocks;

public sealed record PriceHistory(
    StockSymbol Symbol,
    string? Currency,
    string Range,
    string Interval,
    IReadOnlyList<PricePoint> Points);
