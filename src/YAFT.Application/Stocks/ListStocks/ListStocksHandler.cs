using System.Text.RegularExpressions;
using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Domain.Stocks;

namespace YAFT.Application.Stocks.ListStocks;

/// <param name="List">Lista predefinita: most_actives, day_gainers, day_losers, trending.</param>
/// <param name="Query">Testo di ricerca: se presente ha precedenza sulla lista.</param>
/// <param name="Region">Codice paese a due lettere (default US).</param>
/// <param name="Count">Numero di risultati (1-100, default 25).</param>
public sealed record ListStocksQuery(string? List, string? Query, string? Region, int? Count);

/// <summary>Elenco dei titoli disponibili: lista predefinita oppure ricerca testuale.</summary>
public sealed partial class ListStocksHandler(IMarketDataProvider provider)
{
    public const int DefaultCount = 25;
    public const int MaxCount = 100;
    public const int MaxSearchCount = 25;

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex RegionRegex();

    public async Task<Result<StockListDto>> HandleAsync(ListStocksQuery query, CancellationToken ct)
    {
        var count = query.Count ?? DefaultCount;
        if (count is < 1 or > MaxCount)
            return new Error("stock.count_invalid", $"count deve essere tra 1 e {MaxCount}.", ErrorType.Validation);

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var text = query.Query.Trim();
            if (text.Length > 50)
                return new Error("stock.query_invalid", "La ricerca non può superare 50 caratteri.", ErrorType.Validation);

            return ToDto("search", await provider.SearchAsync(text, Math.Min(count, MaxSearchCount), ct));
        }

        if (!TryParseList(query.List, out var listType))
            return new Error("stock.list_invalid",
                "Lista non valida. Valori ammessi: most_actives, day_gainers, day_losers, trending.",
                ErrorType.Validation);

        var region = string.IsNullOrWhiteSpace(query.Region) ? "US" : query.Region.Trim().ToUpperInvariant();
        if (!RegionRegex().IsMatch(region))
            return new Error("stock.region_invalid", "La regione deve essere un codice di due lettere (es. US, IT).",
                ErrorType.Validation);

        return ToDto(ToListName(listType), await provider.GetListAsync(listType, region, count, ct));
    }

    private static Result<StockListDto> ToDto(string source, Result<IReadOnlyList<SymbolSearchResult>> result)
    {
        if (!result.IsSuccess)
            return result.Error!;

        var items = result.Value.Select(StockListItemDto.From).ToList();
        return new StockListDto(source, items.Count, items);
    }

    private static bool TryParseList(string? raw, out StockListType listType)
    {
        listType = StockListType.MostActives;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var normalized = raw.Replace("_", "").Replace("-", "");
        return !int.TryParse(normalized, out _)
               && Enum.TryParse(normalized, ignoreCase: true, out listType);
    }

    private static string ToListName(StockListType type) => type switch
    {
        StockListType.MostActives => "most_actives",
        StockListType.DayGainers => "day_gainers",
        StockListType.DayLosers => "day_losers",
        StockListType.Trending => "trending",
        _ => type.ToString()
    };
}
