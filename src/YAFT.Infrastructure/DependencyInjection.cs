using System.Net;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YAFT.Application.Abstractions;
using YAFT.Infrastructure.Caching;
using YAFT.Infrastructure.Firebase;
using YAFT.Infrastructure.Yahoo;

namespace YAFT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var firebase = config.GetSection(FirebaseOptions.Section).Get<FirebaseOptions>()
                       ?? throw new InvalidOperationException("Sezione 'Firebase' mancante.");
        if (string.IsNullOrWhiteSpace(firebase.ProjectId))
            throw new InvalidOperationException("Firebase:ProjectId mancante.");

        services.AddFirestore(firebase);
        services.AddMarketData(config);
        return services;
    }

    private static void AddFirestore(this IServiceCollection services, FirebaseOptions options)
    {
        // Creato alla prima richiesta: le API di borsa funzionano anche senza credenziali Firestore.
        services.AddSingleton(_ => new FirestoreDbBuilder
        {
            ProjectId = options.ProjectId,
            // Senza file si usano le Application Default Credentials.
            GoogleCredential = string.IsNullOrWhiteSpace(options.CredentialPath)
                ? null
                : CredentialFactory.FromFile<ServiceAccountCredential>(options.CredentialPath).ToGoogleCredential()
        }.Build());

        services.AddScoped<IWatchlistRepository, FirestoreWatchlistRepository>();
    }

    private static void AddMarketData(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<MarketDataCacheOptions>(config.GetSection(MarketDataCacheOptions.Section));

        services.AddHybridCache().AddSerializerFactory<DomainJsonSerializerFactory>();

        services.AddSingleton<YahooCrumbStore>();
        services.AddTransient<YahooCrumbHandler>();

        services.AddHttpClient<YahooFinanceProvider>(client =>
            {
                client.BaseAddress = new Uri(YahooDefaults.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(YahooDefaults.UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                UseCookies = true,
                CookieContainer = sp.GetRequiredService<YahooCrumbStore>().Cookies,
                AutomaticDecompression = DecompressionMethods.All
            })
            .AddHttpMessageHandler<YahooCrumbHandler>();

        // Decorator: la porta IMarketDataProvider è la versione con cache.
        services.AddScoped<IMarketDataProvider>(sp => new CachedMarketDataProvider(
            sp.GetRequiredService<YahooFinanceProvider>(),
            sp.GetRequiredService<HybridCache>(),
            sp.GetRequiredService<IOptions<MarketDataCacheOptions>>()));
    }
}
