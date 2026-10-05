# YAFT-Backend: documentazione API

Base URL in sviluppo: `http://localhost:5081`

- [Autenticazione](#autenticazione)
- [Formato delle risposte ed errori](#formato-delle-risposte-ed-errori)
- [Simboli](#simboli)
- [Titoli](#titoli)
  - [GET /api/stocks: lista titoli disponibili e ricerca](#get-apistocks)
  - [GET /api/stocks/quotes: quotazioni di titoli specifici](#get-apistocksquotes)
  - [GET /api/stocks/{symbol}: quotazione singola](#get-apistockssymbol)
  - [GET /api/stocks/{symbol}/history: storico prezzi](#get-apistockssymbolhistory)
- [Preferiti](#preferiti)
  - [GET /api/watchlist](#get-apiwatchlist)
  - [POST /api/watchlist](#post-apiwatchlist)
  - [DELETE /api/watchlist/{symbol}](#delete-apiwatchlistsymbol)
- [Cache](#cache)
- [Endpoint di servizio](#endpoint-di-servizio)

---

## Autenticazione

Tutti gli endpoint `/api/*` richiedono un **Firebase ID token** del progetto `myfinancetracker-b257e`:

```
Authorization: Bearer <Firebase ID token>
```

L'API verifica firma, issuer (`https://securetoken.google.com/myfinancetracker-b257e`), audience (`myfinancetracker-b257e`) e scadenza. L'utente è identificato dall'UID nel claim `user_id` (o `sub`): i preferiti letti e scritti sono sempre quelli di quell'utente.

Senza token, o con un token non valido o scaduto, la risposta è `401 Unauthorized`.

## Formato delle risposte ed errori

Le risposte sono JSON con proprietà in camelCase. Le date sono in formato ISO 8601 (UTC).

Gli errori seguono lo standard **Problem Details** (RFC 9457), con `Content-Type: application/problem+json`. Il campo `title` contiene un codice stabile, utile al client per riconoscere l'errore; `detail` contiene un messaggio leggibile in italiano.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "stock.not_found",
  "status": 404,
  "detail": "Titolo 'NOPE123' non trovato.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

| Status | Quando |
|---|---|
| 400 Bad Request | Parametri non validi |
| 401 Unauthorized | Token mancante, non valido o scaduto |
| 404 Not Found | Titolo inesistente su Yahoo, o non presente nei preferiti |
| 409 Conflict | Titolo già presente nei preferiti |
| 502 Bad Gateway | Yahoo Finance non raggiungibile o risposta non valida |
| 500 Internal Server Error | Errore inatteso (es. Firestore non configurato) |

### Codici di errore

| `title` | Status | Significato |
|---|---|---|
| `stock.symbol_invalid` | 400 | Simbolo con formato non valido |
| `stock.symbols_required` | 400 | `symbols` vuoto |
| `stock.too_many_symbols` | 400 | Più di 20 simboli |
| `stock.history_params_invalid` | 400 | `range` o `interval` non ammessi |
| `stock.list_invalid` | 400 | Valore di `list` non riconosciuto |
| `stock.region_invalid` | 400 | `region` non è un codice di due lettere |
| `stock.count_invalid` | 400 | `count` fuori da 1–100 |
| `stock.query_invalid` | 400 | Ricerca più lunga di 50 caratteri |
| `stock.not_found` | 404 | Titolo sconosciuto a Yahoo |
| `watchlist.not_found` | 404 | Titolo non presente nei preferiti |
| `watchlist.duplicate` | 409 | Titolo già nei preferiti |
| `stock.provider_unavailable` | 502 | Yahoo Finance non disponibile |
| `auth.required` | 401 | Utente non identificabile dal token |

## Simboli

I simboli sono quelli di Yahoo Finance, con il suffisso della borsa per i titoli non USA:

| Esempio | Strumento |
|---|---|
| `AAPL` | Apple (Nasdaq) |
| `ENI.MI` | Eni (Borsa di Milano) |
| `VWCE.DE` | ETF su Xetra |
| `^GSPC` | Indice S&P 500 (nell'URL: `%5EGSPC`) |
| `EURUSD=X` | Cambio EUR/USD |
| `BTC-USD` | Bitcoin in dollari |

Regole: 1–15 caratteri tra `A-Z`, `0-9`, `.`, `-`, `=`, `^`, con almeno una lettera o cifra. Maiuscole e minuscole sono equivalenti e gli spazi iniziali e finali vengono ignorati: `eni.mi` diventa `ENI.MI`.

---

## Titoli

### GET /api/stocks

Elenco dei titoli disponibili. Restituisce una **lista predefinita** oppure, se c'è `q`, i risultati di una **ricerca testuale**.

| Parametro | Tipo | Default | Descrizione |
|---|---|---|---|
| `list` | string | `most_actives` | `most_actives`, `day_gainers`, `day_losers`, `trending` |
| `q` | string | — | Testo da cercare (nome o simbolo, max 50 caratteri). Se presente, `list` e `region` vengono ignorati |
| `region` | string | `US` | Codice paese di due lettere (es. `US`, `IT`, `DE`) |
| `count` | int | `25` | Numero di risultati, 1–100 (la ricerca restituisce al massimo 25 risultati) |

Liste disponibili:

| `list` | Contenuto |
|---|---|
| `most_actives` | Titoli più scambiati della giornata |
| `day_gainers` | Maggiori rialzi della giornata |
| `day_losers` | Maggiori ribassi della giornata |
| `trending` | Titoli più cercati nella regione (solo simbolo: `name`, `price` e gli altri campi sono `null`) |

**Esempi**

```http
GET /api/stocks?list=day_gainers&count=3
GET /api/stocks?list=trending&region=IT
GET /api/stocks?q=eni
```

**Risposta 200**

```json
{
  "source": "day_gainers",
  "count": 2,
  "items": [
    {
      "symbol": "PTC",
      "name": "PTC Inc.",
      "exchange": "NasdaqGS",
      "type": "EQUITY",
      "price": 192.655,
      "changePercent": 33.76
    },
    {
      "symbol": "XP",
      "name": "XP Inc.",
      "exchange": "NasdaqGS",
      "type": "EQUITY",
      "price": 28.41,
      "changePercent": 32.15
    }
  ]
}
```

| Campo | Descrizione |
|---|---|
| `source` | Nome della lista, oppure `search` |
| `items[].type` | Tipo di strumento secondo Yahoo (`EQUITY`, `ETF`, `INDEX`, `CURRENCY`, `CRYPTOCURRENCY`, …) |
| `items[].price`, `items[].changePercent` | Presenti solo nelle liste `most_actives`, `day_gainers` e `day_losers` |

Errori: 400 (`stock.list_invalid`, `stock.region_invalid`, `stock.count_invalid`, `stock.query_invalid`), 502.

---

### GET /api/stocks/quotes

Quotazioni di più titoli specifici in una sola richiesta.

| Parametro | Tipo | Descrizione |
|---|---|---|
| `symbols` | string | Simboli separati da virgola, massimo 20. I duplicati vengono rimossi |

**Esempio**

```http
GET /api/stocks/quotes?symbols=AAPL,ENI.MI,NOPE123
```

**Risposta 200**

Un elemento per simbolo, nell'ordine richiesto. Se un titolo non esiste o Yahoo non risponde, quel solo elemento ha `quote: null` e un `error`: la richiesta nel suo complesso riesce comunque.

```json
[
  {
    "symbol": "AAPL",
    "quote": {
      "symbol": "AAPL",
      "name": "Apple Inc.",
      "currency": "USD",
      "exchange": "NasdaqGS",
      "price": 332.604,
      "previousClose": 330.1,
      "change": 2.504,
      "changePercent": 0.76,
      "dayHigh": 334.2,
      "dayLow": 329.5,
      "volume": 41250000,
      "marketTime": "2026-10-05T19:45:00+00:00"
    },
    "error": null
  },
  {
    "symbol": "NOPE123",
    "quote": null,
    "error": {
      "code": "stock.not_found",
      "description": "Titolo 'NOPE123' non trovato."
    }
  }
]
```

L'oggetto `quote` ha la stessa forma della [quotazione singola](#get-apistockssymbol).

Errori: 400 (`stock.symbols_required`, `stock.symbol_invalid`, `stock.too_many_symbols`).

---

### GET /api/stocks/{symbol}

Quotazione corrente di un titolo.

**Esempio**

```http
GET /api/stocks/ENI.MI
```

**Risposta 200**

```json
{
  "symbol": "ENI.MI",
  "name": "Eni S.p.A.",
  "currency": "EUR",
  "exchange": "Milan",
  "price": 24.41,
  "previousClose": 24.19,
  "change": 0.22,
  "changePercent": 0.91,
  "dayHigh": 24.505,
  "dayLow": 24.07,
  "volume": 5930233,
  "marketTime": "2026-10-05T15:36:55+00:00"
}
```

| Campo | Descrizione |
|---|---|
| `price` | Ultimo prezzo di mercato |
| `previousClose` | Chiusura della seduta precedente |
| `change` | `price - previousClose` |
| `changePercent` | Variazione percentuale, arrotondata a 2 decimali (`0` se `previousClose` è 0) |
| `dayHigh`, `dayLow`, `volume` | Massimo, minimo e volume della seduta; possono essere `null` |
| `marketTime` | Ora dell'ultimo prezzo (UTC); può essere `null` |

Errori: 400 (`stock.symbol_invalid`), 404 (`stock.not_found`), 502.

---

### GET /api/stocks/{symbol}/history

Storico dei prezzi (OHLCV).

| Parametro | Tipo | Default | Valori ammessi |
|---|---|---|---|
| `range` | string | `1mo` | `1d`, `5d`, `1mo`, `3mo`, `6mo`, `1y`, `2y`, `5y`, `10y`, `ytd`, `max` |
| `interval` | string | `1d` | `1m`, `5m`, `15m`, `30m`, `60m`, `1h`, `1d`, `1wk`, `1mo` |

Yahoo limita le combinazioni (ad esempio i dati intraday con `1m` sono disponibili solo per pochi giorni): una combinazione accettata dall'API ma non supportata da Yahoo può restituire 404 o un elenco vuoto.

**Esempio**

```http
GET /api/stocks/AAPL/history?range=5d&interval=1d
```

**Risposta 200**

```json
{
  "symbol": "AAPL",
  "currency": "USD",
  "range": "5d",
  "interval": "1d",
  "points": [
    {
      "timestamp": "2026-09-29T13:30:00+00:00",
      "open": 336.97,
      "high": 337.09,
      "low": 328.7,
      "close": 329.4,
      "volume": 38478000
    }
  ]
}
```

I prezzi sono arrotondati a 4 decimali. Un punto può avere campi `null` se Yahoo non ha dati per quell'istante (es. seduta in corso).

Errori: 400 (`stock.symbol_invalid`, `stock.history_params_invalid`), 404, 502.

---

## Preferiti

I preferiti appartengono all'utente del token e sono salvati in Firestore in `users/{uid}/watchlist/{symbol}`:

```json
{ "symbol": "ENI.MI", "addedAt": "<Timestamp>" }
```

### GET /api/watchlist

Preferiti dell'utente, in ordine di inserimento, ciascuno con la quotazione corrente.

**Risposta 200**

```json
[
  {
    "symbol": "ENI.MI",
    "addedAt": "2026-10-05T18:02:11.512+00:00",
    "quote": {
      "symbol": "ENI.MI",
      "name": "Eni S.p.A.",
      "currency": "EUR",
      "exchange": "Milan",
      "price": 24.41,
      "previousClose": 24.19,
      "change": 0.22,
      "changePercent": 0.91,
      "dayHigh": 24.505,
      "dayLow": 24.07,
      "volume": 5930233,
      "marketTime": "2026-10-05T15:36:55+00:00"
    }
  }
]
```

Se la quotazione di un titolo non è disponibile (Yahoo non risponde o il titolo è stato delistato), quell'elemento ha `quote: null`: la lista viene restituita comunque. Un utente senza preferiti riceve `[]`.

Errori: 401.

---

### POST /api/watchlist

Aggiunge un titolo ai preferiti. Il titolo deve esistere su Yahoo Finance.

**Body**

```json
{ "symbol": "ENI.MI" }
```

**Risposta 201 Created**

Header `Location: /api/watchlist/ENI.MI`. Il body ha la stessa forma di un elemento di [GET /api/watchlist](#get-apiwatchlist), con la quotazione corrente.

```json
{
  "symbol": "ENI.MI",
  "addedAt": "2026-10-05T18:02:11.512+00:00",
  "quote": { "symbol": "ENI.MI", "price": 24.41, "...": "..." }
}
```

Errori:

| Status | `title` | Quando |
|---|---|---|
| 400 | `stock.symbol_invalid` | Simbolo non valido |
| 404 | `stock.not_found` | Titolo inesistente su Yahoo (non viene salvato) |
| 409 | `watchlist.duplicate` | Titolo già nei preferiti |
| 502 | `stock.provider_unavailable` | Yahoo non raggiungibile: il titolo non viene salvato perché non si può verificare |

---

### DELETE /api/watchlist/{symbol}

Rimuove un titolo dai preferiti.

```http
DELETE /api/watchlist/ENI.MI
```

**Risposta 204 No Content**

Errori: 400 (`stock.symbol_invalid`), 404 (`watchlist.not_found`).

---

## Cache

Le interrogazioni a Yahoo passano da una cache in memoria condivisa da tutti gli utenti. Le richieste concorrenti per la stessa chiave producono una sola chiamata a Yahoo, e gli errori non vengono memorizzati.

| Dato | Durata predefinita | Chiave di configurazione |
|---|---|---|
| Quotazioni (anche nel batch e nei preferiti) | 30 s | `MarketDataCache:QuoteTtl` |
| Liste predefinite | 2 min | `MarketDataCache:ListTtl` |
| Storico | 5 min | `MarketDataCache:HistoryTtl` |
| Ricerche | 10 min | `MarketDataCache:SearchTtl` |

Di conseguenza una quotazione può essere vecchia al massimo quanto la sua durata in cache.

## Endpoint di servizio

Disponibili solo in ambiente Development, senza autenticazione:

| Percorso | Descrizione |
|---|---|
| `GET /openapi/v1.json` | Documento OpenAPI 3 (con schema di sicurezza Bearer) |
| `GET /health` | Stato dell'applicazione (`Healthy`) |
| `GET /alive` | Liveness |
