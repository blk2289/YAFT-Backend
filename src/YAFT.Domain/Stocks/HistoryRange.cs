namespace YAFT.Domain.Stocks;

/// <summary>Combinazioni di range/intervallo ammesse per lo storico prezzi.</summary>
public static class HistoryRange
{
    public static readonly IReadOnlySet<string> Ranges =
        new HashSet<string> { "1d", "5d", "1mo", "3mo", "6mo", "1y", "2y", "5y", "10y", "ytd", "max" };

    public static readonly IReadOnlySet<string> Intervals =
        new HashSet<string> { "1m", "5m", "15m", "30m", "60m", "1h", "1d", "1wk", "1mo" };

    public static bool IsValid(string range, string interval) =>
        Ranges.Contains(range) && Intervals.Contains(interval);
}
