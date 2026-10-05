using YAFT.Domain.Common;
using YAFT.Domain.Stocks;

namespace YAFT.Domain.Tests;

public class StockSymbolTests
{
    [Theory]
    [InlineData("aapl", "AAPL")]
    [InlineData("  eni.mi ", "ENI.MI")]
    [InlineData("^GSPC", "^GSPC")]
    [InlineData("EURUSD=X", "EURUSD=X")]
    [InlineData("BRK-B", "BRK-B")]
    public void TryCreate_normalizza_simboli_validi(string raw, string expected)
    {
        Assert.True(StockSymbol.TryCreate(raw, out var symbol));
        Assert.Equal(expected, symbol!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("AA PL")]
    [InlineData("A/B")]
    [InlineData("TOOLONGSYMBOL1234")]
    public void TryCreate_rifiuta_simboli_non_validi(string? raw)
    {
        Assert.False(StockSymbol.TryCreate(raw, out var symbol));
        Assert.Null(symbol);
    }

    [Fact]
    public void Create_lancia_DomainException_se_non_valido() =>
        Assert.Throws<DomainException>(() => StockSymbol.Create("a b"));

    [Fact]
    public void Simboli_uguali_sono_uguali() =>
        Assert.Equal(StockSymbol.Create("aapl"), StockSymbol.Create("AAPL"));
}
