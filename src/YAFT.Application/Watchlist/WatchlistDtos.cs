using YAFT.Application.Common;
using YAFT.Application.Stocks;

namespace YAFT.Application.Watchlist;

/// Titolo preferito con la quotazione corrente (null se Yahoo non l'ha restituita).
public sealed record WatchlistItemDto(string Symbol, DateTimeOffset AddedAt, StockQuoteDto? Quote);

public static class WatchlistErrors
{
    public static Error AlreadyPresent(string symbol) =>
        new("watchlist.duplicate", $"Il titolo '{symbol}' è già nei preferiti.", ErrorType.Conflict);

    public static Error NotPresent(string symbol) =>
        new("watchlist.not_found", $"Il titolo '{symbol}' non è nei preferiti.", ErrorType.NotFound);
}
