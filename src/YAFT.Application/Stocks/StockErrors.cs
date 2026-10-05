using YAFT.Application.Common;

namespace YAFT.Application.Stocks;

public static class StockErrors
{
    public static Error InvalidSymbol(string? raw) =>
        new("stock.symbol_invalid", $"Simbolo '{raw}' non valido.", ErrorType.Validation);

    public static Error NotFound(string symbol) =>
        new("stock.not_found", $"Titolo '{symbol}' non trovato.", ErrorType.NotFound);

    public static readonly Error Unavailable =
        new("stock.provider_unavailable", "Servizio dati di borsa non disponibile.", ErrorType.Unavailable);
}
