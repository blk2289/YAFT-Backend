using YAFT.Domain.Stocks;
using YAFT.Domain.Watchlist;

namespace YAFT.Application.Abstractions;

public interface IWatchlistRepository
{
    Task<IReadOnlyList<WatchlistItem>> GetByOwnerAsync(string ownerId, CancellationToken ct);
    Task<bool> ExistsAsync(string ownerId, StockSymbol symbol, CancellationToken ct);

    /// Aggiunge il preferito. Restituisce false se era già presente.
    Task<bool> AddAsync(WatchlistItem item, CancellationToken ct);

    /// Rimuove il preferito. Restituisce false se non era presente.
    Task<bool> RemoveAsync(string ownerId, StockSymbol symbol, CancellationToken ct);
}
