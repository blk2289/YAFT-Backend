# YAFT-Backend

API REST (ASP.NET Core, .NET 10) per consultare i titoli finanziari su **Yahoo Finance** e gestire i **titoli preferiti** di ogni utente.

- **Autenticazione:** Firebase Authentication. Ogni richiesta porta un Firebase ID token (`Authorization: Bearer <token>`) e l'API ricava l'utente dal token.
- **Persistenza:** i preferiti stanno su Firestore, nel progetto Firebase `myfinancetracker-b257e` (lo stesso di MyFinance), in `users/{uid}/watchlist/{symbol}`.
- **Dati di mercato:** endpoint pubblici non ufficiali di Yahoo Finance, con una cache in memoria (HybridCache) per le interrogazioni frequenti.
- **Orchestrazione:** .NET Aspire (AppHost + ServiceDefaults: telemetria, health check, resilienza HTTP).

La documentazione completa degli endpoint è in [docs/api.md](docs/api.md).

## Architettura

Il progetto segue la Clean Architecture: le dipendenze puntano solo verso l'interno.

```
Api (YAFT-Backend) ──► Application ──► Domain
        │                   ▲
        └──► Infrastructure ┘
```

| Progetto | Contenuto | Dipende da |
|---|---|---|
| `src/YAFT.Domain` | Entità e value object (`StockSymbol`, `StockQuote`, `WatchlistItem`…), regole di business | niente |
| `src/YAFT.Application` | Casi d'uso (handler), porte (`IMarketDataProvider`, `IWatchlistRepository`, `ICurrentUser`), `Result`, DTO | Domain |
| `src/YAFT.Infrastructure` | Client Yahoo Finance, repository Firestore, cache (decorator `CachedMarketDataProvider`) | Application |
| `YAFT-Backend` | Layer Api: controller, autenticazione JWT, `Program.cs` (composition root) | Application, Infrastructure |
| `YAFT-Backend.AppHost` | Avvio con .NET Aspire | Api |
| `YAFT-Backend.ServiceDefaults` | OpenTelemetry, health check, resilienza HTTP | — |
| `tests/*` | Test di dominio, casi d'uso, cache e regola della dipendenza (NetArchTest) | — |

La regola della dipendenza è verificata da `tests/YAFT.Architecture.Tests`: se un layer interno referenzia un layer esterno, i test falliscono.

Il manuale di riferimento per lo stile del codice è [docs/manuale-clean-architecture-firebase-yahoo.md](docs/manuale-clean-architecture-firebase-yahoo.md).

## Requisiti

- .NET SDK 10
- Un service account del progetto Firebase `myfinancetracker-b257e` con accesso a Firestore (serve solo per gli endpoint `/api/watchlist`)
- Opzionale: Docker, per avviare l'API in container

## Configurazione

`YAFT-Backend/appsettings.json`:

```json
{
  "Firebase": {
    "ProjectId": "myfinancetracker-b257e",
    "CredentialPath": ""
  },
  "MarketDataCache": {
    "QuoteTtl": "00:00:30",
    "HistoryTtl": "00:05:00",
    "SearchTtl": "00:10:00",
    "ListTtl": "00:02:00"
  }
}
```

| Chiave | Descrizione |
|---|---|
| `Firebase:ProjectId` | Progetto Firebase. Usato per validare il token (issuer e audience) e per Firestore |
| `Firebase:CredentialPath` | Percorso del JSON del service account. Se vuoto si usano le Application Default Credentials (variabile `GOOGLE_APPLICATION_CREDENTIALS` o identità del servizio su Google Cloud) |
| `MarketDataCache:*Ttl` | Durata in cache di quotazioni, storico, ricerche e liste |

Non salvare il file del service account nel repository. In sviluppo usa gli user-secrets:

```bash
dotnet user-secrets set "Firebase:CredentialPath" "C:\percorso\service-account.json" --project YAFT-Backend
```

Firestore viene inizializzato alla prima richiesta: gli endpoint `/api/stocks` funzionano anche senza credenziali.

## Avvio

Con Aspire (dashboard con log e tracce):

```bash
dotnet run --project YAFT-Backend.AppHost
```

Solo l'API:

```bash
dotnet run --project YAFT-Backend --launch-profile http
```

L'API ascolta su `http://localhost:5081`. In ambiente Development il documento OpenAPI è su `http://localhost:5081/openapi/v1.json`.

## Ottenere un token per le prove

Il token è il Firebase ID token di un utente del progetto. Due modi:

- da un client già autenticato (es. MyFinance), con `await auth.currentUser.getIdToken()`;
- via REST, con la Web API key del progetto e un utente email/password:

```bash
curl -X POST "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=<WEB_API_KEY>" \
  -H "Content-Type: application/json" \
  -d '{"email":"utente@example.com","password":"...","returnSecureToken":true}'
```

  Il campo `idToken` della risposta è il token (dura un'ora).

Il file [YAFT-Backend/YAFT-Backend.http](YAFT-Backend/YAFT-Backend.http) contiene una richiesta per ogni endpoint: incolla il token nella variabile `@token`.

## Test

```bash
dotnet test YAFT-Backend.slnx
```

## Docker

Il contesto di build è la cartella della solution:

```bash
docker build -f YAFT-Backend/Dockerfile -t yaft-backend .
docker run -p 8080:8080 \
  -e Firebase__CredentialPath=/secrets/sa.json \
  -v /percorso/service-account.json:/secrets/sa.json:ro \
  yaft-backend
```

## Aggiungere una funzionalità

1. **Domain:** nuove entità o regole, se servono.
2. **Application:** handler in una cartella per caso d'uso (es. `Stocks/GetQuote/`), con nuove porte in `Abstractions/` se serve un servizio esterno. Registra l'handler in `DependencyInjection.cs`.
3. **Infrastructure:** implementa le nuove porte (tipi `internal`) e registrale in `DependencyInjection.cs`.
4. **Api:** action nel controller, che chiama l'handler e converte il risultato con `ToActionResult`.
5. **Test:** handler con fake in `tests/YAFT.Application.Tests`.

## Limiti noti

- Yahoo Finance non ha un'API pubblica ufficiale: gli endpoint usati possono cambiare o essere limitati senza preavviso. Per cambiare provider basta una nuova implementazione di `IMarketDataProvider`.
- Le liste predefinite (`most_actives`, `day_gainers`, `day_losers`) sono centrate sul mercato USA. `trending` restituisce solo i simboli, senza nome né prezzo.
- La cache è in memoria: con più istanze dell'API ognuna ha la sua. Si può aggiungere Redis come secondo livello registrando un `IDistributedCache`, senza cambiare il codice.
- L'eliminazione dell'account in MyFinance non cancella la subcollection `users/{uid}/watchlist`.
