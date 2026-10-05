namespace YAFT.Domain.Stocks;

public sealed record StockQuote(
    StockSymbol Symbol,
    string? Name,
    string? Currency,
    string? Exchange,
    decimal Price,
    decimal PreviousClose,
    decimal? DayHigh,
    decimal? DayLow,
    long? Volume,
    DateTimeOffset? MarketTime)
{
    public decimal Change => Price - PreviousClose;

    public decimal ChangePercent =>
        PreviousClose == 0 ? 0 : Math.Round(Change / PreviousClose * 100, 2);
}
