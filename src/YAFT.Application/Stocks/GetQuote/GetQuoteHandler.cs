using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Domain.Stocks;

namespace YAFT.Application.Stocks.GetQuote;

public sealed class GetQuoteHandler(IMarketDataProvider provider)
{
    public async Task<Result<StockQuoteDto>> HandleAsync(string rawSymbol, CancellationToken ct)
    {
        if (!StockSymbol.TryCreate(rawSymbol, out var symbol))
            return StockErrors.InvalidSymbol(rawSymbol);

        var result = await provider.GetQuoteAsync(symbol!, ct);
        return result.IsSuccess ? StockQuoteDto.From(result.Value) : result.Error!;
    }
}
