using System.Net;
using Microsoft.Extensions.Logging;

namespace YAFT.Infrastructure.Yahoo;

/// <summary>
/// Aggiunge il parametro "crumb" alle chiamate dello screener Yahoo, ottenendolo (cookie da fc.yahoo.com
/// + v1/test/getcrumb) solo quando serve. Su 401/403 rinnova il crumb e ritenta una volta.
/// </summary>
internal sealed class YahooCrumbHandler(
    YahooCrumbStore store,
    ILogger<YahooCrumbHandler> logger) : DelegatingHandler
{
    private const string CookieUrl = "https://fc.yahoo.com/";
    private const string CrumbUrl = "https://query1.finance.yahoo.com/v1/test/getcrumb";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!RequiresCrumb(request.RequestUri))
            return await base.SendAsync(request, ct);

        var originalUri = request.RequestUri!;
        var crumb = await store.GetAsync(FetchCrumbAsync, ct);
        request.RequestUri = WithCrumb(originalUri, crumb);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
            return response;

        response.Dispose();
        store.Invalidate(crumb);
        crumb = await store.GetAsync(FetchCrumbAsync, ct);

        using var retry = new HttpRequestMessage(request.Method, WithCrumb(originalUri, crumb));
        foreach (var header in request.Headers)
            retry.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return await base.SendAsync(retry, ct);
    }

    private async Task<string?> FetchCrumbAsync(CancellationToken ct)
    {
        try
        {
            // Il 404 di fc.yahoo.com è atteso: serve solo a ricevere il cookie di sessione.
            using (var cookieResponse = await base.SendAsync(NewRequest(CookieUrl), ct)) { }

            using var response = await base.SendAsync(NewRequest(CrumbUrl), ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var crumb = (await response.Content.ReadAsStringAsync(ct)).Trim();
            return crumb.Length is > 0 and < 64 && !crumb.Contains('<') ? crumb : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Impossibile ottenere il crumb Yahoo: la richiesta prosegue senza");
            return null;
        }
    }

    private static HttpRequestMessage NewRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(YahooDefaults.UserAgent);
        return request;
    }

    private static bool RequiresCrumb(Uri? uri) =>
        uri is not null && uri.AbsolutePath.Contains("/finance/screener/", StringComparison.OrdinalIgnoreCase);

    private static Uri WithCrumb(Uri uri, string? crumb)
    {
        if (string.IsNullOrEmpty(crumb))
            return uri;

        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri(uri + separator + "crumb=" + Uri.EscapeDataString(crumb));
    }
}

internal static class YahooDefaults
{
    public const string BaseUrl = "https://query1.finance.yahoo.com/";

    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36";
}
