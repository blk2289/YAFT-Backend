using System.Text.RegularExpressions;

namespace YAFT.Domain.Stocks;

/// <summary>Simbolo Yahoo Finance normalizzato (es. AAPL, ENI.MI, ^GSPC, EURUSD=X).</summary>
public sealed partial record StockSymbol
{
    public string Value { get; }

    private StockSymbol(string value) => Value = value;

    // Almeno una lettera o cifra: esclude "." e "..", non validi anche come id documento Firestore.
    [GeneratedRegex(@"^(?=.*[A-Z0-9])[A-Z0-9.\-=^]{1,15}$")]
    private static partial Regex SymbolRegex();

    public static bool TryCreate(string? raw, out StockSymbol? symbol)
    {
        symbol = null;
        var normalized = raw?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized) || !SymbolRegex().IsMatch(normalized))
            return false;

        symbol = new StockSymbol(normalized);
        return true;
    }

    public static StockSymbol Create(string? raw) =>
        TryCreate(raw, out var symbol)
            ? symbol!
            : throw new Common.DomainException($"Simbolo '{raw}' non valido.");

    public override string ToString() => Value;
}
