using System.Net;

namespace YAFT.Infrastructure.Yahoo;

/// <summary>
/// Sessione Yahoo condivisa (singleton): cookie e "crumb" richiesti dagli endpoint dello screener.
/// Il CookieContainer è condiviso dai primary handler, così sopravvive alla rotazione di IHttpClientFactory.
/// </summary>
internal sealed class YahooCrumbStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _crumb;
    private DateTimeOffset _expiresAt;

    public CookieContainer Cookies { get; } = new();

    public async Task<string?> GetAsync(Func<CancellationToken, Task<string?>> fetch, CancellationToken ct)
    {
        if (_crumb is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _crumb;

        await _lock.WaitAsync(ct);
        try
        {
            if (_crumb is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _crumb;

            _crumb = await fetch(ct);
            _expiresAt = DateTimeOffset.UtcNow + Lifetime;
            return _crumb;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// Scarta il crumb se è ancora quello rifiutato da Yahoo.
    public void Invalidate(string? rejected)
    {
        if (_crumb == rejected)
            _crumb = null;
    }
}
