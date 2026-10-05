using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Domain.Stocks;

namespace YAFT.Application.Stocks.GetQuotes;

/// <summary>Quotazioni di un insieme di titoli specifici (es. "AAPL,ENI.MI,MSFT").</summary>
public sealed class GetQuotesHandler(IMarketDataProvider provider)
{
    public const int MaxSymbols = 20;

    public async Task<Result<IReadOnlyList<QuoteResultDto>>> HandleAsync(string? rawSymbols, CancellationToken ct)
    {
        var parts = (rawSymbols ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return new Error("stock.symbols_required", "Specificare almeno un simbolo.", ErrorType.Validation);

        var symbols = new List<StockSymbol>();
        foreach (var part in parts)
        {
            if (!StockSymbol.TryCreate(part, out var symbol))
                return StockErrors.InvalidSymbol(part);
            if (!symbols.Contains(symbol!))
                symbols.Add(symbol!);
        }

        if (symbols.Count > MaxSymbols)
            return new Error("stock.too_many_symbols",
                $"Massimo {MaxSymbols} simboli per richiesta.", ErrorType.Validation);

        var results = await Task.WhenAll(symbols.Select(async s =>
        {
            var r = await provider.GetQuoteAsync(s, ct);
            return r.IsSuccess
                ? new QuoteResultDto(s.Value, StockQuoteDto.From(r.Value), null)
                : new QuoteResultDto(s.Value, null, new QuoteErrorDto(r.Error!.Code, r.Error.Description));
        }));

        return results;
    }
}
