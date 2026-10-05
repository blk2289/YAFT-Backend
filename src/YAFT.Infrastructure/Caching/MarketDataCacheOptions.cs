namespace YAFT.Infrastructure.Caching;

/// <summary>Durata in cache delle interrogazioni a Yahoo (sezione "MarketDataCache" di appsettings).</summary>
public sealed class MarketDataCacheOptions
{
    public const string Section = "MarketDataCache";

    public TimeSpan QuoteTtl { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan HistoryTtl { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan SearchTtl { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan ListTtl { get; set; } = TimeSpan.FromMinutes(2);
}
