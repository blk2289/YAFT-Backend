using NetArchTest.Rules;

namespace YAFT.Architecture.Tests;

/// <summary>La regola della dipendenza: le dipendenze puntano solo verso l'interno.</summary>
public class DependencyRuleTests
{
    private static readonly System.Reflection.Assembly Domain = typeof(YAFT.Domain.Stocks.StockSymbol).Assembly;
    private static readonly System.Reflection.Assembly Application = typeof(YAFT.Application.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly Infrastructure = typeof(YAFT.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_non_dipende_da_altri_layer_ne_da_framework()
    {
        var result = Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("YAFT.Application", "YAFT.Infrastructure", "YAFT.Api",
                "Microsoft.AspNetCore", "Microsoft.Extensions", "Google", "System.Net.Http")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Application_non_dipende_da_Infrastructure_Api_o_dettagli_tecnici()
    {
        var result = Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny("YAFT.Infrastructure", "YAFT.Api",
                "Microsoft.AspNetCore", "Google", "System.Net.Http")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Infrastructure_non_dipende_da_Api()
    {
        var result = Types.InAssembly(Infrastructure).ShouldNot()
            .HaveDependencyOnAny("YAFT.Api", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Implementazioni_di_Infrastructure_non_sono_pubbliche()
    {
        var result = Types.InAssembly(Infrastructure)
            .That().ResideInNamespaceStartingWith("YAFT.Infrastructure.Yahoo")
            .Or().ResideInNamespace("YAFT.Infrastructure.Firebase")
            .And().DoNotHaveNameEndingWith("Options")
            .ShouldNot().BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    private static string Failing(TestResult result) =>
        "Tipi non conformi: " + string.Join(", ", result.FailingTypeNames ?? []);
}
