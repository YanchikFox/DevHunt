# Integration Gateway — Dokumentacja Techniczna

## Spis treści

1. [Przegląd systemu](#1-przegląd-systemu)
2. [Architektura](#2-architektura)
3. [Stos technologiczny](#3-stos-technologiczny)
4. [Struktura projektu](#4-struktura-projektu)
5. [Konfiguracja i zmienne środowiskowe](#5-konfiguracja-i-zmienne-środowiskowe)
6. [Bootstrap aplikacji (index.js)](#6-bootstrap-aplikacji-indexjs)
7. [System OAuth 2.0](#7-system-oauth-20)
8. [System webhooków — odbiór i weryfikacja](#8-system-webhooków--odbiór-i-weryfikacja)
9. [Obsługa webhooków GitHub (webhookHandler)](#9-obsługa-webhooków-github-webhookhandler)
10. [Zarządzanie webhookami (webhookService)](#10-zarządzanie-webhookami-webhookservice)
11. [Serwis synchronizacji danych (syncService)](#11-serwis-synchronizacji-danych-syncservice)
12. [Dwukierunkowa integracja GitHub Issues](#12-dwukierunkowa-integracja-github-issues)
13. [Konsumer zdarzeń RabbitMQ (eventConsumer)](#13-konsumer-zdarzeń-rabbitmq-eventconsumer)
14. [Warstwa middleware](#14-warstwa-middleware)
15. [System throttlingu i backpressure](#15-system-throttlingu-i-backpressure)
16. [Metryki Prometheus](#16-metryki-prometheus)
17. [Telemetria OpenTelemetry](#17-telemetria-opentelemetry)
18. [Logowanie (Winston + OpenObserve)](#18-logowanie-winston--openobserve)
19. [Klient HTTP (axiosClient)](#19-klient-http-axiosclient)
20. [Bezpieczeństwo](#20-bezpieczeństwo)
21. [Docker](#21-docker)
22. [Endpointy API — pełna referencja](#22-endpointy-api--pełna-referencja)
23. [Przepływy danych — diagramy sekwencyjne](#23-przepływy-danych--diagramy-sekwencyjne)
24. [Obsługa błędów](#24-obsługa-błędów)
25. [Zależności i pakiety](#25-zależności-i-pakiety)

---

## 1. Przegląd systemu

**Integration Gateway** to mikroserwis Node.js odpowiedzialny za dwukierunkową integrację platformy DevHunt z zewnętrznymi systemami kontroli wersji — **GitHub** i **GitLab** (w tym instancji self-hosted).

### Główne obowiązki

| Funkcja | Opis |
|---------|------|
| **OAuth 2.0** | Autoryzacja użytkowników przez GitHub/GitLab OAuth — generowanie URL autoryzacji, wymiana kodu na token, pobieranie profilu użytkownika |
| **Webhook Inbound** | Odbiór i przetwarzanie webhooków z GitHub/GitLab (push, issues, pull_request, merge_request) z weryfikacją podpisów kryptograficznych |
| **Webhook Management** | Tworzenie i usuwanie webhooków na GitHub/GitLab za pośrednictwem ich API |
| **Data Sync** | Synchronizacja danych repozytoriów (branche, commity, issues, PR/MR, contributors) z Core API |
| **Task ↔ Issue Sync** | Dwukierunkowa synchronizacja: DevHunt Task → GitHub Issue i GitHub Issue → DevHunt Task |
| **Event Consumer** | Konsumpcja zdarzeń z RabbitMQ (project.*, task.*, showcase.*) i reagowanie na nie w kontekście integracji |
| **Observability** | Prometheus metryki, OpenTelemetry tracing, strukturyzowane logowanie do OpenObserve |

### Port

Serwis nasłuchuje na porcie **5002** (`PORT` env var, domyślnie `5002`).

### Komunikacja z innymi serwisami

```
┌─────────────┐     HTTP/REST      ┌──────────────────┐
│   Frontend   │ ←───────────────→ │ Integration GW   │
│   (Next.js)  │                   │   Port: 5002     │
└──────────────┘                   └───────┬──────────┘
                                           │
                    ┌──────────────────────┼───────────────────────┐
                    │                      │                       │
              ┌─────▼──────┐       ┌───────▼────────┐     ┌───────▼───────┐
              │  Core API  │       │    GitHub API   │     │  GitLab API   │
              │  :7002     │       │  api.github.com │     │  gitlab.com   │
              └────────────┘       └────────────────┘     └───────────────┘
                    │
              ┌─────▼──────┐       ┌────────────────┐     ┌───────────────┐
              │  RabbitMQ  │       │     Redis       │     │  OpenObserve  │
              │   :5672    │       │     :6379       │     │    :5080      │
              └────────────┘       └────────────────┘     └───────────────┘
```

---

## 2. Architektura

### Warstwy architektoniczne

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Express 5.1 (HTTP)                            │
├───────────────┬──────────────┬──────────────┬──────────────────────────┤
│ requestContext│ traceContext │ metricsMiddle│ rateLimit │ throttle     │
├───────────────┴──────────────┴──────────────┴──────────────────────────┤
│                       Routes Layer                                     │
│   /api/oauth/*          /api/webhooks/*         /api/sync              │
├─────────────────────────────────────────────────────────────────────────┤
│                      Services Layer                                    │
│  syncService  │ webhookHandler │ webhookService │ githubIssueService   │
├─────────────────────────────────────────────────────────────────────────┤
│                   Event Consumer (RabbitMQ)                             │
│  eventConsumer — 7 routing keys, concurrency=5, retry ×3               │
├─────────────────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                                 │
│  axiosClient │ logger (Winston) │ metrics (Prometheus) │ telemetry     │
└─────────────────────────────────────────────────────────────────────────┘
```

### Wzorce architektoniczne

| Wzorzec | Implementacja |
|---------|---------------|
| **Middleware Chain** | requestContext → traceContext → metrics → rateLimit → throttle → request logging |
| **Event-Driven** | RabbitMQ consumer z topic exchange i 7 routing keys |
| **Backpressure** | Shared throttle package z kolejkową obsługą nadmiarowego ruchu |
| **Circuit Breaker (lite)** | Retry z max 3 prób, potem drop + metryka |
| **Bot Loop Prevention** | Sprawdzanie `sender.login` i metadanych w body Issue przed przetwarzaniem webhooków |
| **Graceful Shutdown** | SIGTERM → zamknięcie eventConsumer → zamknięcie serwera HTTP → exit |

---

## 3. Stos technologiczny

| Kategoria | Technologia | Wersja |
|-----------|-------------|--------|
| Runtime | Node.js | >= 20 |
| Framework | Express | 5.1.0 |
| Moduły | ES Modules (`"type": "module"`) | — |
| HTTP Client | axios | 1.7.7 |
| Message Broker | amqplib | 0.10.9 |
| Cache/Rate Limiting | ioredis | 5.4.2 |
| Rate Limiter | rate-limiter-flexible | 5.0.4 |
| JWT | jsonwebtoken | 9.0.2 |
| OAuth | passport 0.7.0 + passport-github2 + passport-oauth2 | — |
| Logging | winston | 3.15.0 |
| Metrics | prom-client | 15.1.3 |
| Tracing | @opentelemetry/sdk-node + auto-instrumentations-node + exporter-trace-otlp-http | — |
| CORS | cors | 2.8.5 |
| Env Config | dotenv | 17.2.3 |
| Backpressure | Shared package `packages/throttle` | — |

---

## 4. Struktura projektu

```
integration-gateway/
├── package.json                    # Manifest — zależności, skrypty, Node >=20
├── Dockerfile                      # node:20-alpine, multi-stage build
├── README.md                       # Dokumentacja (RU)
└── src/
    ├── index.js                    # Bootstrap Express — middleware, routing, startup
    ├── metrics.js                  # Prometheus registry — 7 metryk + handler
    ├── telemetry.js                # OpenTelemetry SDK + OTLP exporter
    ├── config/
    │   └── oauth.js                # Konfiguracja OAuth GitHub/GitLab
    ├── middleware/
    │   ├── auth.js                 # JWT authenticateToken + requireRole
    │   ├── rateLimit.js            # Redis → RateLimiterRedis + fallback Memory
    │   ├── requestContext.js       # requestId (UUID) + child logger
    │   ├── throttle.js             # Wrapper nad packages/throttle
    │   └── traceContext.js         # W3C traceparent propagation
    ├── routes/
    │   ├── oauth.js                # 4 endpointy OAuth (authorize, callback)
    │   └── webhooks.js             # 3 endpointy webhooków (verify, github, gitlab)
    ├── services/
    │   ├── eventConsumer.js        # RabbitMQ consumer — 7 typów zdarzeń
    │   ├── githubIssueService.js   # CRUD GitHub Issues z DevHunt Tasks
    │   ├── syncService.js          # Synchronizacja danych GitHub/GitLab repos
    │   ├── webhookHandler.js       # Przetwarzanie payload'ów webhooków
    │   └── webhookService.js       # Tworzenie/usuwanie webhooków na GitHub/GitLab
    └── utils/
        ├── axiosClient.js          # Axios z interceptorami (metryki + traceparent)
        └── logger.js               # Winston + OpenObserve transport
```

### Pakiety wspólne

```
packages/
└── throttle/
    └── index.js                    # createThrottle() — backpressure middleware
```

Ten pakiet jest współdzielony między serwisami (Integration Gateway importuje go jako `../../packages/throttle/index.js`).

---

## 5. Konfiguracja i zmienne środowiskowe

### Zmienne wymagane (produkcja)

| Zmienna | Opis | Walidacja |
|---------|------|-----------|
| `GITHUB_WEBHOOK_SECRET` | Secret do weryfikacji podpisów webhooków GitHub | Min. 32 znaki (SEC-013 R2) |
| `GITLAB_WEBHOOK_SECRET` | Secret do weryfikacji tokenów webhooków GitLab | Min. 32 znaki (SEC-013 R2) |
| `JWT_SECRET` | Klucz do weryfikacji JWT tokenów | Min. 32 znaki (SEC-013 R2) |
| `GITHUB_CLIENT_ID` | OAuth Client ID GitHub | Wymagany w konfiguracji OAuth |
| `GITHUB_CLIENT_SECRET` | OAuth Client Secret GitHub | Wymagany w konfiguracji OAuth |

### Zmienne opcjonalne

| Zmienna | Domyślna wartość | Opis |
|---------|------------------|------|
| `PORT` | `5002` | Port serwera HTTP |
| `CORE_API_URL` | `http://core-api:5000` | URL Core API (komunikacja wewnętrzna) |
| `CORS_ORIGINS` | `http://localhost:3000` | Dozwolone originy CORS (rozdzielane przecinkami) |
| `RABBITMQ_URL` | `amqp://guest:guest@message-broker:5672` | URL brokera wiadomości |
| `RABBITMQ_ENABLED` | `true` | Włączenie/wyłączenie konsumera RabbitMQ |
| `REDIS_URL` | `redis://cache-service:6379` | URL Redis do rate limitera |
| `NODE_ENV` | `development` | Środowisko (`production` / `development`) |
| `INTERNAL_API_KEY` | `""` | Klucz API do komunikacji wewnętrznej z Core API |
| `INTEGRATION_WORKER_CONCURRENCY` | `5` | Liczba równoległych worker-ów RabbitMQ |
| `LOG_LEVEL` | `info` | Poziom logowania Winston |
| `GITLAB_CLIENT_ID` | — | OAuth Client ID GitLab |
| `GITLAB_CLIENT_SECRET` | — | OAuth Client Secret GitLab |
| `GITLAB_URL` | `https://gitlab.com` | URL GitLab (self-hosted) |
| `GITLAB_API_URL` | `https://gitlab.com/api/v4` | Git API URL GitLab |
| `OAUTH_REDIRECT_BASE` | `http://localhost:5002` | Bazowy URL dla redirect URI OAuth |
| `FRONTEND_URL` | `https://devhunt.app` | URL frontendu (w metadanych Issue) |
| `GITHUB_BOT_USERNAME` | `devhunt-bot` | Username bota GitHub do wykrywania pętli |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://openobserve:5080/api/default/v1/traces` | Endpoint OpenTelemetry |
| `OTEL_SERVICE_NAME` | `integration-gateway` | Nazwa serwisu w telemetrii |
| `ENVIRONMENT` | `development` | Środowisko (telemetria) |
| `OPENOBSERVE_LOG_URL` | `http://openobserve:5080/api/default/logs/_json` | Endpoint logów OpenObserve |
| `OPENOBSERVE_ROOT_USER` | `admin@devhunt.local` | Użytkownik OpenObserve |
| `OPENOBSERVE_ROOT_PASSWORD` | `ChangeMe123!` | Hasło OpenObserve |
| `OPENOBSERVE_LOG_ENABLED` | `false` | Włączenie transportu logów do OpenObserve |

### Walidacja bezpieczeństwa (SEC-013 R2)

W trybie `NODE_ENV=production` serwis **odmawia uruchomienia** jeśli:
- `GITHUB_WEBHOOK_SECRET` krótszy niż 32 znaki
- `GITLAB_WEBHOOK_SECRET` krótszy niż 32 znaki
- `JWT_SECRET` krótszy niż 32 znaki

```javascript
// index.js — fragment walidacji
if (process.env.NODE_ENV === "production") {
  const requiredSecrets = [
    { name: "GITHUB_WEBHOOK_SECRET", value: process.env.GITHUB_WEBHOOK_SECRET },
    { name: "GITLAB_WEBHOOK_SECRET", value: process.env.GITLAB_WEBHOOK_SECRET },
    { name: "JWT_SECRET", value: process.env.JWT_SECRET },
  ];
  for (const { name, value } of requiredSecrets) {
    if (!value || value.length < 32) {
      console.error(`FATAL: ${name} must be at least 32 characters`);
      process.exit(1);
    }
  }
}
```

---

## 6. Bootstrap aplikacji (index.js)

Plik `src/index.js` (530 linii) jest głównym punktem wejścia serwisu. Odpowiada za:

### 6.1 Inicjalizacja

1. **Import telemetrii** — `./telemetry.js` musi być zaimportowany **pierwszy** (instrumentuje HTTP/Express)
2. **Walidacja sekretów** — SEC-013 R2 w produkcji
3. **CORS** — konfiguracja z `CORS_ORIGINS`, produkcja odrzuca `*` i brak konfiguracji
4. **Body parser** — `express.json({ limit: "10mb" })` (rozmiar ustawiony na 10 MB dla dużych payload'ów webhooków)

### 6.2 Łańcuch middleware

```
requestContext → traceContext → metricsMiddleware → rateLimit → throttle → request logging
```

| Middleware | Funkcja |
|-----------|---------|
| `requestContext` | Generuje UUID `requestId`, tworzy child logger |
| `traceContext` | Propagacja W3C `traceparent`, generuje `traceId` + `spanId` |
| `metricsMiddleware` | Mierzy czas odpowiedzi HTTP (Prometheus histogram) |
| `rateLimit` | Redis-backed rate limiter (100 req/60s) |
| `throttle` | Backpressure — max 100 aktywnych, kolejka do 200 |
| Request logging | Loguje method, path, statusCode, duration |

### 6.3 Routing

| Prefix | Auth | Handler |
|--------|------|---------|
| `/api/oauth` | `authenticateToken` | `routes/oauth.js` |
| `/api/webhooks` | **Brak** (weryfikacja podpisu) | `routes/webhooks.js` |

### 6.4 Endpointy inline

Następujące endpointy są zdefiniowane bezpośrednio w `index.js`:

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/api/sync` | JWT | Synchronizacja danych integracji |
| `POST` | `/api/webhooks` | JWT | Tworzenie webhooków na GitHub/GitLab |
| `DELETE` | `/api/webhooks/:integrationId/:serviceType/:webhookId` | JWT | Usuwanie webhooków |
| `GET` | `/health` | — | Health check |
| `GET` | `/metrics` | — | Metryki Prometheus |
| `GET` | `/metrics/throttle` | — | Diagnostyka throttle |
| `GET` | `/` | — | Informacje o serwisie |

### 6.5 Startup

```javascript
server.listen(PORT, () => {
  logger.info(`Integration Gateway running on port ${PORT}`);
  // Uruchomienie konsumera RabbitMQ (jeśli RABBITMQ_ENABLED !== "false")
  startConsumer().catch(err => logger.error("Failed to start event consumer:", err));
});
```

### 6.6 Graceful Shutdown

Na sygnał `SIGTERM`:
1. Zatrzymanie konsumera zdarzeń (`shutdownConsumer()`)
2. Zamknięcie serwera HTTP (`server.close()`)
3. `process.exit(0)` po 10 sekundach timeout

---

## 7. System OAuth 2.0

### 7.1 Konfiguracja dostawców (`config/oauth.js`)

Funkcja `getOAuthConfig(provider)` zwraca konfigurację OAuth dla danego dostawcy:

#### GitHub

| Parametr | Wartość |
|----------|---------|
| `authUrl` | `https://github.com/login/oauth/authorize` |
| `tokenUrl` | `https://github.com/login/oauth/access_token` |
| `userInfoUrl` | `https://api.github.com/user` |
| `scope` | `read:user user:email repo` |
| `responseType` | `code` |

#### GitLab

| Parametr | Wartość |
|----------|---------|
| `authUrl` | `{GITLAB_URL}/oauth/authorize` |
| `tokenUrl` | `{GITLAB_URL}/oauth/token` |
| `userInfoUrl` | `{GITLAB_API_URL}/user` |
| `scope` | `read_user read_repository api` |
| `responseType` | `code` |

GitLab wspiera **self-hosted** instancje poprzez zmienne `GITLAB_URL` i `GITLAB_API_URL`.

### 7.2 Endpointy OAuth (`routes/oauth.js`)

Plik implementuje **4 endpointy** — 2 pary (nowy format Core API + legacy GET):

#### `POST /api/oauth/authorize`

Generuje URL autoryzacji OAuth (format zgodny z Core API).

**Request Body:**
```json
{
  "provider": "github",
  "projectId": 123,
  "redirectUri": "http://localhost:3000/callback"
}
```

**Logika:**
1. Walidacja `provider` (github/gitlab)
2. Pobranie konfiguracji OAuth (`getOAuthConfig`)
3. Wygenerowanie `state` = `projectId:timestamp`
4. Budowa URL z query params: `client_id`, `redirect_uri`, `scope`, `state`, `response_type`
5. Zwrot URL autoryzacji

**Response:**
```json
{
  "authorizationUrl": "https://github.com/login/oauth/authorize?client_id=..."
}
```

#### `GET /api/oauth/:provider/url`

Legacy endpoint — to samo co `POST /authorize`, ale z parametrami w query string.

#### `POST /api/oauth/callback`

Wymiana kodu autoryzacji na token dostępu + pobranie profilu użytkownika.

**Request Body:**
```json
{
  "provider": "github",
  "code": "abc123",
  "redirectUri": "http://localhost:3000/callback"
}
```

**Logika:**
1. Wymiana `code` na `access_token` via POST na `tokenUrl`
2. Pobranie profilu użytkownika via GET na `userInfoUrl`
3. Mapowanie danych użytkownika (id, login, email, name, avatar_url)

**Response:**
```json
{
  "accessToken": "gho_...",
  "refreshToken": null,
  "userInfo": {
    "id": 12345,
    "login": "username",
    "email": "user@example.com",
    "name": "Full Name",
    "avatar_url": "https://..."
  }
}
```

#### `GET /api/oauth/:provider/callback`

Legacy endpoint — to samo co `POST /callback`, z parametrami w query string.

### 7.3 Diagram przepływu OAuth

```
┌──────────┐    1. POST /authorize     ┌───────────────────┐
│  Frontend │ ──────────────────────── → │ Integration Gateway│
│           │ ← ─── authorizationUrl ── │                   │
│           │                           └───────────────────┘
│           │    2. Redirect
│           │ ──────────────────────── → ┌─────────────────┐
│           │                           │ GitHub/GitLab    │
│           │ ← ──── code + state ───── │                  │
│           │                           └─────────────────┘
│           │    3. POST /callback
│           │ ──────────────────────── → ┌───────────────────┐
│           │                           │ Integration Gateway│
│           │                           │   → exchange code  │
│           │                           │   → fetch userInfo │
│           │ ← ── token + userInfo ─── │                   │
└───────────┘                           └───────────────────┘
```

---

## 8. System webhooków — odbiór i weryfikacja

### 8.1 Weryfikacja podpisów (`routes/webhooks.js`)

Endpointy webhooków **nie wymagają JWT** — zamiast tego weryfikują podpisy kryptograficzne.

#### GitHub — HMAC SHA-256

```javascript
function verifyGitHubSignature(payload, signature, secret) {
  const hmac = crypto.createHmac("sha256", secret);
  hmac.update(typeof payload === "string" ? payload : JSON.stringify(payload));
  const expectedSignature = `sha256=${hmac.digest("hex")}`;
  return secureCompare(signature, expectedSignature);
}
```

| Element | Wartość |
|---------|---------|
| Nagłówek | `x-hub-signature-256` |
| Algorytm | HMAC SHA-256 |
| Format | `sha256=<hex_digest>` |
| Secret | `GITHUB_WEBHOOK_SECRET` lub per-integracja z `getWebhookSecret()` |

#### GitLab — porównanie tokenu

```javascript
function verifyGitLabSignature(token, secret) {
  return secureCompare(token, secret);
}
```

| Element | Wartość |
|---------|---------|
| Nagłówek | `x-gitlab-token` |
| Metoda | Bezpośrednie porównanie tokenu z secretem |
| Anti-timing | `crypto.timingSafeEqual()` (via `secureCompare()`) |

#### Funkcja `secureCompare()`

Ochrona przed atakami timing:

```javascript
function secureCompare(a, b) {
  if (!a || !b) return false;
  const bufA = Buffer.from(a);
  const bufB = Buffer.from(b);
  if (bufA.length !== bufB.length) return false;
  return crypto.timingSafeEqual(bufA, bufB);
}
```

### 8.2 Endpointy webhooków

#### `POST /api/webhooks/github`

1. Odczyt nagłówka `x-hub-signature-256`
2. Weryfikacja podpisu via `verifyGitHubSignature()`
3. Zapis metryki `recordWebhookVerify("github", "success"/"failure")`
4. Jeśli podpis prawidłowy — wywołanie `processGitHubWebhook(eventType, payload)`
5. **Zawsze zwraca 200** — nawet jeśli przetwarzanie się nie powiedzie (zapobiega retry'om GitHub)

#### `POST /api/webhooks/gitlab`

1. Odczyt nagłówka `x-gitlab-token`
2. Weryfikacja via `verifyGitLabSignature()`
3. Zapis metryki
4. **Aktualnie nie implementuje przetwarzania** — loguje odbiór i zwraca 200

#### `POST /api/webhooks/verify-signature`

Generyczny endpoint do ręcznej weryfikacji podpisu:

```json
{
  "provider": "github",
  "payload": { ... },
  "signature": "sha256=..."
}
```

### 8.3 Pobieranie secretu per integracja

Funkcja `getWebhookSecret(provider, headers)` próbuje pobrać per-integrację secret:

1. Odczyt `x-integration-id` z nagłówków
2. Jeśli jest — zapytanie do Core API: `GET /api/integrations/{id}`
3. Odczyt `config.WebhookSecret` lub `config.webhookSecret`
4. Fallback → globalna zmienna `GITHUB_WEBHOOK_SECRET` / `GITLAB_WEBHOOK_SECRET`

---

## 9. Obsługa webhooków GitHub (webhookHandler)

Plik `services/webhookHandler.js` (422 linie) przetwarza payload'y webhooków GitHub i synchronizuje zmiany z DevHunt.

### 9.1 Dispatch typów zdarzeń

```javascript
switch (eventType) {
  case "issues":     → handleIssuesEvent(payload)
  case "issue_comment": → handleIssueCommentEvent(payload)
  case "push":       → handlePushEvent(payload)
  case "pull_request": → handlePullRequestEvent(payload)
}
```

### 9.2 Bot Loop Prevention

Przed przetwarzaniem każdego webhooka sprawdzane jest czy zdarzenie zostało wywołane przez bota DevHunt:

```javascript
function isOwnBotAction(payload) {
  const sender = payload?.sender?.login;
  return (
    sender === BOT_USERNAME ||       // env: GITHUB_BOT_USERNAME
    sender.endsWith("[bot]") ||
    sender.toLowerCase().includes("devhunt")
  );
}
```

Dodatkowo, sprawdzane jest czy Issue zostało utworzone przez DevHunt:

```javascript
function isDevHuntCreatedIssue(issue) {
  const body = issue?.body || "";
  return body.includes("_Synced from [DevHunt]") || body.includes("_Task ID:");
}
```

### 9.3 Obsługa zdarzeń Issues

| Akcja | Handler | Endpoint Core API |
|-------|---------|-------------------|
| `opened` | `handleIssueOpened()` | `POST /api/tasks/from-github` |
| `closed` | `handleIssueClosed()` | `PATCH /api/tasks/{id}/complete-from-github` |
| `reopened` | `handleIssueReopened()` | `PATCH /api/tasks/{id}/reopen-from-github` |
| `edited` | `handleIssueEdited()` | `PATCH /api/tasks/{id}/update-from-github` |
| `assigned`/`unassigned` | `handleIssueAssignmentChanged()` | ⚠️ Nie zaimplementowane (TODO) |
| `labeled`/`unlabeled` | `handleIssueLabelsChanged()` | `PATCH /api/tasks/{id}/labels-from-github` |

#### Mapowanie priorytetów z etykiet GitHub

```javascript
const priorityLabels = {
  "priority: critical": "urgent",
  "priority: high":     "high",
  "priority: medium":   "medium",
  "priority: low":      "low",
  "urgent":             "urgent",
  "high priority":      "high",
  "low priority":       "low",
};
```

#### Ekstrakcja tagów

Wszystkie etykiety GitHub **oprócz** tych z prefiksami priorytetowymi (`priority:`, `urgent`, `high priority`, `low priority`) są konwertowane na tagi DevHunt (ciąg rozdzielony przecinkami).

#### Ekstrakcja referencji do zadań z commitów

```javascript
const patterns = [
  /(?:fix(?:es)?|close[sd]?|resolve[sd]?)\s+#(\d+)/gi,
  /task[:\s]([a-f0-9-]{36})/gi,
];
```

Rozpoznawane wzorce: `fixes #123`, `closes #456`, `resolves #789`, `task:uuid-here`.

### 9.4 Wyszukiwanie integracji i zadań

| Funkcja | Endpoint Core API |
|---------|-------------------|
| `findIntegrationByRepository(repoFullName)` | `GET /api/integrations/by-repository?repository=owner/repo` |
| `findTaskByGitHubIssue(projectId, gitHubIssueId)` | `GET /api/tasks/by-github-issue?projectId=X&gitHubIssueId=Y` |

### 9.5 Handlery push i pull_request

| Event | Status |
|-------|--------|
| `push` | Parsuje commity, szuka referencji do zadań → ⚠️ linkowanie nie zaimplementowane (TODO) |
| `pull_request` | ⚠️ Nie zaimplementowane (TODO) |
| `issue_comment` | ⚠️ Nie zaimplementowane (potencjalna integracja z AI) |

---

## 10. Zarządzanie webhookami (webhookService)

Plik `services/webhookService.js` obsługuje **tworzenie i usuwanie** webhooków na GitHub i GitLab za pośrednictwem ich API.

### 10.1 GitHub

#### `createGitHubWebhook(repository, webhookUrl, events, accessToken)`

1. Parsowanie `repository` → `owner/repo`
2. Generowanie losowego `webhookSecret` (32 bajty hex)
3. `POST https://api.github.com/repos/{owner}/{repo}/hooks`
4. Konfiguracja:
   - `content_type: "json"`
   - `insecure_ssl: "0"` (wymuszenie HTTPS)
   - Domyślne zdarzenia: `["push", "pull_request", "issues", "release"]`
5. Zwrot: `{ webhookId, webhookUrl, secret, events, createdAt }`

#### `deleteGitHubWebhook(repository, webhookId, accessToken)`

1. `DELETE https://api.github.com/repos/{owner}/{repo}/hooks/{webhookId}`
2. Status 404 traktowany jako sukces (webhook już usunięty)

### 10.2 GitLab

#### `createGitLabWebhook(projectId, webhookUrl, events, accessToken, gitlabUrl)`

1. Generowanie losowego `webhookSecret` (32 bajty hex)
2. `POST {gitlabUrl}/projects/{projectId}/hooks`
3. Mapowanie zdarzeń na flagi GitLab:
   - `push_events`, `issues_events`, `merge_requests_events`
   - `tag_push_events`, `note_events`, `job_events`, `pipeline_events`, `wiki_page_events`
4. `enable_ssl_verification: true`
5. `token` = wygenerowany secret

#### `deleteGitLabWebhook(projectId, webhookId, accessToken, gitlabUrl)`

1. `DELETE {gitlabUrl}/projects/{projectId}/hooks/{webhookId}`
2. Status 404 traktowany jako sukces

### 10.3 Tworzenie webhooka z index.js

Endpoint `POST /api/webhooks` w `index.js` koordynuje cały proces:

1. Przyjęcie parametrów (wspiera camelCase i PascalCase)
2. Pobranie access tokenu z Core API: `GET /api/integrations/{integrationId}/token`
3. Wywołanie `createGitHubWebhook()` lub `createGitLabWebhook()`
4. Aktualizacja konfiguracji integracji w Core API: `PUT /api/integrations/{integrationId}/webhook-config`

---

## 11. Serwis synchronizacji danych (syncService)

Plik `services/syncService.js` (391 linii) odpowiada za synchronizację danych repozytoriów z GitHub i GitLab.

### 11.1 Punkt wejścia

```javascript
export async function syncIntegrationData(integrationId, serviceType, config, accessToken)
```

Dispatcher na podstawie `serviceType`:
- `"github"` → `syncGitHubData()`
- `"gitlab"` → `syncGitLabData()`

### 11.2 Synchronizacja GitHub

Łączy się z GitHub API v3 (`https://api.github.com`) i pobiera 5 typów zasobów:

| Zasób | Endpoint GitHub API | Limity |
|-------|---------------------|--------|
| **Branches** | `GET /repos/{owner}/{repo}/branches` | Wszystkie (per_page=100) |
| **Commits** | `GET /repos/{owner}/{repo}/commits` | Ostatnie 30 |
| **Issues** | `GET /repos/{owner}/{repo}/issues` | Otwarte, max 50, wykluczając PR'y |
| **Pull Requests** | `GET /repos/{owner}/{repo}/pulls` | Otwarte, max 20 |
| **Contributors** | `GET /repos/{owner}/{repo}/contributors` | Max 30 |

#### Normalizacja danych

Każdy typ zasobu jest mapowany do znormalizowanego formatu:

```javascript
// Branches
{ name, protected }

// Commits
{ sha, message, author: { login, date }, url }

// Issues (filtrowane: !pull_request)  
{ number, title, state, labels: [name], created_at, updated_at, html_url }

// Pull Requests
{ number, title, state, head: { ref, sha }, base: { ref }, created_at, updated_at, html_url }

// Contributors
{ login, contributions, avatar_url }
```

#### Aktualizacja Core API

Po synchronizacji: `PATCH /api/integrations/{integrationId}/last-sync` z datą `LastSyncAt`.

### 11.3 Synchronizacja GitLab

Łączy się z GitLab API v4 (domyślnie `https://gitlab.com/api/v4`, konfigurowalne) i pobiera 5 typów zasobów:

| Zasób | Endpoint GitLab API | Limity |
|-------|---------------------|--------|
| **Branches** | `GET /projects/{id}/repository/branches` | Wszystkie (per_page=100) |
| **Commits** | `GET /projects/{id}/repository/commits` | Ostatnie 30 |
| **Issues** | `GET /projects/{id}/issues` | Otwarte, max 50 |
| **Merge Requests** | `GET /projects/{id}/merge_requests` | Otwarte, max 20 |
| **Members** | `GET /projects/{id}/members` | Max 30 |

#### Normalizacja GitLab

```javascript
// Branches
{ name, protected, merged, developers_can_push }

// Commits
{ id, short_id, title, author_name, created_at, web_url }

// Issues
{ iid, title, description, state, labels, created_at, updated_at, web_url }

// Merge Requests
{ iid, title, state, created_at, updated_at, web_url, source_branch, target_branch }

// Members (contributors)
{ username, name, access_level }
```

### 11.4 Punkt wejścia z index.js

Endpoint `POST /api/sync` koordynuje synchronizację:

1. Przyjęcie `integrationId`, `serviceType`, `config`, `accessToken`
2. Jeśli brak `config` — pobranie z Core API: `GET /api/integrations/{integrationId}`
3. Wywołanie `syncIntegrationData()`
4. Zwrot danych synchronizacji lub błędu

---

## 12. Dwukierunkowa integracja GitHub Issues

Plik `services/githubIssueService.js` (305 linii) implementuje pełny cykl CRUD operacji na GitHub Issues.

### 12.1 DevHunt Task → GitHub Issue

#### `createGitHubIssue(task, integration, accessToken)`

1. Odczyt `repository` z `integration.Config.repository`
2. Mapowanie priorytetów na etykiety GitHub:

| Priorytet DevHunt | Etykieta GitHub |
|-------------------|-----------------|
| `urgent` | `priority: critical` |
| `high` | `priority: high` |
| `medium` | `priority: medium` |
| `low` | `priority: low` |

3. Konwersja tagów na dodatkowe etykiety
4. Budowa body z opisem + metadanymi DevHunt:

```markdown
{task.Description}

---
_Synced from [DevHunt](https://devhunt.app)_
_Task ID: {task.Id}_
_Deadline: {task.Deadline}_
_Estimated: {task.EstimatedHours}h_
```

5. `POST https://api.github.com/repos/{owner}/{repo}/issues`
6. Aktualizacja zadania w Core API z danymi Issue: `PATCH /api/tasks/{taskId}/github-link`

#### `updateGitHubIssue(task, integration, accessToken)`

`PATCH https://api.github.com/repos/{owner}/{repo}/issues/{issueNumber}` — aktualizacja tytułu i body.

#### `closeGitHubIssue(task, integration, accessToken)`

`PATCH .../issues/{issueNumber}` z `{ state: "closed" }`.

#### `reopenGitHubIssue(task, integration, accessToken)`

`PATCH .../issues/{issueNumber}` z `{ state: "open" }`.

#### `assignGitHubIssue(task, integration, accessToken)`

`POST .../issues/{issueNumber}/assignees` z `{ assignees: [githubUsername] }`.

Wymaga `task.AssignedToUser.GithubUsername`.

### 12.2 GitHub Issue → DevHunt Task

Realizowane przez `webhookHandler.js` (sekcja 9). Kierunek odwrotny:

```
GitHub Issue opened  → POST /api/tasks/from-github    → DevHunt Task created
GitHub Issue closed  → PATCH /api/tasks/{id}/complete  → DevHunt Task completed
GitHub Issue reopened → PATCH /api/tasks/{id}/reopen   → DevHunt Task reopened
GitHub Issue edited  → PATCH /api/tasks/{id}/update    → DevHunt Task updated
```

### 12.3 Diagram dwukierunkowej synchronizacji

```
┌──────────────────┐                              ┌──────────────────┐
│   DevHunt Task   │                              │   GitHub Issue   │
│                  │                              │                  │
│  Created ────────┼── githubIssueService ───────→│   Created        │
│  Updated ────────┼── githubIssueService ───────→│   Updated        │
│  Completed ──────┼── githubIssueService ───────→│   Closed         │
│  Reopened ───────┼── githubIssueService ───────→│   Reopened       │
│  Assigned ───────┼── githubIssueService ───────→│   Assigned       │
│                  │                              │                  │
│  Created ←───────┼── webhookHandler ←──────────│   Opened         │
│  Completed ←─────┼── webhookHandler ←──────────│   Closed         │
│  Reopened ←──────┼── webhookHandler ←──────────│   Reopened       │
│  Updated ←───────┼── webhookHandler ←──────────│   Edited         │
│  Labels ←────────┼── webhookHandler ←──────────│   Labeled        │
│                  │                              │                  │
│  Bot Loop Prevent│   isOwnBotAction()          │   sender check   │
│  Metadata check  │   isDevHuntCreatedIssue()   │   body markers   │
└──────────────────┘                              └──────────────────┘
```

---

## 13. Konsumer zdarzeń RabbitMQ (eventConsumer)

Plik `services/eventConsumer.js` (582 linie) implementuje konsumer wiadomości z kolejki RabbitMQ.

### 13.1 Konfiguracja połączenia

| Parametr | Wartość |
|----------|---------|
| Exchange | `devhunt.events` |
| Typ exchange | `topic` (durable) |
| Kolejka | `devhunt.integrations` (durable) |
| Worker concurrency | `INTEGRATION_WORKER_CONCURRENCY` (domyślnie 5) |
| Max retry | 3 próby (nagłówek `x-retry-count`) |

### 13.2 Routing keys

```javascript
const ROUTING_KEYS = [
  "project.created",
  "project.updated",
  "project.completed",
  "task.created",
  "task.updated",
  "task.completed",
  "showcase.published",
];
```

### 13.3 Event handlers

| Routing Key | Handler | Działanie |
|-------------|---------|-----------|
| `project.created` | `handleProjectCreated()` | Pobranie integracji projektu → sync każdej aktywnej |
| `project.updated` | `handleProjectUpdated()` | Deleguje do `handleProjectCreated()` |
| `project.completed` | `handleProjectCompleted()` | Logowanie (zamknięcie milestone — TODO) |
| `task.created` | `handleTaskCreated()` | Tworzenie GitHub Issue via `createGitHubIssue()` |
| `task.updated` | `handleTaskUpdated()` | Logowanie (sync do GitHub — TODO) |
| `task.completed` | `handleTaskCompleted()` | Zamknięcie GitHub Issue via `closeGitHubIssue()` |
| `showcase.published` | `handleShowcasePublished()` | Logowanie (Slack/Discord — TODO) |

### 13.4 Handler `handleTaskCreated()`

```javascript
async function handleTaskCreated(taskId, data) {
  // 1. Sprawdzenie warunków
  if (!data?.ProjectId) return;                     // Brak ProjectId
  if (data?.GitHubIssueId) return;                  // Już powiązany z Issue

  // 2. Pobranie integracji projektu
  const integrations = await getProjectIntegrations(data.ProjectId);

  // 3. Dispatch po service type
  for (const integration of integrations) {
    if (!integration.IsActive) continue;
    const handler = TASK_CREATE_HANDLERS[serviceType]; // github, jira, trello
    if (handler) await handler(taskId, data, integration);
  }
}
```

Mapa handlerów:

```javascript
const TASK_CREATE_HANDLERS = {
  github: createTaskInGitHub,       // Implementacja pełna
  jira:   logUnsupportedService,    // Placeholder
  trello: logUnsupportedService,    // Placeholder
};
```

### 13.5 Wzorzec przetwarzania wiadomości

```
RabbitMQ → consume() → pendingMessages[] → scheduleNext()
                                              ↓
                                   ┌─── activeWorkers < CONCURRENCY? ───┐
                                   │ YES                                │ NO
                                   ▼                                    ▼
                             processMessage()                      czekaj na
                                   │                               zakończenie
                                   ├── parse JSON                  workera
                                   ├── processEvent()
                                   ├── ack()
                                   │   ↓ error?
                                   ├── retry < 3? → republish z x-retry-count++
                                   └── retry ≥ 3? → drop + recordEventResult("dropped")
```

### 13.6 Retry logika

```javascript
const retryCount = (msg.properties.headers?.["x-retry-count"] || 0);
if (retryCount < MAX_RETRIES) {
  // Republish z inkrementowanym x-retry-count
  channel.publish(EXCHANGE_NAME, routingKey, content, {
    headers: { "x-retry-count": retryCount + 1 }
  });
}
// Zawsze ack oryginał (nie nack — unikamy poison queue)
channel.ack(msg);
```

### 13.7 Auto-reconnect

| Zdarzenie | Opóźnienie reconnect |
|-----------|---------------------|
| Zamknięcie połączenia (`close` event) | 5 sekund |
| Błąd startowy | 10 sekund |

### 13.8 Komunikacja z Core API

| Funkcja | Endpoint | Cel |
|---------|----------|-----|
| `getProjectIntegrations(projectId)` | `GET /api/integrations/project/{projectId}` | Pobranie integracji projektu |
| `getIntegrationAccessToken(integrationId)` | `GET /api/integrations/{integrationId}/token` | Pobranie access tokenu |

Nagłówki wewnętrzne:
```javascript
{
  Authorization: `Bearer ${INTERNAL_API_KEY}`,
  "X-Service-Name": "integration-gateway"
}
```

---

## 14. Warstwa middleware

### 14.1 Autentykacja JWT (`middleware/auth.js`)

#### `authenticateToken(req, res, next)`

1. **Tryb deweloperski** (`NODE_ENV !== "production"` i brak `JWT_SECRET`):
   - Loguje ostrzeżenie: "JWT auth disabled in development mode"
   - Ustawia `req.user = { id: 0, email: "dev@devhunt.local", role: "admin" }`
   - Przepuszcza żądanie dalej

2. **Tryb produkcyjny**:
   - Odczyt nagłówka `Authorization: Bearer <token>`
   - Weryfikacja via `jwt.verify(token, JWT_SECRET, { issuer: "DevHunt.AuthService", algorithms: ["HS256"] })`
   - Ustawienie `req.user` z payload JWT
   - Błędy: 401 Unauthorized

#### `requireRole(...roles)`

```javascript
export function requireRole(...roles) {
  return (req, res, next) => {
    if (!req.user) return res.status(401).json({ ... });
    if (!roles.includes(req.user.role)) return res.status(403).json({ ... });
    next();
  };
}
```

### 14.2 Rate Limiter (`middleware/rateLimit.js`)

#### Architektura

```
┌────────────────┐     fallback     ┌──────────────────┐
│ RateLimiterRedis│ ──────────────→ │ RateLimiterMemory │
│  (primary)     │   przy błędzie   │   (insurance)     │
└────────────────┘     Redis        └──────────────────┘
```

#### Konfiguracja

| Parametr | Wartość |
|----------|---------|
| Key prefix | `integration_gateway_rl` |
| Punkty (limit) | 100 żądań |
| Okno czasowe | 60 sekund |
| Redis URL | `REDIS_URL` (domyślnie `redis://cache-service:6379`) |
| Fallback | RateLimiterMemory (te same parametry) |

#### Nagłówki odpowiedzi

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 87
Retry-After: 45          (tylko przy przekroczeniu limitu)
```

#### Odpowiedź przy przekroczeniu limitu (429)

```json
{
  "error": "Too Many Requests",
  "retryAfter": 45
}
```

### 14.3 Request Context (`middleware/requestContext.js`)

1. Generuje UUID `requestId` (lub używa `x-request-id` z nagłówka przychodzącego)
2. **Kasuje** nagłówek `x-user-id` z żądania (ochrona przed spoofingiem)
3. Ustawia `req.requestId`
4. Tworzy child logger Winston z kontekstem:
   ```javascript
   req.logger = logger.child({ requestId, userId: req.user?.id });
   ```

### 14.4 Trace Context (`middleware/traceContext.js`)

Implementacja propagacji W3C Trace Context:

1. Parsowanie nagłówka `traceparent` z żądania przychodzącego
2. Ekstrakcja `traceId` (lub generowanie nowego 32-znakowego hex)
3. Generowanie nowego `spanId` (16-znakowy hex)
4. Ustawienie `req.traceparent` = `00-{traceId}-{spanId}-01`
5. Ustawienie nagłówka odpowiedzi `traceparent`
6. Rozszerzenie child loggera o `traceId`

---

## 15. System throttlingu i backpressure

### 15.1 Architektura

Współdzielony pakiet `packages/throttle/index.js` (247 linii) implementuje middleware backpressure z monitorowaniem zasobów systemowych.

### 15.2 Konfiguracja (Integration Gateway)

```javascript
const { throttle, getMetrics: getThrottleMetrics } = createThrottle({
  logger,
  maxConcurrent: 100,      // Max równoległych żądań
  maxQueueSize: 200,        // Max rozmiar kolejki oczekujących
  cpuThreshold: 0.8,        // Próg CPU (80%)
  memoryThreshold: 0.9,     // Próg pamięci (90%)
  metricsRefreshMs: 2000,   // Odświeżanie metryk co 2s
});
```

### 15.3 Algorytm działania

```
Żądanie przychodzące
        │
        ▼
    System overloaded?  ────── TAK ────→ 503 { reason: "System overloaded", retryAfter: 5 }
        │ NIE
        ▼
    activeRequests < maxConcurrent?
        │ TAK                    │ NIE
        ▼                        ▼
    Przetwarzaj natychmiast   requestQueue.length >= maxQueueSize?
    (activeRequests++)            │ TAK              │ NIE
                                  ▼                  ▼
                              503 { reason:      Dodaj do kolejki
                              "Request queue     requestQueue.push()
                              full",             processQueue()
                              retryAfter: 10 }
```

### 15.4 Monitorowanie zasobów

Funkcja `sampleSystemMetrics()` wywoływana co `metricsRefreshMs` (2s):

```javascript
function getCpuLoad() {
  const cpus = os.cpus();
  // Kalkulacja: 1 - (idle / total) dla średniego CPU
}

function getMemoryUsage() {
  return (os.totalmem() - os.freemem()) / os.totalmem();
}
```

### 15.5 Timeout żądań w kolejce

Żądania oczekujące w kolejce mają timeout **30 sekund**:

```javascript
setTimeout(() => {
  if (!isFinished()) {
    logger.warn(`Request timeout for ${req.path}`);
    finish();
  }
}, 30_000);
```

### 15.6 Diagnostyka throttle

Endpoint `GET /metrics/throttle` zwraca:

```json
{
  "activeRequests": 42,
  "queueLength": 3,
  "maxConcurrentRequests": 100,
  "maxQueueSize": 200,
  "cpuLoad": 0.35,
  "memoryUsage": 0.62,
  "isOverloaded": false
}
```

---

## 16. Metryki Prometheus

Plik `src/metrics.js` definiuje 7 metryk z dedykowanym rejestrem Prometheus.

### 16.1 Lista metryk

| Metryka | Typ | Labels | Opis |
|---------|-----|--------|------|
| `integration_gateway_http_request_duration_seconds` | Histogram | method, route, status_code | Czas trwania żądań HTTP |
| `integration_gateway_http_requests_total` | Counter | method, route, status_code | Łączna liczba żądań HTTP |
| `integration_gateway_outgoing_http_duration_seconds` | Histogram | target, status_code | Czas żądań wychodzących (do GitHub/GitLab/Core API) |
| `integration_gateway_webhook_verify_total` | Counter | provider, result | Próby weryfikacji podpisów webhooków |
| `integration_gateway_rabbitmq_connection_state` | Gauge | — | Stan połączenia RabbitMQ (1=connected, 0=disconnected) |
| `integration_gateway_event_messages_total` | Counter | routing_key, status | Zdarzenia z RabbitMQ wg statusu |
| `integration_gateway_throttle_active_requests` | Gauge | — | Aktywne żądania w throttle |
| `integration_gateway_throttle_queue_length` | Gauge | — | Długość kolejki throttle |
| `integration_gateway_throttle_overloaded` | Gauge | — | Flaga przeciążenia systemu |

### 16.2 Buckety histogramów

**Żądania przychodzące:**
```javascript
buckets: [0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10]  // sekundy
```

**Żądania wychodzące:**
```javascript
buckets: [0.1, 0.25, 0.5, 1, 2, 5, 10]  // sekundy
```

### 16.3 Default labels

```javascript
register.setDefaultLabels({
  service: "integration-gateway",
});
```

### 16.4 Default metrics

Zbierane automatycznie z prefiksem `integration_gateway_`:
- Metryki procesu Node.js (CPU, memory, event loop, GC)
- Metryki V8 heap

### 16.5 Sanityzacja routes

```javascript
function sanitizeRoute(req) {
  if (req.route?.path) return req.route.path;
  if (req.baseUrl) return `${req.baseUrl}${req.path}`;
  return req.originalUrl?.split("?")[0] || "unknown";
}
```

Zapobiega eksplozji kardynalności metryk (np. `/api/webhooks/:id` zamiast `/api/webhooks/12345`).

### 16.6 Eksportowane funkcje

| Funkcja | Zastosowanie |
|---------|-------------|
| `metricsMiddleware(req, res, next)` | Middleware — mierzy czas odpowiedzi |
| `metricsHandler(req, res)` | Handler `GET /metrics` |
| `setRabbitConnectionState(isConnected)` | Aktualizacja gauge'a RabbitMQ |
| `recordEventResult(routingKey, status)` | Inkrementacja countera zdarzeń |
| `observeOutgoingHttp(target, statusCode, seconds)` | Rejestracja czasu żądań wychodzących |
| `recordWebhookVerify(provider, result)` | Rejestracja weryfikacji webhooków |

---

## 17. Telemetria OpenTelemetry

Plik `src/telemetry.js` konfiguruje OpenTelemetry SDK dla distributed tracing.

### 17.1 Konfiguracja

```javascript
const sdk = new NodeSDK({
  resource: resourceFromAttributes({
    "service.name": "integration-gateway",
    "service.namespace": "devhunt",
    "service.environment": process.env.ENVIRONMENT || "development",
  }),
  spanProcessor: new BatchSpanProcessor(traceExporter),
  instrumentations: [
    getNodeAutoInstrumentations({
      "@opentelemetry/instrumentation-http": {
        enabled: true,
        ignoreOutgoingPaths: [/\/metrics/],  // Ignoruj endpointy metryk
      },
      "@opentelemetry/instrumentation-express": {
        enabled: true,
      },
    }),
  ],
});
```

### 17.2 Exporter

| Parametr | Wartość |
|----------|---------|
| Typ | OTLP HTTP (`OTLPTraceExporter`) |
| Endpoint | `OTEL_EXPORTER_OTLP_ENDPOINT` (domyślnie `http://openobserve:5080/api/default/v1/traces`) |
| Processor | `BatchSpanProcessor` (batch → flush) |

### 17.3 Auto-instrumentacja

Automatycznie instrumentowane:
- **HTTP** — wszystkie żądania przychodzące i wychodzące (oprócz `/metrics`)
- **Express** — middleware i route handlers

### 17.4 Shutdown

Na sygnały `SIGTERM` i `SIGINT`:
```javascript
await sdk.shutdown();
```

**Uwaga:** Telemetria nie wywołuje `process.exit()` — to odpowiedzialność głównego modułu (`index.js`).

---

## 18. Logowanie (Winston + OpenObserve)

Plik `src/utils/logger.js` konfiguruje wielotransportowy system logowania.

### 18.1 Konfiguracja loggera

```javascript
export const logger = winston.createLogger({
  level: process.env.LOG_LEVEL || "info",
  format: winston.format.combine(
    winston.format.timestamp(),
    winston.format.errors({ stack: true }),
    winston.format.splat(),
    winston.format.json(),
  ),
  defaultMeta: { service: "integration-gateway" },
  transports: [
    new winston.transports.Console({ ... }),
    new OpenObserveTransport(),
  ],
});
```

### 18.2 Transporty

| Transport | Cel | Format |
|-----------|-----|--------|
| `Console` | stdout/stderr (Docker logs) | JSON ze znacznikiem czasu |
| `OpenObserveTransport` | Centralizowane logowanie | JSON via HTTP POST |

### 18.3 OpenObserve Transport

Niestandardowy transport Winston wysyłający logi do OpenObserve:

```javascript
class OpenObserveTransport extends winston.Transport {
  log(info, callback) {
    setImmediate(callback);  // Asynchroniczny — nie blokuje
    if (!this.enabled) return;

    axios.post(this.url, [{
      level: info.level,
      message: info.message,
      timestamp: info.timestamp,
      service: "integration-gateway",
      ...info,  // Wszystkie dodatkowe pola
    }], {
      headers: {
        Authorization: `Basic ${base64(user:pass)}`,
        "Content-Type": "application/json",
        "traceparent": info.traceparent,
      },
      timeout: 2000,
    }).catch(() => {});  // Fire & forget — nie blokujemy
  }
}
```

| Parametr | Wartość |
|----------|---------|
| URL | `OPENOBSERVE_LOG_URL` (domyślnie `http://openobserve:5080/api/default/logs/_json`) |
| Auth | Basic (base64 `user:pass`) |
| Timeout | 2000 ms |
| Włączenie | `OPENOBSERVE_LOG_ENABLED=true` |
| Propagacja trace | nagłówek `traceparent` |

### 18.4 Child logger

W middleware `requestContext` tworzony jest child logger z kontekstem:

```javascript
req.logger = logger.child({
  requestId: req.requestId,
  userId: req.user?.id,
  traceId: req.traceId,  // dodawany przez traceContext middleware
});
```

---

## 19. Klient HTTP (axiosClient)

Plik `src/utils/axiosClient.js` eksportuje skonfigurowaną instancję Axios z interceptorami.

### 19.1 Interceptory

#### Request interceptor — traceparent propagation

```javascript
client.interceptors.request.use((config) => {
  if (config.traceparent) {
    config.headers["traceparent"] = config.traceparent;
  }
  config.metadata = { startTime: Date.now() };
  return config;
});
```

#### Response interceptor — metryki czasu

```javascript
client.interceptors.response.use(
  (response) => {
    const duration = (Date.now() - config.metadata.startTime) / 1000;
    observeOutgoingHttp(target, status, duration);
    return response;
  },
  (error) => {
    // To samo — rejestracja metryki nawet przy błędzie
    observeOutgoingHttp(target, status, duration);
    return Promise.reject(error);
  }
);
```

### 19.2 Metryki

Każde żądanie wychodzące rejestruje histogram `integration_gateway_outgoing_http_duration_seconds` z labels:
- `target` — URL żądania
- `status_code` — kod HTTP odpowiedzi

---

## 20. Bezpieczeństwo

### 20.1 Podsumowanie mechanizmów bezpieczeństwa

| Mechanizm | Implementacja |
|-----------|---------------|
| **JWT Auth** | Weryfikacja z issuerem `DevHunt.AuthService`, algorytm HS256 |
| **Webhook Signatures** | HMAC SHA-256 (GitHub) / Token comparison (GitLab) |
| **Timing-Safe Compare** | `crypto.timingSafeEqual()` — ochrona przed timing attacks |
| **Bot Loop Prevention** | Sprawdzanie sender login + metadata w Issue body |
| **Rate Limiting** | Redis-backed, 100 req/60s z fallbackiem Memory |
| **Backpressure** | Throttle z kolejką, monitorem CPU/RAM, 503 przy przeciążeniu |
| **CORS** | Konfigurowalny, produkcja odrzuca `*` |
| **Secret Validation** | Min. 32 znaki w produkcji (GITHUB_WEBHOOK_SECRET, GITLAB_WEBHOOK_SECRET, JWT_SECRET) |
| **X-User-Id Stripping** | Middleware requestContext kasuje nagłówek `x-user-id` z żądań zewnętrznych |
| **SSL Enforcement** | `insecure_ssl: "0"` w konfiguracji webhooków GitHub |
| **Internal API Key** | Nagłówek `Authorization: Bearer {INTERNAL_API_KEY}` + `X-Service-Name` |
| **Body Size Limit** | `express.json({ limit: "10mb" })` |

### 20.2 Ochrona przed atakami

| Atak | Ochrona |
|------|---------|
| Webhook forgery | Weryfikacja podpisów kryptograficznych (HMAC SHA-256 / token) |
| Timing attack | `crypto.timingSafeEqual()` |
| Infinite loop (bot) | Sprawdzanie sender + metadata markers |
| DDoS | Rate limiting + throttle + backpressure |
| CSRF | CORS policy + JWT tokens |
| Request spoofing | X-User-Id stripping |
| Large payload | Body size limit 10 MB |
| Weak secrets | Minimum 32 znaków w produkcji |

---

## 21. Docker

### 21.1 Dockerfile

```dockerfile
FROM node:20-alpine
WORKDIR /app

# Kopiowanie pakietów wspólnych
COPY packages/ ./packages/

# Kopiowanie źródeł serwisu
COPY integration-gateway/package*.json ./
RUN npm install --omit=dev
COPY integration-gateway/ ./

EXPOSE 5002
USER node

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
  CMD node -e "require('http').get('http://localhost:5002/health', (r) => { process.exit(r.statusCode === 200 ? 0 : 1) })"

CMD ["node", "src/index.js"]
```

### 21.2 Konfiguracja w docker-compose

```yaml
integration-gateway:
  build:
    context: .
    dockerfile: integration-gateway/Dockerfile
  ports:
    - "5002:5002"
  environment:
    - CORE_API_URL=http://core-api:5000
    - RABBITMQ_URL=amqp://guest:guest@message-broker:5672
    - REDIS_URL=redis://cache-service:6379
  depends_on:
    - core-api
    - message-broker
    - cache-service
```

### 21.3 Health check

Endpoint `GET /health`:

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "supportedProviders": ["github", "gitlab"]
}
```

---

## 22. Endpointy API — pełna referencja

### 22.1 OAuth

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/api/oauth/authorize` | JWT | Generowanie URL autoryzacji OAuth |
| `GET` | `/api/oauth/:provider/url` | JWT | Generowanie URL autoryzacji (legacy) |
| `POST` | `/api/oauth/callback` | JWT | Wymiana kodu na token + profil |
| `GET` | `/api/oauth/:provider/callback` | JWT | Wymiana kodu na token (legacy) |

### 22.2 Webhooky — odbiór

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/api/webhooks/verify-signature` | — | Generyczna weryfikacja podpisu |
| `POST` | `/api/webhooks/github` | Signature | Odbiór webhooków GitHub |
| `POST` | `/api/webhooks/gitlab` | Token | Odbiór webhooków GitLab |

### 22.3 Webhooky — zarządzanie

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/api/webhooks` | JWT | Tworzenie webhooka na GitHub/GitLab |
| `DELETE` | `/api/webhooks/:integrationId/:serviceType/:webhookId` | JWT | Usuwanie webhooka |

### 22.4 Synchronizacja

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/api/sync` | JWT | Synchronizacja danych z repozytorium |

### 22.5 System

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/health` | — | Health check |
| `GET` | `/metrics` | — | Metryki Prometheus |
| `GET` | `/metrics/throttle` | — | Diagnostyka throttle |
| `GET` | `/` | — | Informacje o serwisie |

---

## 23. Przepływy danych — diagramy sekwencyjne

### 23.1 GitHub Webhook → DevHunt Task

```
GitHub ──── POST /api/webhooks/github ──→ Integration Gateway
                                              │
                                    [1] Weryfikacja HMAC SHA-256
                                    [2] isOwnBotAction()? → skip
                                    [3] isDevHuntCreatedIssue()? → skip
                                              │
                                    [4] findIntegrationByRepository()
                                              │
                              GET /api/integrations/by-repository
                                              ↓
                                          Core API
                                              │
                                    [5] handleIssueOpened()
                                              │
                              POST /api/tasks/from-github
                                              ↓
                                          Core API → DB
```

### 23.2 DevHunt Task.Created → GitHub Issue

```
Core API ── publish("task.created") ──→ RabbitMQ
                                           │
                        devhunt.integrations queue
                                           │
                                    eventConsumer
                                           │
                              [1] handleTaskCreated()
                              [2] getProjectIntegrations()
                              [3] getIntegrationAccessToken()
                                           │
                              [4] createGitHubIssue()
                                           │
                         POST /repos/{owner}/{repo}/issues
                                           ↓
                                       GitHub API
                                           │
                              [5] updateTaskWithIssueInfo()
                                           │
                         PATCH /api/tasks/{id}/github-link
                                           ↓
                                       Core API → DB
```

### 23.3 Synchronizacja danych

```
Frontend ── POST /api/sync ──→ Integration Gateway
                                      │
                            [1] authenticateToken()
                            [2] syncIntegrationData()
                                      │
                   ┌──────────────────┼──────────────────┐
                   ▼                                      ▼
           syncGitHubData()                       syncGitLabData()
                   │                                      │
           GET /repos/.../branches              GET /projects/.../branches
           GET /repos/.../commits               GET /projects/.../commits
           GET /repos/.../issues                GET /projects/.../issues
           GET /repos/.../pulls                 GET /projects/.../merge_requests
           GET /repos/.../contributors          GET /projects/.../members
                   │                                      │
                   └──────────────────┬───────────────────┘
                                      │
                   PATCH /api/integrations/{id}/last-sync
                                      ↓
                                  Core API → DB
```

---

## 24. Obsługa błędów

### 24.1 Wzorce obsługi błędów

| Kontekst | Strategia |
|----------|-----------|
| Webhooky GitHub | Zawsze zwracaj 200 (nawet przy błędzie) — zapobiega retry'om |
| Webhooky GitLab | To samo — 200 nawet przy failure |
| RabbitMQ events | Retry do 3 razy, potem drop z metryką |
| OAuth | Zwrot 500 z komunikatem błędu |
| Sync Service | Throw z komunikatem `"GitHub/GitLab API error: {status} - {message}"` |
| Core API calls | Logowanie błędu, zwrot `null` lub `[]` |
| Rate limit | 429 z nagłówkiem `Retry-After` |
| Throttle overload | 503 z `retryAfter: 5` |
| Queue full | 503 z `retryAfter: 10` |

### 24.2 Kody HTTP

| Kod | Kiedy |
|-----|-------|
| 200 | Sukces / webhook odebrany |
| 400 | Nieprawidłowe parametry / brak wymaganych pól |
| 401 | Brak lub nieprawidłowy JWT |
| 403 | Brak wymaganej roli |
| 404 | Webhook nie znaleziony (traktowany jako sukces przy usuwaniu) |
| 429 | Rate limit exceeded |
| 500 | Niespodziewany błąd serwera |
| 503 | System overloaded / queue full |

---

## 25. Zależności i pakiety

### 25.1 Zależności runtime (18)

| Pakiet | Wersja | Zastosowanie |
|--------|--------|-------------|
| `express` | 5.1.0 | Framework HTTP |
| `axios` | 1.7.7 | Klient HTTP (GitHub/GitLab/Core API) |
| `amqplib` | 0.10.9 | Klient RabbitMQ (AMQP) |
| `ioredis` | 5.4.2 | Klient Redis |
| `rate-limiter-flexible` | 5.0.4 | Rate limiting |
| `jsonwebtoken` | 9.0.2 | Weryfikacja JWT |
| `passport` | 0.7.0 | Middleware autentykacji (zadeklarowany) |
| `passport-github2` | 0.1.12 | Strategia GitHub OAuth |
| `passport-oauth2` | 1.8.0 | Generyczna strategia OAuth 2.0 |
| `cors` | 2.8.5 | CORS middleware |
| `dotenv` | 17.2.3 | Ładowanie zmiennych środowiskowych |
| `winston` | 3.15.0 | Logowanie strukturyzowane |
| `prom-client` | 15.1.3 | Metryki Prometheus |
| `@opentelemetry/sdk-node` | — | OpenTelemetry SDK |
| `@opentelemetry/auto-instrumentations-node` | — | Auto-instrumentacja HTTP/Express |
| `@opentelemetry/exporter-trace-otlp-http` | — | Eksport trace'ów via OTLP HTTP |
| `@opentelemetry/sdk-trace-base` | — | Bazowy SDK trace (BatchSpanProcessor) |
| `@opentelemetry/resources` | — | Atrybuty zasobów (service.name itd.) |

### 25.2 Zależności deweloperskie (1)

| Pakiet | Wersja | Zastosowanie |
|--------|--------|-------------|
| `@types/node` | ^20.0.0 | Definicje typów Node.js (dla IDE) |

### 25.3 Pakiety wspólne

| Pakiet | Ścieżka | Zastosowanie |
|--------|---------|-------------|
| `throttle` | `packages/throttle/index.js` | Middleware backpressure z monitorowaniem CPU/RAM |

### 25.4 Skrypty npm

```json
{
  "start": "node src/index.js",
  "dev": "node --watch src/index.js",
  "test": "node --test"
}
```

| Skrypt | Opis |
|--------|------|
| `start` | Uruchomienie produkcyjne |
| `dev` | Uruchomienie z hot reload (Node.js `--watch`) |
| `test` | Uruchomienie testów (Node.js wbudowany test runner) |

### 25.5 Wymagania runtime

```json
{
  "engines": {
    "node": ">=20"
  }
}
```

---

*Dokument wygenerowany na podstawie analizy kodu źródłowego Integration Gateway — DevHunt Platform.*
*Wersja: 1.0 | Data: 2025*
