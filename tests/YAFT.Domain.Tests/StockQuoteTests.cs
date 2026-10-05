using YAFT.Domain.Common;
using YAFT.Domain.Stocks;
using YAFT.Domain.Watchlist;

namespace YAFT.Domain.Tests;

public class StockQuoteTests
{
    private static StockQuote Quote(decimal price, decimal previousClose) =>
        new(StockSymbol.Create("AAPL"), null, "USD", null, price, previousClose, null, null, null, null);

    [Fact]
    public void Calcola_variazione_e_percentuale()
    {
        var q = Quote(110m, 100m);
        Assert.Equal(10m, q.Change);
        Assert.Equal(10m, q.ChangePercent);
    }

    [Fact]
    public void Percentuale_zero_se_chiusura_precedente_zero() =>
        Assert.Equal(0m, Quote(10m, 0m).ChangePercent);

    [Fact]
    public void Percentuale_arrotondata_a_due_decimali() =>
        Assert.Equal(33.33m, Quote(4m, 3m).ChangePercent);
}

public class WatchlistItemTests
{
    [Fact]
    public void Create_richiede_un_proprietario() =>
        Assert.Throws<DomainException>(() =>
            WatchlistItem.Create(" ", StockSymbol.Create("AAPL"), DateTimeOffset.UtcNow));
}
