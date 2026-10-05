namespace YAFT.Infrastructure.Yahoo;

// DTO del JSON di Yahoo Finance: restano interni all'Infrastructure.

// v8/finance/chart/{symbol}
internal sealed class YahooChartResponse { public YahooChart? Chart { get; set; } }

internal sealed class YahooChart
{
    public List<YahooChartResult>? Result { get; set; }
    public YahooError? Error { get; set; }
}

internal sealed class YahooError
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

internal sealed class YahooChartResult
{
    public YahooMeta Meta { get; set; } = new();
    public List<long>? Timestamp { get; set; }
    public YahooIndicators? Indicators { get; set; }
}

internal sealed class YahooMeta
{
    public string? Symbol { get; set; }
    public string? LongName { get; set; }
    public string? ShortName { get; set; }
    public string? Currency { get; set; }
    public string? FullExchangeName { get; set; }
    public string? ExchangeName { get; set; }
    public decimal? RegularMarketPrice { get; set; }
    public decimal? PreviousClose { get; set; }
    public decimal? ChartPreviousClose { get; set; }
    public decimal? RegularMarketDayHigh { get; set; }
    public decimal? RegularMarketDayLow { get; set; }
    public long? RegularMarketVolume { get; set; }
    public long? RegularMarketTime { get; set; }
}

internal sealed class YahooIndicators { public List<YahooOhlcv>? Quote { get; set; } }

internal sealed class YahooOhlcv
{
    public List<decimal?>? Open { get; set; }
    public List<decimal?>? High { get; set; }
    public List<decimal?>? Low { get; set; }
    public List<decimal?>? Close { get; set; }
    public List<long?>? Volume { get; set; }
}

// v1/finance/search
internal sealed class YahooSearchResponse { public List<YahooSearchQuote>? Quotes { get; set; } }

internal sealed class YahooSearchQuote
{
    public string? Symbol { get; set; }
    public string? Shortname { get; set; }
    public string? Longname { get; set; }
    public string? ExchDisp { get; set; }
    public string? Exchange { get; set; }
    public string? QuoteType { get; set; }
}

// v1/finance/screener/predefined/saved e v1/finance/trending/{region}
internal sealed class YahooFinanceResponse { public YahooFinance? Finance { get; set; } }

internal sealed class YahooFinance
{
    public List<YahooFinanceResult>? Result { get; set; }
    public YahooError? Error { get; set; }
}

internal sealed class YahooFinanceResult { public List<YahooListQuote>? Quotes { get; set; } }

internal sealed class YahooListQuote
{
    public string? Symbol { get; set; }
    public string? ShortName { get; set; }
    public string? LongName { get; set; }
    public string? FullExchangeName { get; set; }
    public string? Exchange { get; set; }
    public string? QuoteType { get; set; }
    public decimal? RegularMarketPrice { get; set; }
    public decimal? RegularMarketChangePercent { get; set; }
}
