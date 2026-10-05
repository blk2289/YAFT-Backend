using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Application.Stocks;
using YAFT.Domain.Stocks;

namespace YAFT.Infrastructure.Yahoo;

internal sealed class YahooFinanceProvider(
    HttpClient http,
    ILogger<YahooFinanceProvider> logger) : IMarketDataProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public Task<Result<StockQuote>> GetQuoteAsync(StockSymbol symbol, CancellationToken ct) =>
        ExecuteAsync<StockQuote>($"quote {symbol}", ct, async () =>
        {
            var chart = await GetChartAsync(symbol, "1d", "1d", ct);
            if (chart is null)
                return StockErrors.NotFound(symbol.Value);

            var meta = chart.Meta;
            var previousClose = meta.PreviousClose ?? meta.ChartPreviousClose;
            if (meta.RegularMarketPrice is null || previousClose is null)
                return StockErrors.NotFound(symbol.Value);

            return new StockQuote(
                symbol,
                meta.LongName ?? meta.ShortName,
                meta.Currency,
                meta.FullExchangeName ?? meta.ExchangeName,
                meta.RegularMarketPrice.Value,
                previousClose.Value,
                meta.RegularMarketDayHigh,
                meta.RegularMarketDayLow,
                meta.RegularMarketVolume,
                meta.RegularMarketTime is { } t ? DateTimeOffset.FromUnixTimeSeconds(t) : null);
        });

    public Task<Result<PriceHistory>> GetHistoryAsync(
        StockSymbol symbol, string range, string interval, CancellationToken ct) =>
        ExecuteAsync<PriceHistory>($"history {symbol}", ct, async () =>
        {
            var chart = await GetChartAsync(symbol, range, interval, ct);
            if (chart is null)
                return StockErrors.NotFound(symbol.Value);

            var timestamps = chart.Timestamp ?? [];
            var ohlcv = chart.Indicators?.Quote?.FirstOrDefault();

            var points = new List<PricePoint>(timestamps.Count);
            for (var i = 0; i < timestamps.Count; i++)
            {
                points.Add(new PricePoint(
                    DateTimeOffset.FromUnixTimeSeconds(timestamps[i]),
                    Price(ohlcv?.Open, i), Price(ohlcv?.High, i), Price(ohlcv?.Low, i), Price(ohlcv?.Close, i),
                    At(ohlcv?.Volume, i)));
            }

            return new PriceHistory(symbol, chart.Meta.Currency, range, interval, points);
        });

    public Task<Result<IReadOnlyList<SymbolSearchResult>>> SearchAsync(
        string query, int count, CancellationToken ct) =>
        ExecuteAsync($"search '{query}'", ct, async () =>
        {
            var url = $"v1/finance/search?q={Uri.EscapeDataString(query)}" +
                      $"&quotesCount={count}&newsCount=0&listsCount=0&enableFuzzyQuery=false";
            var data = await http.GetFromJsonAsync<YahooSearchResponse>(url, JsonOpts, ct);

            IReadOnlyList<SymbolSearchResult> items = (data?.Quotes ?? [])
                .Where(q => !string.IsNullOrEmpty(q.Symbol))
                .Select(q => new SymbolSearchResult(
                    q.Symbol!, q.Longname ?? q.Shortname, q.ExchDisp ?? q.Exchange, q.QuoteType))
                .ToList();

            return Result<IReadOnlyList<SymbolSearchResult>>.Success(items);
        });

    public Task<Result<IReadOnlyList<SymbolSearchResult>>> GetListAsync(
        StockListType listType, string region, int count, CancellationToken ct) =>
        ExecuteAsync($"list {listType} {region}", ct, async () =>
        {
            var url = listType switch
            {
                StockListType.Trending => $"v1/finance/trending/{region}?count={count}",
                _ => $"v1/finance/screener/predefined/saved?scrIds={ScreenerId(listType)}" +
                     $"&count={count}&region={region}&formatted=false&lang=en-US"
            };

            var data = await http.GetFromJsonAsync<YahooFinanceResponse>(url, JsonOpts, ct);

            IReadOnlyList<SymbolSearchResult> items = (data?.Finance?.Result?.FirstOrDefault()?.Quotes ?? [])
                .Where(q => !string.IsNullOrEmpty(q.Symbol))
                .Take(count)
                .Select(q => new SymbolSearchResult(
                    q.Symbol!, q.LongName ?? q.ShortName, q.FullExchangeName ?? q.Exchange, q.QuoteType,
                    q.RegularMarketPrice, q.RegularMarketChangePercent is { } p ? Math.Round(p, 2) : null))
                .ToList();

            return Result<IReadOnlyList<SymbolSearchResult>>.Success(items);
        });

    /// Restituisce null se Yahoo non conosce il simbolo.
    private async Task<YahooChartResult?> GetChartAsync(
        StockSymbol symbol, string range, string interval, CancellationToken ct)
    {
        var url = $"v8/finance/chart/{Uri.EscapeDataString(symbol.Value)}?range={range}&interval={interval}";
        using var response = await http.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<YahooChartResponse>(JsonOpts, ct);
        return data?.Chart?.Result?.FirstOrDefault();
    }

    private async Task<Result<T>> ExecuteAsync<T>(
        string operation, CancellationToken ct, Func<Task<Result<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (ct.IsCancellationRequested) throw;   // annullata dal client: non è un errore del provider

            logger.LogWarning(ex, "Yahoo Finance non disponibile ({Operation})", operation);
            return StockErrors.Unavailable;
        }
    }

    private static string ScreenerId(StockListType type) => type switch
    {
        StockListType.MostActives => "most_actives",
        StockListType.DayGainers => "day_gainers",
        StockListType.DayLosers => "day_losers",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private static T? At<T>(List<T?>? values, int index) where T : struct =>
        values is not null && index < values.Count ? values[index] : null;

    // Yahoo restituisce float (es. 336.9700012207031): si arrotonda per eliminare il rumore.
    private static decimal? Price(List<decimal?>? values, int index) =>
        At(values, index) is { } v ? Math.Round(v, 4) : null;
}
