using YAFT.Application.Common;
using YAFT.Application.Watchlist.AddToWatchlist;
using YAFT.Application.Watchlist.GetWatchlist;
using YAFT.Application.Watchlist.RemoveFromWatchlist;
using YAFT.Domain.Stocks;
using YAFT.Domain.Watchlist;

namespace YAFT.Application.Tests;

public class WatchlistHandlersTests
{
    private const string Uid = "user-1";

    private readonly FakeMarketData _market = new() { Prices = { ["AAPL"] = 200m, ["ENI.MI"] = 15m } };
    private readonly InMemoryWatchlist _repo = new();

    private AddToWatchlistHandler Add(string? uid = Uid) =>
        new(_repo, _market, new FakeCurrentUser(uid), TimeProvider.System);

    [Fact]
    public async Task Utente_non_autenticato_restituisce_Unauthorized()
    {
        var get = await new GetWatchlistHandler(_repo, _market, new FakeCurrentUser(null)).HandleAsync(CancellationToken.None);
        var add = await Add(null).HandleAsync(new AddToWatchlistCommand("AAPL"), CancellationToken.None);
        var remove = await new RemoveFromWatchlistHandler(_repo, new FakeCurrentUser(null)).HandleAsync("AAPL", CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, get.Error!.Type);
        Assert.Equal(ErrorType.Unauthorized, add.Error!.Type);
        Assert.Equal(ErrorType.Unauthorized, remove.Error!.Type);
    }

    [Fact]
    public async Task Add_salva_il_titolo_normalizzato_con_quotazione()
    {
        var result = await Add().HandleAsync(new AddToWatchlistCommand(" eni.mi "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ENI.MI", result.Value.Symbol);
        Assert.Equal(15m, result.Value.Quote!.Price);
        Assert.Single(_repo.Items, i => i.OwnerId == Uid && i.Symbol.Value == "ENI.MI");
    }

    [Fact]
    public async Task Add_duplicato_restituisce_Conflict()
    {
        await Add().HandleAsync(new AddToWatchlistCommand("AAPL"), CancellationToken.None);
        var result = await Add().HandleAsync(new AddToWatchlistCommand("aapl"), CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Single(_repo.Items);
    }

    [Fact]
    public async Task Add_titolo_inesistente_restituisce_NotFound_e_non_salva()
    {
        var result = await Add().HandleAsync(new AddToWatchlistCommand("NOPE"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task Add_provider_non_disponibile_restituisce_Unavailable()
    {
        _market.Unavailable = true;
        var result = await Add().HandleAsync(new AddToWatchlistCommand("AAPL"), CancellationToken.None);

        Assert.Equal(ErrorType.Unavailable, result.Error!.Type);
        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task Get_restituisce_solo_i_preferiti_dell_utente_e_quote_null_se_non_disponibile()
    {
        _repo.Items.Add(WatchlistItem.Rehydrate(Uid, StockSymbol.Create("AAPL"), DateTimeOffset.UtcNow));
        _repo.Items.Add(WatchlistItem.Rehydrate(Uid, StockSymbol.Create("DELISTED"), DateTimeOffset.UtcNow));
        _repo.Items.Add(WatchlistItem.Rehydrate("other", StockSymbol.Create("ENI.MI"), DateTimeOffset.UtcNow));

        var result = await new GetWatchlistHandler(_repo, _market, new FakeCurrentUser(Uid)).HandleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["AAPL", "DELISTED"], result.Value.Select(i => i.Symbol));
        Assert.Equal(200m, result.Value[0].Quote!.Price);
        Assert.Null(result.Value[1].Quote);
    }

    [Fact]
    public async Task Remove_titolo_assente_restituisce_NotFound()
    {
        var handler = new RemoveFromWatchlistHandler(_repo, new FakeCurrentUser(Uid));
        _repo.Items.Add(WatchlistItem.Rehydrate(Uid, StockSymbol.Create("AAPL"), DateTimeOffset.UtcNow));

        Assert.True((await handler.HandleAsync("aapl", CancellationToken.None)).IsSuccess);
        Assert.Equal(ErrorType.NotFound, (await handler.HandleAsync("AAPL", CancellationToken.None)).Error!.Type);
    }
}
