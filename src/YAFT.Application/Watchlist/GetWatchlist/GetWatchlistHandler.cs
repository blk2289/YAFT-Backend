using YAFT.Application.Abstractions;
using YAFT.Application.Common;
using YAFT.Application.Stocks;

namespace YAFT.Application.Watchlist.GetWatchlist;

/// <summary>Preferiti dell'utente autenticato, ciascuno con la quotazione corrente.</summary>
public sealed class GetWatchlistHandler(
    IWatchlistRepository repository,
    IMarketDataProvider provider,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<WatchlistItemDto>>> HandleAsync(CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return CommonErrors.Unauthenticated;

        var items = await repository.GetByOwnerAsync(currentUser.UserId, ct);

        // Una quotazione non disponibile non deve far fallire l'intera lista.
        var dtos = await Task.WhenAll(items.Select(async item =>
        {
            var quote = await provider.GetQuoteAsync(item.Symbol, ct);
            return new WatchlistItemDto(
                item.Symbol.Value,
                item.AddedAt,
                quote.IsSuccess ? StockQuoteDto.From(quote.Value) : null);
        }));

        return dtos;
    }
}
