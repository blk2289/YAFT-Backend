namespace YAFT.Domain.Stocks;

/// <summary>Titolo disponibile restituito da una ricerca o da una lista predefinita.</summary>
public sealed record SymbolSearchResult(
    string Symbol,
    string? Name,
    string? Exchange,
    string? Type,
    decimal? Price = null,
    decimal? ChangePercent = null);
