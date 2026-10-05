using YAFT.Application.Common;
using YAFT.Application.Stocks.GetHistory;
using YAFT.Application.Stocks.GetQuote;
using YAFT.Application.Stocks.GetQuotes;
using YAFT.Application.Stocks.ListStocks;
using YAFT.Domain.Stocks;

namespace YAFT.Application.Tests;

public class StocksHandlersTests
{
    private readonly FakeMarketData _market = new() { Prices = { ["AAPL"] = 200m, ["MSFT"] = 400m } };

    [Fact]
    public async Task GetQuote_simbolo_non_valido_restituisce_Validation()
    {
        var result = await new GetQuoteHandler(_market).HandleAsync("a b", CancellationToken.None);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(_market.QuoteCalls);
    }

    [Fact]
    public async Task GetQuote_titolo_inesistente_restituisce_NotFound()
    {
        var result = await new GetQuoteHandler(_market).HandleAsync("NOPE", CancellationToken.None);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task GetQuotes_rimuove_duplicati_e_restituisce_risultato_parziale()
    {
        var result = await new GetQuotesHandler(_market).HandleAsync("aapl, AAPL ,NOPE,msft", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["AAPL", "NOPE", "MSFT"], result.Value.Select(r => r.Symbol));
        Assert.Equal(200m, result.Value[0].Quote!.Price);
        Assert.Null(result.Value[1].Quote);
        Assert.Equal("stock.not_found", result.Value[1].Error!.Code);
        Assert.Equal(3, _market.QuoteCalls.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" , ")]
    [InlineData("AAPL,a b")]
    public async Task GetQuotes_input_non_valido_restituisce_Validation(string? symbols)
    {
        var result = await new GetQuotesHandler(_market).HandleAsync(symbols, CancellationToken.None);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task GetQuotes_oltre_il_limite_restituisce_Validation()
    {
        var symbols = string.Join(',', Enumerable.Range(1, GetQuotesHandler.MaxSymbols + 1).Select(i => $"S{i}"));
        var result = await new GetQuotesHandler(_market).HandleAsync(symbols, CancellationToken.None);

        Assert.Equal("stock.too_many_symbols", result.Error!.Code);
        Assert.Empty(_market.QuoteCalls);
    }

    [Fact]
    public async Task GetHistory_valida_range_e_intervallo()
    {
        var handler = new GetHistoryHandler(_market);

        var bad = await handler.HandleAsync(new GetHistoryQuery("AAPL", "7y", "1d"), CancellationToken.None);
        Assert.Equal(ErrorType.Validation, bad.Error!.Type);

        var ok = await handler.HandleAsync(new GetHistoryQuery("AAPL", null, null), CancellationToken.None);
        Assert.Equal(("1mo", "1d"), (ok.Value.Range, ok.Value.Interval));
    }

    [Theory]
    [InlineData(null, StockListType.MostActives)]
    [InlineData("most_actives", StockListType.MostActives)]
    [InlineData("day_gainers", StockListType.DayGainers)]
    [InlineData("DAY-LOSERS", StockListType.DayLosers)]
    [InlineData("trending", StockListType.Trending)]
    public async Task ListStocks_lista_predefinita(string? list, StockListType expected)
    {
        var result = await new ListStocksHandler(_market)
            .HandleAsync(new ListStocksQuery(list, null, "it", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((expected, "IT", ListStocksHandler.DefaultCount), _market.LastList);
        Assert.Equal(1, result.Value.Count);
    }

    [Theory]
    [InlineData("unknown", null, null)]
    [InlineData("1", null, null)]
    [InlineData(null, "ITA", null)]
    [InlineData(null, null, 0)]
    [InlineData(null, null, 101)]
    public async Task ListStocks_parametri_non_validi_restituiscono_Validation(string? list, string? region, int? count)
    {
        var result = await new ListStocksHandler(_market)
            .HandleAsync(new ListStocksQuery(list, null, region, count), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Null(_market.LastList);
    }

    [Fact]
    public async Task ListStocks_con_query_esegue_ricerca()
    {
        var result = await new ListStocksHandler(_market)
            .HandleAsync(new ListStocksQuery("trending", " eni ", null, 100), CancellationToken.None);

        Assert.Equal("search", result.Value.Source);
        Assert.Equal(("eni", ListStocksHandler.MaxSearchCount), _market.LastSearch);
        Assert.Null(_market.LastList);
    }
}
