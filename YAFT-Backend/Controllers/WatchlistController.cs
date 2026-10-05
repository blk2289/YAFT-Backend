using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YAFT.Api.Extensions;
using YAFT.Application.Watchlist;
using YAFT.Application.Watchlist.AddToWatchlist;
using YAFT.Application.Watchlist.GetWatchlist;
using YAFT.Application.Watchlist.RemoveFromWatchlist;

namespace YAFT.Api.Controllers;

/// <summary>Titoli preferiti dell'utente identificato dal Firebase ID token.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class WatchlistController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WatchlistItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromServices] GetWatchlistHandler handler, CancellationToken ct)
        => this.ToActionResult(await handler.HandleAsync(ct));

    public sealed record AddToWatchlistRequest(string Symbol);

    [HttpPost]
    [ProducesResponseType<WatchlistItemDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Add(
        [FromBody] AddToWatchlistRequest request,
        [FromServices] AddToWatchlistHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new AddToWatchlistCommand(request.Symbol), ct);
        return this.ToActionResult(result, item => Created($"/api/watchlist/{Uri.EscapeDataString(item.Symbol)}", item));
    }

    [HttpDelete("{symbol}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(
        string symbol, [FromServices] RemoveFromWatchlistHandler handler, CancellationToken ct)
        => this.ToActionResult(await handler.HandleAsync(symbol, ct));
}
