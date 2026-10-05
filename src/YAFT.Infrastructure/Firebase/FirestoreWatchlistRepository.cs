using Google.Cloud.Firestore;
using Grpc.Core;
using YAFT.Application.Abstractions;
using YAFT.Domain.Stocks;
using YAFT.Domain.Watchlist;

namespace YAFT.Infrastructure.Firebase;

internal sealed class FirestoreWatchlistRepository(FirestoreDb db) : IWatchlistRepository
{
    private CollectionReference Watchlist(string ownerId) =>
        db.Collection("users").Document(ownerId).Collection("watchlist");

    public async Task<IReadOnlyList<WatchlistItem>> GetByOwnerAsync(string ownerId, CancellationToken ct)
    {
        var snapshot = await Watchlist(ownerId).OrderBy("addedAt").GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(d => d.ConvertTo<WatchlistDocument>().ToDomain(ownerId, d.Id))
            .ToList();
    }

    public async Task<bool> ExistsAsync(string ownerId, StockSymbol symbol, CancellationToken ct)
    {
        var snapshot = await Watchlist(ownerId).Document(symbol.Value).GetSnapshotAsync(ct);
        return snapshot.Exists;
    }

    public async Task<bool> AddAsync(WatchlistItem item, CancellationToken ct)
    {
        try
        {
            // CreateAsync fallisce se il documento esiste già: nessuna sovrascrittura in caso di richieste concorrenti.
            await Watchlist(item.OwnerId).Document(item.Symbol.Value)
                .CreateAsync(WatchlistDocument.FromDomain(item), ct);
            return true;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            return false;
        }
    }

    public async Task<bool> RemoveAsync(string ownerId, StockSymbol symbol, CancellationToken ct)
    {
        try
        {
            await Watchlist(ownerId).Document(symbol.Value).DeleteAsync(Precondition.MustExist, ct);
            return true;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }
}
