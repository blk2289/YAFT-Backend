using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Application.Stocks;
using YAFT.Domain.Stocks;
using YAFT.Domain.Watchlist;

namespace YAFT.Application.Watchlist.AddToWatchlist;

public sealed record AddToWatchlistCommand(string Symbol);

public sealed class AddToWatchlistHandler(
    IWatchlistRepository repository,
    IMarketDataProvider provider,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<Result<WatchlistItemDto>> HandleAsync(AddToWatchlistCommand command, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return CommonErrors.Unauthenticated;

        if (!StockSymbol.TryCreate(command.Symbol, out var symbol))
            return StockErrors.InvalidSymbol(command.Symbol);

        if (await repository.ExistsAsync(currentUser.UserId, symbol!, ct))
            return WatchlistErrors.AlreadyPresent(symbol!.Value);

        // Si salvano solo titoli che esistono davvero su Yahoo.
        var quote = await provider.GetQuoteAsync(symbol!, ct);
        if (!quote.IsSuccess)
            return quote.Error!;

        var item = WatchlistItem.Create(currentUser.UserId, symbol!, clock.GetUtcNow());
        if (!await repository.AddAsync(item, ct))
            return WatchlistErrors.AlreadyPresent(symbol!.Value);

        return new WatchlistItemDto(item.Symbol.Value, item.AddedAt, StockQuoteDto.From(quote.Value));
    }
}
