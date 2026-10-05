using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YAFT.Api.Extensions;
using YAFT.Application.Stocks;
using YAFT.Application.Stocks.GetHistory;
using YAFT.Application.Stocks.GetQuote;
using YAFT.Application.Stocks.GetQuotes;
using YAFT.Application.Stocks.ListStocks;

namespace YAFT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class StocksController : ControllerBase
{
    /// <summary>Elenco dei titoli disponibili: lista predefinita oppure ricerca testuale.</summary>
    /// <remarks>
    /// GET api/stocks?list=most_actives&amp;region=US&amp;count=25 |
    /// GET api/stocks?list=trending&amp;region=IT |
    /// GET api/stocks?q=eni
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<StockListDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? list, [FromQuery] string? q, [FromQuery] string? region, [FromQuery] int? count,
        [FromServices] ListStocksHandler handler, CancellationToken ct)
        => this.ToActionResult(await handler.HandleAsync(new ListStocksQuery(list, q, region, count), ct));

    /// <summary>Quotazioni di titoli specifici.</summary>
    /// <remarks>GET api/stocks/quotes?symbols=AAPL,ENI.MI,MSFT (max 20)</remarks>
    [HttpGet("quotes")]
    [ProducesResponseType<IReadOnlyList<QuoteResultDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuotes(
        [FromQuery] string? symbols, [FromServices] GetQuotesHandler handler, CancellationToken ct)
        => this.ToActionResult(await handler.HandleAsync(symbols, ct));

    /// <summary>Quotazione di un singolo titolo.</summary>
    /// <remarks>GET api/stocks/AAPL | api/stocks/ENI.MI</remarks>
    [HttpGet("{symbol}")]
    [ProducesResponseType<StockQuoteDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuote(
        string symbol, [FromServices] GetQuoteHandler handler, CancellationToken ct)
        => this.ToActionResult(await handler.HandleAsync(symbol, ct));

    /// <summary>Storico prezzi di un titolo.</summary>
    /// <remarks>GET api/stocks/AAPL/history?range=1mo&amp;interval=1d</remarks>
    [HttpGet("{symbol}/history")]
    [ProducesResponseType<PriceHistoryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        string symbol, [FromQuery] string? range, [FromQuery] string? interval,
        [FromServices] GetHistoryHandler handler, CancellationToken ct)
        => this.ToActionResult(await handler.HandleAsync(new GetHistoryQuery(symbol, range, interval), ct));
}
