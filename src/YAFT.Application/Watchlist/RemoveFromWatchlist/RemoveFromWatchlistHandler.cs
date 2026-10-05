using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Application.Stocks;
using YAFT.Domain.Stocks;

namespace YAFT.Application.Watchlist.RemoveFromWatchlist;

public sealed class RemoveFromWatchlistHandler(IWatchlistRepository repository, ICurrentUser currentUser)
{
    public async Task<Result> HandleAsync(string rawSymbol, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return CommonErrors.Unauthenticated;

        if (!StockSymbol.TryCreate(rawSymbol, out var symbol))
            return StockErrors.InvalidSymbol(rawSymbol);

        return await repository.RemoveAsync(currentUser.UserId, symbol!, ct)
            ? Result.Success()
            : WatchlistErrors.NotPresent(symbol!.Value);
    }
}
