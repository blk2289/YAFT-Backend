using Microsoft.Extensions.DependencyInjection;
using YAFT.Application.Stocks.GetHistory;
using YAFT.Application.Stocks.GetQuote;
using YAFT.Application.Stocks.GetQuotes;
using YAFT.Application.Stocks.ListStocks;
using YAFT.Application.Watchlist.AddToWatchlist;
using YAFT.Application.Watchlist.GetWatchlist;
using YAFT.Application.Watchlist.RemoveFromWatchlist;

namespace YAFT.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<GetQuoteHandler>();
        services.AddScoped<GetQuotesHandler>();
        services.AddScoped<GetHistoryHandler>();
        services.AddScoped<ListStocksHandler>();

        services.AddScoped<GetWatchlistHandler>();
        services.AddScoped<AddToWatchlistHandler>();
        services.AddScoped<RemoveFromWatchlistHandler>();

        return services;
    }
}
