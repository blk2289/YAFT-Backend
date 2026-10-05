namespace YAFT.Domain.Stocks;

public sealed record PricePoint(
    DateTimeOffset Timestamp,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? Close,
    long? Volume);
