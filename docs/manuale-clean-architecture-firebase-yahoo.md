# Manuale: REST API C# con Clean Architecture, Firebase e Yahoo Finance

Guida allo sviluppo di una REST API **ASP.NET Core (.NET 8+)** organizzata secondo la **Clean Architecture**, con:

- **Firebase Authentication** per l'identità degli utenti (JWT)
- **Firestore** per la persistenza (esempio: note per utente)
- **Yahoo Finance** come sorgente esterna di quotazioni di borsa

---

## Indice

1. [Principi](#1-principi)
2. [Struttura della solution](#2-struttura-della-solution)
3. [Creazione dei progetti](#3-creazione-dei-progetti)
4. [Layer Domain](#4-layer-domain)
5. [Layer Application](#5-layer-application)
6. [Layer Infrastructure](#6-layer-infrastructure)
7. [Layer Api](#7-layer-api)
8. [Flusso di una richiesta](#8-flusso-di-una-richiesta)
9. [Testing](#9-testing)
10. [Configurazione e sicurezza](#10-configurazione-e-sicurezza)
11. [Aggiungere una nuova funzionalità](#11-aggiungere-una-nuova-funzionalità)
12. [Errori comuni](#12-errori-comuni)
13. [Checklist finale](#13-checklist-finale)

---

## 1. Principi

### La regola della dipendenza

Le dipendenze del codice puntano **sempre verso l'interno**. Il nucleo non conosce i dettagli tecnici (framework, database, HTTP).

```
        ┌──────────────────────────────────────────┐
        │  Api (Controller, Program, Auth)         │
        │  ┌────────────────────────────────────┐  │
        │  │ Infrastructure (Firestore, Yahoo)  │  │
        │  │  ┌──────────────────────────────┐  │  │
        │  │  │ Application (Use case,       │  │  │
        │  │  │ interfacce, Result)          │  │  │
        │  │  │  ┌────────────────────────┐  │  │  │
        │  │  │  │ Domain (Entità, regole)│  │  │  │
        │  │  │  └────────────────────────┘  │  │  │
        │  │  └──────────────────────────────┘  │  │
        │  └────────────────────────────────────┘  │
        └──────────────────────────────────────────┘
```

| Layer | Contiene | Dipende da |
|---|---|---|
| **Domain** | Entità, value object, regole di business | Niente |
| **Application** | Casi d'uso, interfacce (porte), DTO, `Result` | Domain |
| **Infrastructure** | Implementazioni delle porte: Firestore, Yahoo, cache | Application, Domain |
| **Api** | Controller, autenticazione, composizione (DI) | Application, Infrastructure |

### Porte e adattatori

L'Application **definisce** le interfacce di cui ha bisogno (`INoteRepository`, `IStockQuoteProvider`). L'Infrastructure le **implementa**. L'Api collega le due cose tramite dependency injection.

Vantaggio: puoi sostituire Yahoo Finance con un altro provider, o Firestore con un altro database, **senza toccare** casi d'uso e dominio.

---

## 2. Struttura della solution

```
MyApp/
├── MyApp.sln
├── src/
│   ├── MyApp.Domain/
│   │   ├── Notes/
│   │   │   └── Note.cs
│   │   ├── Stocks/
│   │   │   ├── StockSymbol.cs
│   │   │   └── StockQuote.cs
│   │   └── Common/
│   │       └── DomainException.cs
│   │
│   ├── MyApp.Application/
│   │   ├── Abstractions/
│   │   │   ├── ICurrentUser.cs
│   │   │   ├── INoteRepository.cs
│   │   │   └── IStockQuoteProvider.cs
│   │   ├── Common/
│   │   │   └── Result.cs
│   │   ├── Notes/
│   │   │   ├── CreateNote/
│   │   │   └── GetNotes/
│   │   ├── Stocks/
│   │   │   └── GetStockQuote/
│   │   └── DependencyInjection.cs
│   │
│   ├── MyApp.Infrastructure/
│   │   ├── Firebase/
│   │   │   ├── FirebaseOptions.cs
│   │   │   ├── NoteDocument.cs
│   │   │   └── FirestoreNoteRepository.cs
│   │   ├── Yahoo/
│   │   │   ├── YahooDtos.cs
│   │   │   ├── YahooFinanceQuoteProvider.cs
│   │   │   └── CachedStockQuoteProvider.cs
│   │   └── DependencyInjection.cs
│   │
│   └── MyApp.Api/
│       ├── Controllers/
│       │   ├── NotesController.cs
│       │   └── StocksController.cs
│       ├── Auth/
│       │   └── CurrentUser.cs
│       ├── Extensions/
│       │   └── ResultExtensions.cs
│       ├── Program.cs
│       └── appsettings.json
│
└── tests/
    ├── MyApp.Domain.Tests/
    ├── MyApp.Application.Tests/
    └── MyApp.Architecture.Tests/
```

---

## 3. Creazione dei progetti

```bash
mkdir MyApp && cd MyApp
dotnet new sln -n MyApp

dotnet new classlib -n MyApp.Domain         -o src/MyApp.Domain
dotnet new classlib -n MyApp.Application    -o src/MyApp.Application
dotnet new classlib -n MyApp.Infrastructure -o src/MyApp.Infrastructure
dotnet new webapi   -n MyApp.Api            -o src/MyApp.Api --use-controllers

dotnet sln add src/MyApp.Domain src/MyApp.Application \
               src/MyApp.Infrastructure src/MyApp.Api

# Riferimenti (rispettano la regola della dipendenza)
dotnet add src/MyApp.Application    reference src/MyApp.Domain
dotnet add src/MyApp.Infrastructure reference src/MyApp.Application
dotnet add src/MyApp.Api            reference src/MyApp.Application
dotnet add src/MyApp.Api            reference src/MyApp.Infrastructure
```

> `MyApp.Domain` **non ha riferimenti** a nessun altro progetto e nessun pacchetto NuGet.

### Pacchetti NuGet

```bash
# Infrastructure
dotnet add src/MyApp.Infrastructure package FirebaseAdmin
dotnet add src/MyApp.Infrastructure package Google.Cloud.Firestore
dotnet add src/MyApp.Infrastructure package Microsoft.Extensions.Http
dotnet add src/MyApp.Infrastructure package Microsoft.Extensions.Caching.Memory
dotnet add src/MyApp.Infrastructure package Microsoft.Extensions.Options.ConfigurationExtensions

# Api
dotnet add src/MyApp.Api package Microsoft.AspNetCore.Authentication.JwtBearer

# Application (solo per registrare i servizi)
dotnet add src/MyApp.Application package Microsoft.Extensions.DependencyInjection.Abstractions
```

Se `MyApp.Infrastructure` usa `IConfiguration`/`IServiceCollection` con il template `classlib`, aggiungi anche `Microsoft.Extensions.Configuration.Abstractions` e `Microsoft.Extensions.DependencyInjection.Abstractions`.

---

## 4. Layer Domain

Il dominio contiene le **regole di business pure**: nessun riferimento a Firebase, HTTP o ASP.NET.

### 4.1 Eccezione di dominio

```csharp
// Domain/Common/DomainException.cs
namespace MyApp.Domain.Common;

public sealed class DomainException(string message) : Exception(message);
```

### 4.2 Entità `Note`

```csharp
// Domain/Notes/Note.cs
using MyApp.Domain.Common;

namespace MyApp.Domain.Notes;

public sealed class Note
{
    public string Id { get; }
    public string OwnerId { get; }
    public string Title { get; private set; }
    public string Text { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private Note(string id, string ownerId, string title, string text, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerId = ownerId;
        Title = title;
        Text = text;
        CreatedAt = createdAt;
    }

    /// Crea una nuova nota applicando le regole di business.
    public static Note Create(string ownerId, string title, string text, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new DomainException("La nota deve avere un proprietario.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Il titolo è obbligatorio.");
        if (title.Length > 100)
            throw new DomainException("Il titolo non può superare 100 caratteri.");
        if (text?.Length > 5000)
            throw new DomainException("Il testo non può superare 5000 caratteri.");

        return new Note(Guid.NewGuid().ToString("N"), ownerId, title.Trim(), text ?? "", now);
    }

    /// Ricostruisce una nota già esistente (usato dai repository, non applica validazioni di creazione).
    public static Note Rehydrate(string id, string ownerId, string title, string text, DateTimeOffset createdAt)
        => new(id, ownerId, title, text, createdAt);
}
```

### 4.3 Value object `StockSymbol`

```csharp
// Domain/Stocks/StockSymbol.cs
using System.Text.RegularExpressions;

namespace MyApp.Domain.Stocks;

public sealed partial record StockSymbol
{
    public string Value { get; }

    private StockSymbol(string value) => Value = value;

    [GeneratedRegex(@"^[A-Z0-9.\-=^]{1,15}$")]
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

    public override string ToString() => Value;
}
```

### 4.4 Modello `StockQuote`

```csharp
// Domain/Stocks/StockQuote.cs
namespace MyApp.Domain.Stocks;

public sealed record StockQuote(
    StockSymbol Symbol,
    string? Name,
    string? Currency,
    string? Exchange,
    decimal Price,
    decimal PreviousClose,
    decimal? DayHigh,
    decimal? DayLow,
    long? Volume,
    DateTimeOffset? MarketTime)
{
    // Logica di business: la variazione si calcola nel dominio, non nel provider.
    public decimal Change => Price - PreviousClose;

    public decimal ChangePercent =>
        PreviousClose == 0 ? 0 : Math.Round(Change / PreviousClose * 100, 2);
}
```

---

## 5. Layer Application

Contiene i **casi d'uso** e le **porte** (interfacce). Non sa nulla di Firestore, Yahoo o HTTP.

### 5.1 Result pattern

Gli errori "attesi" (validazione, non trovato, servizio non disponibile) vengono restituiti come valore, non come eccezione.

```csharp
// Application/Common/Result.cs
namespace MyApp.Application.Common;

public enum ErrorType { Validation, NotFound, Unauthorized, Unavailable }

public sealed record Error(string Code, string Description, ErrorType Type);

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value) { _value = value; }
    private Result(Error error) { Error = error; }

    public bool IsSuccess => Error is null;
    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Il risultato è un errore.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}
```

### 5.2 Porte (interfacce)

```csharp
// Application/Abstractions/ICurrentUser.cs
namespace MyApp.Application.Abstractions;

public interface ICurrentUser
{
    /// UID Firebase dell'utente autenticato, oppure null.
    string? UserId { get; }
}
```

```csharp
// Application/Abstractions/INoteRepository.cs
using MyApp.Domain.Notes;

namespace MyApp.Application.Abstractions;

public interface INoteRepository
{
    Task AddAsync(Note note, CancellationToken ct);
    Task<IReadOnlyList<Note>> GetByOwnerAsync(string ownerId, CancellationToken ct);
}
```

```csharp
// Application/Abstractions/IStockQuoteProvider.cs
using MyApp.Application.Common;
using MyApp.Domain.Stocks;

namespace MyApp.Application.Abstractions;

public interface IStockQuoteProvider
{
    Task<Result<StockQuote>> GetQuoteAsync(StockSymbol symbol, CancellationToken ct);
}
```

### 5.3 Caso d'uso: creare una nota

```csharp
// Application/Notes/CreateNote/CreateNoteCommand.cs
namespace MyApp.Application.Notes.CreateNote;

public sealed record CreateNoteCommand(string Title, string Text);
```

```csharp
// Application/Notes/CreateNote/CreateNoteHandler.cs
using MyApp.Application.Abstractions;
using MyApp.Application.Common;
using MyApp.Domain.Common;
using MyApp.Domain.Notes;

namespace MyApp.Application.Notes.CreateNote;

public sealed class CreateNoteHandler(
    INoteRepository repository,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<Result<string>> HandleAsync(CreateNoteCommand command, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return new Error("auth.required", "Utente non autenticato.", ErrorType.Unauthorized);

        try
        {
            var note = Note.Create(currentUser.UserId, command.Title, command.Text, clock.GetUtcNow());
            await repository.AddAsync(note, ct);
            return note.Id;
        }
        catch (DomainException ex)
        {
            return new Error("note.invalid", ex.Message, ErrorType.Validation);
        }
    }
}
```

### 5.4 Caso d'uso: elenco note

```csharp
// Application/Notes/GetNotes/GetNotesHandler.cs
using MyApp.Application.Abstractions;
using MyApp.Application.Common;

namespace MyApp.Application.Notes.GetNotes;

public sealed record NoteDto(string Id, string Title, string Text, DateTimeOffset CreatedAt);

public sealed class GetNotesHandler(INoteRepository repository, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<NoteDto>>> HandleAsync(CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return new Error("auth.required", "Utente non autenticato.", ErrorType.Unauthorized);

        var notes = await repository.GetByOwnerAsync(currentUser.UserId, ct);

        IReadOnlyList<NoteDto> dtos = notes
            .Select(n => new NoteDto(n.Id, n.Title, n.Text, n.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<NoteDto>>.Success(dtos);
    }
}
```

### 5.5 Caso d'uso: quotazione di un titolo

```csharp
// Application/Stocks/GetStockQuote/GetStockQuoteHandler.cs
using MyApp.Application.Abstractions;
using MyApp.Application.Common;
using MyApp.Domain.Stocks;

namespace MyApp.Application.Stocks.GetStockQuote;

public sealed record StockQuoteDto(
    string Symbol, string? Name, string? Currency, string? Exchange,
    decimal Price, decimal PreviousClose, decimal Change, decimal ChangePercent,
    decimal? DayHigh, decimal? DayLow, long? Volume, DateTimeOffset? MarketTime);

public sealed class GetStockQuoteHandler(IStockQuoteProvider provider)
{
    public async Task<Result<StockQuoteDto>> HandleAsync(string rawSymbol, CancellationToken ct)
    {
        if (!StockSymbol.TryCreate(rawSymbol, out var symbol))
            return new Error("stock.symbol_invalid", "Simbolo non valido.", ErrorType.Validation);

        var result = await provider.GetQuoteAsync(symbol!, ct);
        if (!result.IsSuccess)
            return result.Error!;

        var q = result.Value;
        return new StockQuoteDto(
            q.Symbol.Value, q.Name, q.Currency, q.Exchange,
            q.Price, q.PreviousClose, q.Change, q.ChangePercent,
            q.DayHigh, q.DayLow, q.Volume, q.MarketTime);
    }
}
```

### 5.6 Registrazione dei servizi Application

```csharp
// Application/DependencyInjection.cs
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Notes.CreateNote;
using MyApp.Application.Notes.GetNotes;
using MyApp.Application.Stocks.GetStockQuote;

namespace MyApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateNoteHandler>();
        services.AddScoped<GetNotesHandler>();
        services.AddScoped<GetStockQuoteHandler>();
        return services;
    }
}
```

> **Nota su MediatR**: puoi usarlo al posto degli handler "a mano", ma le versioni recenti hanno cambiato modello di licenza. Verifica i termini prima di adottarlo. Gli handler semplici mostrati qui non richiedono librerie.

---

## 6. Layer Infrastructure

Qui vivono **tutti i dettagli tecnici**. I tipi sono `internal`: l'Api vede solo `AddInfrastructure(...)`.

### 6.1 Opzioni Firebase

```csharp
// Infrastructure/Firebase/FirebaseOptions.cs
namespace MyApp.Infrastructure.Firebase;

public sealed class FirebaseOptions
{
    public const string Section = "Firebase";

    public string ProjectId { get; set; } = "";

    /// Percorso del service account JSON. Lascia vuoto su Google Cloud (Application Default Credentials).
    public string? CredentialPath { get; set; }
}
```

### 6.2 Modello di persistenza `NoteDocument`

Il documento Firestore è **separato** dall'entità di dominio. In questo modo gli attributi `[Firestore...]` restano fuori dal Domain.

```csharp
// Infrastructure/Firebase/NoteDocument.cs
using Google.Cloud.Firestore;
using MyApp.Domain.Notes;

namespace MyApp.Infrastructure.Firebase;

[FirestoreData]
internal sealed class NoteDocument
{
    [FirestoreProperty] public string OwnerId { get; set; } = "";
    [FirestoreProperty] public string Title { get; set; } = "";
    [FirestoreProperty] public string Text { get; set; } = "";
    [FirestoreProperty] public Timestamp CreatedAt { get; set; }

    public static NoteDocument FromDomain(Note n) => new()
    {
        OwnerId = n.OwnerId,
        Title = n.Title,
        Text = n.Text,
        CreatedAt = Timestamp.FromDateTimeOffset(n.CreatedAt)
    };

    public Note ToDomain(string id) =>
        Note.Rehydrate(id, OwnerId, Title, Text, CreatedAt.ToDateTimeOffset());
}
```

### 6.3 Repository Firestore

```csharp
// Infrastructure/Firebase/FirestoreNoteRepository.cs
using Google.Cloud.Firestore;
using MyApp.Application.Abstractions;
using MyApp.Domain.Notes;

namespace MyApp.Infrastructure.Firebase;

internal sealed class FirestoreNoteRepository(FirestoreDb db) : INoteRepository
{
    private CollectionReference Notes => db.Collection("notes");

    public async Task AddAsync(Note note, CancellationToken ct)
    {
        await Notes.Document(note.Id).SetAsync(NoteDocument.FromDomain(note), cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Note>> GetByOwnerAsync(string ownerId, CancellationToken ct)
    {
        var snapshot = await Notes
            .WhereEqualTo("OwnerId", ownerId)
            .OrderByDescending("CreatedAt")
            .GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(d => d.ConvertTo<NoteDocument>().ToDomain(d.Id))
            .ToList();
    }
}
```

> La query con `WhereEqualTo` + `OrderByDescending` su campi diversi richiede un **indice composito**. Alla prima esecuzione Firestore restituisce un errore con il link per crearlo.

### 6.4 Client Yahoo Finance

I DTO del JSON di Yahoo restano **interni** all'Infrastructure.

```csharp
// Infrastructure/Yahoo/YahooDtos.cs
namespace MyApp.Infrastructure.Yahoo;

internal sealed class YahooChartResponse { public YahooChart? Chart { get; set; } }

internal sealed class YahooChart
{
    public List<YahooResult>? Result { get; set; }
    public YahooError? Error { get; set; }
}

internal sealed class YahooError
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

internal sealed class YahooResult { public YahooMeta Meta { get; set; } = new(); }

internal sealed class YahooMeta
{
    public string? Symbol { get; set; }
    public string? LongName { get; set; }
    public string? ShortName { get; set; }
    public string? Currency { get; set; }
    public string? ExchangeName { get; set; }
    public decimal? RegularMarketPrice { get; set; }
    public decimal? ChartPreviousClose { get; set; }
    public decimal? RegularMarketDayHigh { get; set; }
    public decimal? RegularMarketDayLow { get; set; }
    public long? RegularMarketVolume { get; set; }
    public long? RegularMarketTime { get; set; }
}
```

```csharp
// Infrastructure/Yahoo/YahooFinanceQuoteProvider.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.Common;
using MyApp.Domain.Stocks;

namespace MyApp.Infrastructure.Yahoo;

internal sealed class YahooFinanceQuoteProvider(
    HttpClient http,
    ILogger<YahooFinanceQuoteProvider> logger) : IStockQuoteProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<Result<StockQuote>> GetQuoteAsync(StockSymbol symbol, CancellationToken ct)
    {
        try
        {
            var url = $"v8/finance/chart/{Uri.EscapeDataString(symbol.Value)}?range=1d&interval=1d";
            using var response = await http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return NotFound(symbol);

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<YahooChartResponse>(JsonOpts, ct);
            var meta = data?.Chart?.Result?.FirstOrDefault()?.Meta;

            if (meta?.RegularMarketPrice is null || meta.ChartPreviousClose is null)
                return NotFound(symbol);

            return new StockQuote(
                symbol,
                meta.LongName ?? meta.ShortName,
                meta.Currency,
                meta.ExchangeName,
                meta.RegularMarketPrice.Value,
                meta.ChartPreviousClose.Value,
                meta.RegularMarketDayHigh,
                meta.RegularMarketDayLow,
                meta.RegularMarketVolume,
                meta.RegularMarketTime is { } t ? DateTimeOffset.FromUnixTimeSeconds(t) : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (ct.IsCancellationRequested) throw;   // annullata dal client: non è un errore del provider

            logger.LogWarning(ex, "Yahoo Finance non disponibile per {Symbol}", symbol);
            return new Error("stock.provider_unavailable",
                "Servizio dati di borsa non disponibile.", ErrorType.Unavailable);
        }
    }

    private static Error NotFound(StockSymbol s) =>
        new("stock.not_found", $"Titolo '{s}' non trovato.", ErrorType.NotFound);
}
```

### 6.5 Cache come Decorator

La cache **non** sta nel controller né nel provider: è un decorator che avvolge il provider reale. Il caso d'uso non se ne accorge.

```csharp
// Infrastructure/Yahoo/CachedStockQuoteProvider.cs
using Microsoft.Extensions.Caching.Memory;
using MyApp.Application.Abstractions;
using MyApp.Application.Common;
using MyApp.Domain.Stocks;

namespace MyApp.Infrastructure.Yahoo;

internal sealed class CachedStockQuoteProvider(
    IStockQuoteProvider inner,
    IMemoryCache cache) : IStockQuoteProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task<Result<StockQuote>> GetQuoteAsync(StockSymbol symbol, CancellationToken ct)
    {
        var key = $"quote:{symbol.Value}";

        if (cache.TryGetValue(key, out StockQuote? cached) && cached is not null)
            return cached;

        var result = await inner.GetQuoteAsync(symbol, ct);

        if (result.IsSuccess)                       // non si mettono in cache gli errori
            cache.Set(key, result.Value, Ttl);

        return result;
    }
}
```

### 6.6 Registrazione dei servizi Infrastructure

```csharp
// Infrastructure/DependencyInjection.cs
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Abstractions;
using MyApp.Infrastructure.Firebase;
using MyApp.Infrastructure.Yahoo;

namespace MyApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection(FirebaseOptions.Section).Get<FirebaseOptions>()
                      ?? throw new InvalidOperationException("Sezione 'Firebase' mancante.");

        services.AddFirebase(options);
        services.AddYahoo();
        return services;
    }

    private static void AddFirebase(this IServiceCollection services, FirebaseOptions options)
    {
        var hasFile = !string.IsNullOrWhiteSpace(options.CredentialPath);

        // Admin SDK (necessario solo se usi FirebaseAuth.DefaultInstance, custom claims, ecc.)
        if (FirebaseApp.DefaultInstance is null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = hasFile
                    ? GoogleCredential.FromFile(options.CredentialPath)
                    : GoogleCredential.GetApplicationDefault(),
                ProjectId = options.ProjectId
            });
        }

        // Firestore
        services.AddSingleton(new FirestoreDbBuilder
        {
            ProjectId = options.ProjectId,
            CredentialsPath = hasFile ? options.CredentialPath : null
        }.Build());

        services.AddScoped<INoteRepository, FirestoreNoteRepository>();
    }

    private static void AddYahoo(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddHttpClient<YahooFinanceQuoteProvider>(client =>
        {
            client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        });

        // Decorator: la porta IStockQuoteProvider è la versione con cache.
        services.AddScoped<IStockQuoteProvider>(sp => new CachedStockQuoteProvider(
            sp.GetRequiredService<YahooFinanceQuoteProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
    }
}
```

> **Nota sui dati Yahoo**: Yahoo Finance non offre un'API pubblica ufficiale. Gli endpoint usati sono non documentati e possono cambiare o essere limitati senza preavviso. Grazie alla porta `IStockQuoteProvider`, passare a un provider ufficiale (Alpha Vantage, Twelve Data, Finnhub, ecc.) significa scrivere **una sola nuova classe** in Infrastructure.

---

## 7. Layer Api

Il layer più esterno: traduce HTTP in chiamate ai casi d'uso e viceversa. **Nessuna logica di business qui.**

### 7.1 `ICurrentUser` basato sul token

```csharp
// Api/Auth/CurrentUser.cs
using System.Security.Claims;
using MyApp.Application.Abstractions;

namespace MyApp.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // Firebase inserisce l'UID nei claim "user_id" e "sub".
    public string? UserId =>
        accessor.HttpContext?.User.FindFirstValue("user_id")
        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
```

### 7.2 Mappatura `Result` → HTTP

```csharp
// Api/Extensions/ResultExtensions.cs
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Common;

namespace MyApp.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result,
        Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess is null ? controller.Ok(result.Value) : onSuccess(result.Value);

        var error = result.Error!;
        var status = error.Type switch
        {
            ErrorType.Validation   => StatusCodes.Status400BadRequest,
            ErrorType.NotFound     => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Unavailable  => StatusCodes.Status502BadGateway,
            _                      => StatusCodes.Status500InternalServerError
        };

        return controller.Problem(title: error.Code, detail: error.Description, statusCode: status);
    }
}
```

### 7.3 Controller

I controller sono **sottili**: ricevono la richiesta, invocano l'handler, mappano il risultato.

```csharp
// Api/Controllers/NotesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.Api.Extensions;
using MyApp.Application.Notes.CreateNote;
using MyApp.Application.Notes.GetNotes;

namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromServices] GetNotesHandler handler, CancellationToken ct)
        => this.ToActionResult(await handler.HandleAsync(ct));

    public sealed record CreateNoteRequest(string Title, string Text);

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateNoteRequest request,
        [FromServices] CreateNoteHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CreateNoteCommand(request.Title, request.Text), ct);
        return this.ToActionResult(result, id => Created($"api/notes/{id}", new { id }));
    }
}
```

```csharp
// Api/Controllers/StocksController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.Api.Extensions;
using MyApp.Application.Stocks.GetStockQuote;

namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StocksController : ControllerBase
{
    /// GET api/stocks/AAPL   |   api/stocks/ENI.MI
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetQuote(
        string symbol, [FromServices] GetStockQuoteHandler handler, CancellationToken ct)
        => this.ToActionResult(await handler.HandleAsync(symbol, ct));
}
```

### 7.4 Program.cs (composition root)

```csharp
// Api/Program.cs
using MyApp.Api.Auth;
using MyApp.Application;
using MyApp.Application.Abstractions;
using MyApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var projectId = builder.Configuration["Firebase:ProjectId"]
    ?? throw new InvalidOperationException("Firebase:ProjectId mancante.");

// Layer
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Utente corrente
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Autenticazione: validazione dei JWT emessi da Firebase
builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = $"https://securetoken.google.com/{projectId}";
        o.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{projectId}",
            ValidateAudience = true,
            ValidAudience = projectId,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();      // eccezioni inattese -> ProblemDetails 500
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### 7.5 appsettings.json

```json
{
  "Firebase": {
    "ProjectId": "il-tuo-project-id",
    "CredentialPath": ""
  }
}
```

In sviluppo imposta il percorso tramite **user-secrets** o variabile d'ambiente (vedi sezione 10).

---

## 8. Flusso di una richiesta

Esempio: `GET /api/stocks/ENI.MI`

```
Client
  │  Authorization: Bearer <Firebase ID token>
  ▼
[Api]  JwtBearer valida il token
  ▼
[Api]  StocksController.GetQuote("ENI.MI")
  ▼
[Application]  GetStockQuoteHandler
        ├─ StockSymbol.TryCreate()            → validazione (Domain)
        └─ IStockQuoteProvider.GetQuoteAsync()
  ▼
[Infrastructure]  CachedStockQuoteProvider     → hit in cache? ritorna
        └─ YahooFinanceQuoteProvider           → chiamata HTTP a Yahoo
  ▼
Result<StockQuote> risale fino al controller
  ▼
[Api]  ToActionResult() → 200 OK / 400 / 404 / 502
```

Le frecce delle **dipendenze di compilazione** vanno verso l'interno, mentre il **flusso di esecuzione** attraversa i layer: è resa possibile dalle interfacce e dalla DI.

---

## 9. Testing

Il grande vantaggio della Clean Architecture: la logica importante si testa **senza Firebase, senza HTTP e senza ASP.NET**.

### 9.1 Test del dominio

```bash
dotnet new xunit -n MyApp.Domain.Tests -o tests/MyApp.Domain.Tests
dotnet add tests/MyApp.Domain.Tests reference src/MyApp.Domain
dotnet sln add tests/MyApp.Domain.Tests
```

```csharp
public class StockQuoteTests
{
    [Fact]
    public void ChangePercent_IsCalculatedFromPreviousClose()
    {
        StockSymbol.TryCreate("AAPL", out var symbol);
        var quote = new StockQuote(symbol!, null, "USD", null, 110m, 100m, null, null, null, null);

        Assert.Equal(10m, quote.Change);
        Assert.Equal(10m, quote.ChangePercent);
    }

    [Theory]
    [InlineData("AAPL", true)]
    [InlineData("eni.mi", true)]
    [InlineData("A A", false)]
    [InlineData("../etc", false)]
    [InlineData("", false)]
    public void StockSymbol_Validation(string raw, bool expected)
        => Assert.Equal(expected, StockSymbol.TryCreate(raw, out _));
}
```

### 9.2 Test dei casi d'uso con fake

```bash
dotnet new xunit -n MyApp.Application.Tests -o tests/MyApp.Application.Tests
dotnet add tests/MyApp.Application.Tests reference src/MyApp.Application
dotnet add tests/MyApp.Application.Tests package NSubstitute
dotnet sln add tests/MyApp.Application.Tests
```

```csharp
using NSubstitute;

public class CreateNoteHandlerTests
{
    private readonly INoteRepository _repo = Substitute.For<INoteRepository>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly CreateNoteHandler _sut;

    public CreateNoteHandlerTests()
        => _sut = new CreateNoteHandler(_repo, _user, TimeProvider.System);

    [Fact]
    public async Task Fails_when_user_is_not_authenticated()
    {
        _user.UserId.Returns((string?)null);

        var result = await _sut.HandleAsync(new CreateNoteCommand("Titolo", "Testo"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        await _repo.DidNotReceive().AddAsync(Arg.Any<Note>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Saves_note_for_current_user()
    {
        _user.UserId.Returns("uid-123");

        var result = await _sut.HandleAsync(new CreateNoteCommand("Titolo", "Testo"), default);

        Assert.True(result.IsSuccess);
        await _repo.Received(1).AddAsync(
            Arg.Is<Note>(n => n.OwnerId == "uid-123"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_validation_error_for_empty_title()
    {
        _user.UserId.Returns("uid-123");

        var result = await _sut.HandleAsync(new CreateNoteCommand("  ", "Testo"), default);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}
```

### 9.3 Architecture test: la regola della dipendenza in CI

Impedisce che qualcuno aggiunga per errore un riferimento sbagliato.

```bash
dotnet new xunit -n MyApp.Architecture.Tests -o tests/MyApp.Architecture.Tests
dotnet add tests/MyApp.Architecture.Tests package NetArchTest.Rules
dotnet add tests/MyApp.Architecture.Tests reference src/MyApp.Domain src/MyApp.Application src/MyApp.Infrastructure
dotnet sln add tests/MyApp.Architecture.Tests
```

```csharp
using NetArchTest.Rules;

public class ArchitectureTests
{
    private static readonly System.Reflection.Assembly Domain = typeof(MyApp.Domain.Notes.Note).Assembly;
    private static readonly System.Reflection.Assembly Application =
        typeof(MyApp.Application.DependencyInjection).Assembly;

    [Fact]
    public void Domain_DoesNotDependOnOtherLayers()
    {
        var result = Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("MyApp.Application", "MyApp.Infrastructure", "MyApp.Api")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny("MyApp.Infrastructure", "MyApp.Api",
                                 "Google.Cloud.Firestore", "FirebaseAdmin")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
```

### 9.4 Test di integrazione (opzionale)

- **Firestore**: usa l'**emulatore** (`firebase emulators:start`) e imposta la variabile `FIRESTORE_EMULATOR_HOST=localhost:8080`. Il client .NET la rispetta automaticamente.
- **API**: `WebApplicationFactory<Program>` per testare l'intera pipeline HTTP, sostituendo `IStockQuoteProvider` con un fake per non chiamare Yahoo nei test.

---

## 10. Configurazione e sicurezza

### Segreti in sviluppo

```bash
cd src/MyApp.Api
dotnet user-secrets init
dotnet user-secrets set "Firebase:CredentialPath" "C:/keys/firebase-key.json"
```

Oppure:

```bash
export GOOGLE_APPLICATION_CREDENTIALS="/percorso/firebase-key.json"
```

### `.gitignore`

```
firebase-key.json
*.serviceaccount.json
appsettings.*.local.json
```

> ⚠️ La chiave del service account dà accesso completo al progetto Firebase. Non committarla mai, non inserirla in immagini Docker, ruotala se viene esposta.

### Produzione

- **Google Cloud (Cloud Run, GKE, ecc.)**: lascia `CredentialPath` vuoto e assegna il ruolo IAM al service account dell'ambiente (*Application Default Credentials*).
- **Altri hosting**: usa un secret manager (Azure Key Vault, AWS Secrets Manager) o variabili d'ambiente, mai file nel repository.

### Autorizzazione

Il service account **bypassa le Security Rules di Firestore**. Il controllo dei permessi è quindi responsabilità del **tuo codice**:

- filtra sempre per `OwnerId` (come fa `GetByOwnerAsync`);
- verifica la proprietà prima di modificare o cancellare una risorsa;
- non fidarti mai di un `userId` passato dal client: leggilo solo dal token (`ICurrentUser`).

### Validazione input

- Il simbolo di borsa è validato dal value object `StockSymbol` prima di comporre l'URL.
- Il testo delle note ha limiti di lunghezza nel dominio.
- Yahoo applica rate limit: la cache riduce le chiamate; per più istanze dell'API considera una cache distribuita (Redis).

---

## 11. Aggiungere una nuova funzionalità

Esempio: **watchlist** per utente (elenco di simboli seguiti).

| Passo | Layer | Azione |
|---|---|---|
| 1 | Domain | Crea l'entità `Watchlist` con le regole (max 50 simboli, niente duplicati) e i relativi test |
| 2 | Application | Definisci `IWatchlistRepository` |
| 3 | Application | Crea `AddToWatchlistHandler` (+ command) usando `ICurrentUser` e `StockSymbol` |
| 4 | Application | Scrivi i test dell'handler con repository fake |
| 5 | Infrastructure | Crea `WatchlistDocument` e `FirestoreWatchlistRepository` |
| 6 | Infrastructure | Registra il repository in `AddFirebase(...)` |
| 7 | Application | Registra l'handler in `AddApplication()` |
| 8 | Api | Crea `WatchlistController` sottile che usa `ToActionResult` |

**Regola pratica**: si parte sempre dall'interno (Domain → Application) e si finisce con i dettagli (Infrastructure → Api). Se ti accorgi di partire dal controller o dal database, probabilmente stai invertendo le dipendenze.

---

## 12. Errori comuni

| Errore | Perché è un problema | Soluzione |
|---|---|---|
| Attributi `[FirestoreData]` sull'entità di dominio | Il Domain dipende da Google.Cloud.Firestore | Usa un modello di persistenza separato (`NoteDocument`) |
| Controller che chiama direttamente `FirestoreDb` o `HttpClient` | Logica non testabile, accoppiamento al framework | Passa sempre da handler e porte |
| Interfacce definite in Infrastructure | L'Application dovrebbe dipendere da Infrastructure | Le interfacce vivono in **Application** |
| DTO di Yahoo restituiti dal caso d'uso | Il formato di un fornitore esterno "trapela" fino al client | Mappa verso modelli di dominio/DTO propri |
| Eccezioni per errori attesi (es. titolo non trovato) | Flusso poco chiaro, costo prestazionale | Usa `Result<T>` |
| Cache dentro al controller | Duplicazione, difficile da testare | Usa il decorator in Infrastructure |
| Leggere l'`userId` dal body della richiesta | Un utente potrebbe accedere ai dati di altri | Usa `ICurrentUser` (dal token) |
| Layer "anemico" con handler vuoti che fanno solo passthrough | Complessità senza beneficio | Metti le regole di business nel Domain |

### Quando la Clean Architecture *non* serve

Per un CRUD minimale o un prototipo, 4 progetti possono essere eccessivi. Il costo (più file, più mapping) si ripaga quando: la logica di business cresce, i provider esterni possono cambiare, o serve una buona copertura di test. Puoi anche partire con una solution a un progetto e cartelle separate per layer, poi estrarre i progetti quando servono.

---

## 13. Checklist finale

**Struttura**
- [ ] `Domain` senza riferimenti a progetti o pacchetti esterni
- [ ] `Application` dipende solo da `Domain`
- [ ] `Infrastructure` implementa le interfacce di `Application`
- [ ] `Api` contiene solo controller, auth e composizione

**Codice**
- [ ] Le regole di business stanno nelle entità/value object
- [ ] Gli errori attesi usano `Result<T>`
- [ ] Modelli di persistenza e DTO esterni separati dal dominio
- [ ] Cache implementata come decorator

**Sicurezza**
- [ ] Chiave del service account fuori dal repository
- [ ] `[Authorize]` sui controller
- [ ] Filtro per `OwnerId` in ogni query utente
- [ ] Input validato (simbolo, lunghezze)

**Qualità**
- [ ] Test unitari su Domain e handler
- [ ] Architecture test in CI
- [ ] Indici compositi Firestore creati
- [ ] Avviso sull'uso di endpoint non ufficiali di Yahoo documentato nel README

---

## Riferimenti utili

- Firebase Admin .NET SDK: <https://firebase.google.com/docs/admin/setup>
- Firestore .NET client: <https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest>
- Verifica ID token Firebase: <https://firebase.google.com/docs/auth/admin/verify-id-tokens>
- Firebase Emulator Suite: <https://firebase.google.com/docs/emulator-suite>
- NetArchTest: <https://github.com/BenMorris/NetArchTest>
