using YAFT.Domain.Common;
using YAFT.Domain.Stocks;

namespace YAFT.Domain.Watchlist;

/// <summary>Titolo preferito di un utente.</summary>
public sealed class WatchlistItem
{
    public string OwnerId { get; }
    public StockSymbol Symbol { get; }
    public DateTimeOffset AddedAt { get; }

    private WatchlistItem(string ownerId, StockSymbol symbol, DateTimeOffset addedAt)
    {
        OwnerId = ownerId;
        Symbol = symbol;
        AddedAt = addedAt;
    }

    public static WatchlistItem Create(string ownerId, StockSymbol symbol, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new DomainException("Il preferito deve avere un proprietario.");

        return new WatchlistItem(ownerId, symbol, now);
    }

    /// Ricostruisce un preferito già salvato (usato dai repository).
    public static WatchlistItem Rehydrate(string ownerId, StockSymbol symbol, DateTimeOffset addedAt)
        => new(ownerId, symbol, addedAt);
}
