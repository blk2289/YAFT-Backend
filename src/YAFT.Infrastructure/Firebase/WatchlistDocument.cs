using Google.Cloud.Firestore;
using YAFT.Domain.Stocks;
using YAFT.Domain.Watchlist;

namespace YAFT.Infrastructure.Firebase;

/// <summary>Documento Firestore in users/{uid}/watchlist/{symbol}.</summary>
[FirestoreData]
internal sealed class WatchlistDocument
{
    [FirestoreProperty("symbol")] public string Symbol { get; set; } = "";
    [FirestoreProperty("addedAt")] public Timestamp AddedAt { get; set; }

    public static WatchlistDocument FromDomain(WatchlistItem item) => new()
    {
        Symbol = item.Symbol.Value,
        AddedAt = Timestamp.FromDateTimeOffset(item.AddedAt)
    };

    public WatchlistItem ToDomain(string ownerId, string documentId) =>
        WatchlistItem.Rehydrate(
            ownerId,
            StockSymbol.Create(string.IsNullOrEmpty(Symbol) ? documentId : Symbol),
            AddedAt.ToDateTimeOffset());
}
