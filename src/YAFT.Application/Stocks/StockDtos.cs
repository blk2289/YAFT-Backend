using YAFT.Domain.Stocks;

namespace YAFT.Application.Stocks;

public sealed record StockQuoteDto(
    string Symbol, string? Name, string? Currency, string? Exchange,
    decimal Price, decimal PreviousClose, decimal Change, decimal ChangePercent,
    decimal? DayHigh, decimal? DayLow, long? Volume, DateTimeOffset? MarketTime)
{
    public static StockQuoteDto From(StockQuote q) => new(
        q.Symbol.Value, q.Name, q.Currency, q.Exchange,
        q.Price, q.PreviousClose, q.Change, q.ChangePercent,
        q.DayHigh, q.DayLow, q.Volume, q.MarketTime);
}

public sealed record PricePointDto(
    DateTimeOffset Timestamp, decimal? Open, decimal? High, decimal? Low, decimal? Close, long? Volume);

public sealed record PriceHistoryDto(
    string Symbol, string? Currency, string Range, string Interval, IReadOnlyList<PricePointDto> Points)
{
    public static PriceHistoryDto From(PriceHistory h) => new(
        h.Symbol.Value, h.Currency, h.Range, h.Interval,
        h.Points.Select(p => new PricePointDto(p.Timestamp, p.Open, p.High, p.Low, p.Close, p.Volume)).ToList());
}

public sealed record StockListItemDto(
    string Symbol, string? Name, string? Exchange, string? Type, decimal? Price, decimal? ChangePercent)
{
    public static StockListItemDto From(SymbolSearchResult r) =>
        new(r.Symbol, r.Name, r.Exchange, r.Type, r.Price, r.ChangePercent);
}

public sealed record StockListDto(string Source, int Count, IReadOnlyList<StockListItemDto> Items);

public sealed record QuoteErrorDto(string Code, string Description);

/// Esito della quotazione di un singolo titolo all'interno di una richiesta batch.
public sealed record QuoteResultDto(string Symbol, StockQuoteDto? Quote, QuoteErrorDto? Error);
