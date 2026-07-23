# DevHunt Core API — Dokumentacja Techniczna

> **Wersja**: 1.0  
> **Data aktualizacji**: 11 lutego 2026  
> **Technologia**: ASP.NET Core (.NET 10.0), Entity Framework Core 9, PostgreSQL 16  
> **Port domyślny**: 7002 (HTTP), Swagger: `/swagger`

---

## Spis treści

1. [Przegląd systemu](#1-przegląd-systemu)
2. [Architektura](#2-architektura)
3. [Stos technologiczny](#3-stos-technologiczny)
4. [Struktura projektu](#4-struktura-projektu)
5. [Konfiguracja i uruchomienie](#5-konfiguracja-i-uruchomienie)
6. [Pipeline middleware](#6-pipeline-middleware)
7. [Bezpieczeństwo](#7-bezpieczeństwo)
8. [Endpointy API](#8-endpointy-api)
   - 8.1 [Projekty](#81-projekty)
   - 8.2 [Cykl życia projektu](#82-cykl-życia-projektu)
   - 8.3 [Zespół projektu](#83-zespół-projektu)
   - 8.4 [Zadania (Tasks)](#84-zadania-tasks)
   - 8.5 [Kolumny i tablica zadań](#85-kolumny-i-tablica-zadań)
   - 8.6 [Powiązania zadań](#86-powiązania-zadań)
   - 8.7 [Załączniki zadań](#87-załączniki-zadań)
   - 8.8 [Ustawienia tablicy](#88-ustawienia-tablicy)
   - 8.9 [Pliki projektu](#89-pliki-projektu)
   - 8.10 [Dokumenty projektu](#810-dokumenty-projektu)
   - 8.11 [Artefakty projektu](#811-artefakty-projektu)
   - 8.12 [Aktualności projektu](#812-aktualności-projektu)
   - 8.13 [Zgłoszenia problemów (Issues)](#813-zgłoszenia-problemów-issues)
   - 8.14 [Metryki projektu](#814-metryki-projektu)
   - 8.15 [Umiejętności projektu](#815-umiejętności-projektu)
   - 8.16 [Subskrypcje projektu](#816-subskrypcje-projektu)
   - 8.17 [Showcase projektu](#817-showcase-projektu)
   - 8.18 [Użytkownicy](#818-użytkownicy)
   - 8.19 [Profil](#819-profil)
   - 8.20 [Czat](#820-czat)
   - 8.21 [Powiadomienia](#821-powiadomienia)
   - 8.22 [Feed (kanał aktywności)](#822-feed-kanał-aktywności)
   - 8.23 [Aktywności](#823-aktywności)
   - 8.24 [Rekomendacje AI](#824-rekomendacje-ai)
   - 8.25 [Planowanie AI](#825-planowanie-ai)
   - 8.26 [AI (legacy)](#826-ai-legacy)
   - 8.27 [Umiejętności (katalog)](#827-umiejętności-katalog)
   - 8.28 [Odznaki (badges)](#828-odznaki-badges)
   - 8.29 [Recenzje](#829-recenzje)
   - 8.30 [Integracje](#830-integracje)
   - 8.31 [Zaproszenia](#831-zaproszenia)
   - 8.32 [Moderacja](#832-moderacja)
   - 8.33 [Społeczność (feedback)](#833-społeczność-feedback)
   - 8.34 [Wsparcie (support)](#834-wsparcie-support)
   - 8.35 [Panel administracyjny](#835-panel-administracyjny)
   - 8.36 [Wsparcie administracyjne](#836-wsparcie-administracyjne)
   - 8.37 [Super Admin](#837-super-admin)
   - 8.38 [Metadane](#838-metadane)
   - 8.39 [Synchronizacja GitHub](#839-synchronizacja-github)
9. [Komunikacja w czasie rzeczywistym (SignalR)](#9-komunikacja-w-czasie-rzeczywistym-signalr)
10. [Serwisy aplikacyjne](#10-serwisy-aplikacyjne)
11. [Infrastruktura i zależności zewnętrzne](#11-infrastruktura-i-zależności-zewnętrzne)
12. [Obserwowalność](#12-obserwowalność)
13. [Health Checks](#13-health-checks)
14. [Wzorzec Outbox](#14-wzorzec-outbox)
15. [Konteneryzacja (Docker)](#15-konteneryzacja-docker)
16. [Format odpowiedzi API](#16-format-odpowiedzi-api)

---

## 1. Przegląd systemu

**DevHunt Core API** to główny mikroserwis systemu DevHunt odpowiedzialny za całą logikę biznesową platformy. Obsługuje zarządzanie projektami, zespołami, zadaniami, czatem, powiadomieniami, moderacją, rekomendacjami AI, systemem odznak i wieloma innymi funkcjami.

### Przypadki użycia (SRS v1.0)

| Kod | Opis |
|-----|------|
| UC-1 | Rejestracja i tworzenie profilu |
| UC-2 | Tworzenie projektu |
| UC-3 | Wyszukiwanie zespołu i rekrutacja członków |
| UC-4 | Współpraca projektowa |
| UC-5 | Publikacja i showcase projektu |
| UC-6 | Mentoring i interakcje firmowe |

---

## 2. Architektura

Core API działa jako centralny serwis w architekturze mikroserwisowej:

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│  Frontend   │────▶│ API Gateway  │────▶│  Core API   │
│ (Next.js)   │     │   (Nginx)    │     │ (.NET 10)   │
│   :3000     │     │  :80/443     │     │   :7002     │
└─────────────┘     └──────────────┘     └──────┬──────┘
                                                │
                    ┌───────────────────────────┼───────────────────────┐
                    │                           │                       │
             ┌──────▼──────┐           ┌────────▼───────┐     ┌────────▼──────┐
             │ PostgreSQL  │           │     Redis      │     │   RabbitMQ    │
             │   :5432     │           │    :6379       │     │    :5672      │
             └─────────────┘           └────────────────┘     └───────────────┘
                    │
    ┌───────────────┼──────────────────┐
    │               │                  │
┌───▼────────┐ ┌────▼──────────┐ ┌────▼──────────────┐
│ ML Service │ │ Integration   │ │ Notification Svc  │
│ (Python)   │ │ Gateway       │ │ (Node.js)         │
│  :8000     │ │ (Node.js)     │ │  :5003            │
└────────────┘ │  :5002        │ └───────────────────┘
               └───────────────┘
```

### Komunikacja z innymi serwisami

| Serwis | Protokół | Cel |
|--------|----------|-----|
| PostgreSQL | TCP/SQL | Główna baza danych (EF Core) |
| Redis | TCP | Cache, backplane SignalR, rate limiting |
| RabbitMQ | AMQP | Kolejka zdarzeń (Outbox Pattern) |
| ML Service | HTTP REST | Rekomendacje AI, analiza tech stacku |
| Integration Gateway | HTTP REST | OAuth, synchronizacja GitHub/GitLab |
| Notification Service | HTTP REST | Wysyłka powiadomień (email, push) |
| SeaweedFS | S3 API | Object storage (pliki, avatary) |
| OpenSearch | HTTP | Logowanie (Serilog sink) |

---

## 3. Stos technologiczny

| Warstwa | Technologia | Wersja |
|---------|-------------|--------|
| Framework | ASP.NET Core | .NET 10.0 |
| ORM | Entity Framework Core | 9.x |
| Baza danych | PostgreSQL | 16 |
| Cache | Redis (StackExchange.Redis) | 7 |
| Uwierzytelnianie | JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer) | — |
| WebSocket | SignalR + Redis backplane | — |
| Logowanie | Serilog (Console + OpenSearch sink) | — |
| Metryki | Prometheus (prometheus-net.AspNetCore) | — |
| Tracing | OpenTelemetry (OTLP exporter) | — |
| Rate Limiting | AspNetCoreRateLimit | — |
| Resilience | Polly (circuit breaker, retry) | — |
| Object Storage | AWS SDK S3 (SeaweedFS compatible) | — |
| Message Broker | RabbitMQ.Client | — |
| Hashing | BCrypt.Net-Next | — |
| Sanityzacja HTML | HtmlSanitizer | — |
| Dokumentacja API | Swashbuckle.AspNetCore (Swagger) | — |
| Kompresja | Brotli + Gzip | — |
| Sekrety | Azure Key Vault (produkcja) | — |

---

## 4. Struktura projektu

```
DevHunt.CoreApi/
├── Program.cs                     # Punkt wejścia aplikacji
├── DevHunt.CoreApi.csproj         # Definicja projektu
├── Dockerfile                     # Konteneryzacja
├── appsettings.json               # Konfiguracja bazowa
├── appsettings.Development.json   # Konfiguracja deweloperska
│
├── Controllers/                   # Kontrolery API (35 kontrolerów)
│   ├── ProjectsController.cs      # CRUD projektów
│   ├── UsersController.cs         # Operacje na użytkownikach
│   ├── TasksController.cs         # Zarządzanie zadaniami
│   ├── ChatController.cs          # System czatów
│   ├── NotificationsController.cs # Powiadomienia
│   ├── ProfileController.cs       # Profil użytkownika
│   ├── AdminController.cs         # Panel administracyjny
│   ├── AiPlansController.cs       # Planowanie AI
│   ├── IntegrationsController.cs  # Integracje GitHub/GitLab
│   ├── ... (31 dodatkowych)
│   └── BaseProjectController.cs   # Abstrakcyjna klasa bazowa
│
├── Services/                      # Serwisy biznesowe
│   ├── Ai/                        # Planowanie AI i strategie
│   ├── Badges/                    # System odznak/osiągnięć
│   ├── Chat/                      # Logika czatu
│   ├── Integrations/              # OAuth i autoryzacja integracji
│   ├── Profile/                   # Fasada profilu
│   ├── Projects/                  # Filtrowanie, zespół, news, issues
│   ├── Tasks/                     # Autoryzacja zadań
│   ├── Users/                     # Wyszukiwanie, fasada użytkowników
│   ├── CacheService.cs            # Abstrakcja cache (Redis)
│   ├── EventBusService.cs         # RabbitMQ publisher
│   ├── ObjectStorageService.cs    # S3-compatible storage
│   └── ... (inne serwisy)
│
├── Hubs/                          # SignalR Hubs
│   ├── ChatHub.cs                 # Czat w czasie rzeczywistym
│   └── NotificationHub.cs        # Powiadomienia push
│
├── Middleware/                    # Pipeline middleware
│   ├── CorrelationIdMiddleware.cs  # Identyfikator korelacji
│   ├── CsrfTokenMiddleware.cs    # Ochrona CSRF
│   ├── FileUploadValidationMiddleware.cs # Walidacja uploadu
│   ├── LogEnrichmentMiddleware.cs  # Wzbogacanie logów
│   ├── MetricsMiddleware.cs       # Metryki Prometheus
│   ├── SecurityHeadersMiddleware.cs # Nagłówki bezpieczeństwa
│   └── UserActiveCheckMiddleware.cs # Sprawdzanie aktywności konta
│
├── Security/                      # Serwisy bezpieczeństwa
│   ├── AuditService.cs           # Dziennik audytu
│   ├── EncryptionService.cs      # Szyfrowanie danych
│   ├── InternalServiceAuthenticator.cs # HMAC auth
│   ├── IpWhitelistAttribute.cs   # Biała lista IP
│   ├── SecurityHelpers.cs        # Pomocnicze funkcje
│   └── ValidationAttributes.cs   # Atrybuty walidacji
│
├── Extensions/                    # Metody rozszerzające
│   ├── AuthenticationExtensions.cs # JWT setup
│   ├── ConfigurationExtensions.cs # Azure Key Vault
│   ├── DatabaseExtensions.cs     # EF Core + Read/Write split
│   ├── InfrastructureExtensions.cs # HTTP Clients + Polly
│   ├── LoggingExtensions.cs      # Serilog
│   ├── OpenTelemetryExtensions.cs # OTEL
│   └── RateLimitingExtensions.cs # Rate limiting
│
├── Filters/                       # Filtry MVC
│   ├── ProfanityFilter.cs        # Filtr wulgaryzmów
│   └── ValidateCsrfAttribute.cs  # Walidacja CSRF
│
├── Models/                        # Enumeracje i modele
│   ├── ProjectStatus.cs
│   ├── ProjectVisibility.cs
│   ├── TaskPriority.cs
│   ├── TaskStatus.cs
│   └── ...
│
└── Properties/
    └── launchSettings.json        # Profile uruchomieniowe
```

---

## 5. Konfiguracja i uruchomienie

### 5.1 Wymagania

- .NET 10.0 SDK
- PostgreSQL 16
- Redis 7 (opcjonalnie w dev)
- RabbitMQ 3 (opcjonalnie w dev)
- SeaweedFS (opcjonalnie w dev)

### 5.2 Zmienne środowiskowe i ustawienia

Główne sekcje konfiguracji (`appsettings.json`):

| Sekcja | Opis | Klucz przykładowy |
|--------|------|--------------------|
| `ConnectionStrings` | Połączenia do baz danych | `DefaultConnection`, `ReadOnlyConnection`, `RedisConnection` |
| `Jwt` | Konfiguracja JWT | `Key`, `Issuer`, `Audience` |
| `Encryption` | Klucze szyfrowania | `Key` (32 bajtów), `IV` (16 bajtów) |
| `Serilog` | Konfiguracja logowania | `MinimumLevel`, `WriteTo` |
| `RabbitMQ` | Kolejka wiadomości | `ConnectionString`, `ExchangeName` |
| `ObjectStorage` | Storage S3 (SeaweedFS) | `Endpoint`, `AccessKey`, `SecretKey` |
| `MLService` | ML Service URL | `BaseUrl` (domyślnie: `http://ml-service:8000`) |
| `IntegrationGateway` | Integration Gateway URL | `BaseUrl` (domyślnie: `http://integration-gateway:5002`) |
| `NotificationService` | Notification Service URL | `BaseUrl` (domyślnie: `http://notification-service:5003`) |
| `Email` | Konfiguracja SMTP/SendGrid | `SmtpHost`, `SmtpPort`, `SendGridApiKey` |
| `Cors` | Dozwolone originy | `AllowedOrigins` |
| `Tracing` | OpenTelemetry | `AgentHost`, `AgentPort`, `ServiceName` |
| `Prometheus` | Endpoint metryczny | `Endpoint` (`/metrics`) |
| `SuperAdmin` | IP whitelist | `AllowedIPs` |
| `KeyVault` | Azure Key Vault URI | `Uri` |

### 5.3 Uruchomienie lokalne

```bash
# Z hot reload
dotnet watch run --project DevHunt.CoreApi/DevHunt.CoreApi.csproj

# Bez hot reload
dotnet run --project DevHunt.CoreApi/DevHunt.CoreApi.csproj

# Via Docker
docker-compose up -d --build core-api
```

### 5.4 Dostępne URL (dev)

| URL | Opis |
|-----|------|
| `http://localhost:7002` | Core API |
| `http://localhost:7002/swagger` | Swagger UI (tylko dev/staging) |
| `http://localhost:7002/health` | Health check |

---

## 6. Pipeline middleware

Kolejność middleware w potoku HTTP (od góry do dołu):

```
Żądanie HTTP
    │
    ▼
┌──────────────────────────┐
│  Response Compression     │  Brotli/Gzip kompresja
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  CORS                     │  DevCors / ProductionCors
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  CorrelationId            │  Generuje X-Correlation-ID
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  LogEnrichment            │  Wzbogaca logi o userId, traceId
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  Metrics (Prometheus)     │  Zlicza żądania, mierzy czas
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  FileUploadValidation     │  Walidacja typu/rozmiaru plików
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  Security Headers         │  HSTS, X-Frame-Options, CSP...
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  CSRF Token Generator     │  Double Submit Cookie pattern
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  Authentication           │  JWT Bearer validation
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  Authorization            │  Polityki autoryzacji
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  Rate Limiting            │  IP-based rate limiting (Redis)
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  UserActiveCheck          │  Sprawdza czy konto jest aktywne
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│  Routing → Controllers    │  Mapowanie do endpointów
└──────────────────────────┘
```

### Opis poszczególnych middleware

| Middleware | Opis |
|-----------|------|
| **ResponseCompression** | Kompresja odpowiedzi (Brotli/Gzip, poziom: Optimal). Obsługiwane typy MIME: JSON, XML, text, CSS, JS. |
| **CORS** | Dwie polityki: `DevCors` (permisywna, localhost origins) i `ProductionCors` (ściśle ograniczona, tylko skonfigurowane originy, konkretne nagłówki/metody). |
| **CorrelationId** | Dodaje nagłówek `X-Correlation-ID` do każdego żądania dla śledzenia w systemach rozproszonych. |
| **LogEnrichment** | Wzbogaca kontekst Serilog o `UserId`, `TraceId`, `SpanId`. |
| **Metrics** | Zbiera metryki Prometheus: liczba żądań, czas odpowiedzi, kody statusu. |
| **FileUploadValidation** | Walidacja przesyłanych plików: typ MIME, rozmiar, rozszerzenie. |
| **SecurityHeaders** | Dodaje nagłówki bezpieczeństwa: `X-Frame-Options`, `X-Content-Type-Options`, `X-XSS-Protection`, `Content-Security-Policy`, `Strict-Transport-Security`. |
| **CSRF Token** | Generuje tokeny CSRF (Double Submit Cookie pattern). Token w cookie `CSRF-TOKEN` + nagłówek `X-CSRF-TOKEN`. |
| **IpRateLimit** | Rate limiting na poziomie IP z backendem Redis dla środowisk rozproszonych. |
| **UserActiveCheck** | Sprawdza, czy uwierzytelniony użytkownik ma aktywne konto (nie jest zablokowany/zdeaktywowany). |

---

## 7. Bezpieczeństwo

### 7.1 Uwierzytelnianie

- **JWT Bearer tokens** — tokeny wydawane przez Auth Service (`DevHunt.AuthService`)
- **Issuer**: `DevHunt.AuthService`
- **Audience**: `DevHunt.CoreApi`
- **Klucz**: konfigurowany w `Jwt:Key` (minimum 32 znaki)

### 7.2 Autoryzacja

| Poziom | Mechanizm |
|--------|-----------|
| Anonimowy | `[AllowAnonymous]` — endpointy publiczne |
| Uwierzytelniony | `[Authorize]` — wymagany ważny JWT |
| Role | `[Authorize(Roles = "admin,curator")]` — weryfikacja roli |
| Polityka | `[Authorize(Policy = "AdminOrCurator")]` — niestandardowa polityka |
| Super Admin | `[Authorize]` + `[IpWhitelist]` + weryfikacja hasła w DB |

### 7.3 CSRF Protection (SEC-011)

- **Double Submit Cookie pattern**
- Token generowany per żądanie
- Cookie: `CSRF-TOKEN` (HttpOnly, SameSite=Lax)
- Nagłówek walidacyjny: `X-CSRF-TOKEN`
- **Globalny filtr**: `GlobalCsrfValidationFilter` — automatyczne sprawdzanie dla POST, PUT, DELETE, PATCH
- Endpoint: `GET /api/csrf-token` — pobieranie tokena dla aplikacji SPA

### 7.4 Rate Limiting

- Backend: Redis (rozproszone rate limiting)
- Konfiguracja per IP
- Middleware `IpRateLimitMiddleware` uruchomiony **przed** kontrolą aktywności konta

### 7.5 Walidacja danych wejściowych

| Parametr | Limit |
|----------|-------|
| Maksymalny rozmiar żądania | 10 MB |
| Maksymalny rozmiar pola formularza | 4 MB |
| Maksymalna liczba pól | 1000 |
| Maksymalny rozmiar kolekcji modelu | 100 elementów |
| Maksymalny rozmiar wiadomości SignalR | 8 KB |

### 7.6 Szyfrowanie

- **EncryptionService** — AES szyfrowanie dla wrażliwych danych (np. tokeny integracji)
- Klucz: 32 bajty, IV: 16 bajtów (konfiguracja w `Encryption:Key`, `Encryption:IV`)

### 7.7 Audyt

- **AuditService** — loguje wszystkie operacje wrażliwe
- Pola audytu: akcja, użytkownik, adres IP, data, szczegóły
- Dostępne przez `GET /api/superadmin/audit-logs`

### 7.8 Filtr wulgaryzmów

- **ProfanityFilter** — atrybut `[ProfanityFilter]` stosowany na kontrolerach tworzenia/edycji treści
- Sprawdza: nazwy projektów, opisy, wiadomości czatu, komentarze

### 7.9 Sanityzacja HTML

- **HtmlSanitizer** — biblioteka do usuwania niebezpiecznych tagów HTML
- Stosowany do treści generowanych przez użytkowników (opisy, dokumenty, komentarze)

### 7.10 Uwierzytelnianie międzyserwisowe

- **InternalServiceAuthenticator** — HMAC-based auth dla wewnętrznych wywołań
- Nagłówek: `X-Service-Name` + podpis HMAC
- Używane przez: Integration Gateway, Notification Service

---

## 8. Endpointy API

> **Konwencja routingu**: Wszystkie endpointy mają prefiks `/api/`  
> **Serializacja JSON**: camelCase dla nazw właściwości i kluczy słownikowych  
> **Paginacja**: parametry `page` i `pageSize` (domyślnie 20, max 100)

---

### 8.1 Projekty

**Route**: `api/projects`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/api/projects` | Anonim | Lista projektów z filtrowaniem i paginacją |
| `GET` | `/api/projects/{id}` | Anonim | Szczegóły projektu (sprawdzanie widoczności, cache) |
| `GET` | `/api/projects/{id}/permissions` | JWT | Uprawnienia użytkownika dla projektu |
| `POST` | `/api/projects` | JWT | Tworzenie projektu `[ProfanityFilter]` |
| `PUT` | `/api/projects/{id}` | JWT | Aktualizacja projektu `[ProfanityFilter]` |
| `PATCH` | `/api/projects/{id}/status` | JWT | Zmiana statusu projektu |
| `PATCH` | `/api/projects/{id}/visibility` | JWT | Zmiana widoczności |
| `PATCH` | `/api/projects/{id}/settings` | JWT | Aktualizacja ustawień |
| `DELETE` | `/api/projects/{id}` | JWT | Usunięcie projektu (tylko właściciel) |
| `POST` | `/api/projects/{id}/transfer-ownership` | JWT | Przeniesienie własności |

**Parametry filtrowania** (`GET /api/projects`):

| Parametr | Typ | Opis |
|----------|-----|------|
| `Status` | string | Filtr statusu (draft, recruiting, active, completed, cancelled, archived) |
| `Visibility` | string | Filtr widoczności (public, private, unlisted) |
| `Query` | string | Wyszukiwanie tekstowe |
| `Tech` | string | Filtr technologii |
| `Difficulty` | string | Poziom trudności |
| `Showcase` | bool | Tylko projekty z showcase |
| `Featured` | bool | Tylko wyróżnione projekty |
| `MyProjects` | bool | Tylko moje projekty |
| `OwnerId` | int | Po ID właściciela |
| `ExcludeDrafts` | bool | Wyklucz szkice |
| `IncludeCancelled` | bool | Uwzględnij anulowane |
| `Page` | int | Numer strony |
| `PageSize` | int | Rozmiar strony (domyślnie 20) |
| `SortBy` | string | Pole sortowania |
| `SortOrder` | string | Kierunek sortowania (asc/desc) |

---

### 8.2 Cykl życia projektu

**Route**: `api/projects`

Statusy projektu i dozwolone przejścia:

```
draft ──────▶ recruiting ──────▶ active ──────▶ completed
  │               │                │
  │               │                ├──────▶ cancelled
  │               ├────────────────┤
  │               │                │
  └───────────────┴────────────────┴──────▶ archived
```

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/api/projects/{id}/archive` | JWT | Archiwizacja (dowolny → archived, widoczność → private) |
| `POST` | `/api/projects/{id}/unarchive` | JWT | Przywrócenie z archiwum (archived → draft, widoczność → public) |
| `POST` | `/api/projects/{id}/publish` | JWT | Publikacja (draft → recruiting) |
| `POST` | `/api/projects/{id}/activate` | JWT | Aktywacja (recruiting → active; wymaga ≥2 aktywnych członków) |
| `POST` | `/api/projects/{id}/complete` | JWT | Zakończenie (active → completed) |
| `POST` | `/api/projects/{id}/cancel` | JWT | Anulowanie (dowolny oprócz archived/cancelled → cancelled; powiadomienia zespołu) |

---

### 8.3 Zespół projektu

**Route**: `api/projects/{projectId}/team`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/members` | Anonim | Lista członków zespołu |
| `PATCH` | `/members/{memberId}/permissions` | JWT | Aktualizacja uprawnień członka |
| `GET` | `/roles` | Anonim | Lista otwartych wakatów |
| `POST` | `/members` | JWT | Dołączenie do zespołu |
| `POST` | `/members/leave` | JWT | Opuszczenie zespołu |
| `DELETE` | `/members/{userId}` | JWT | Usunięcie członka (kick) |
| `POST` | `/roles` | JWT | Zmiana roli członka |
| `POST` | `/roles/transfer-leadership` | JWT | Przeniesienie przywództwa |

---

### 8.4 Zadania (Tasks)

**Route**: `api/projects/{projectId}/tasks`  
**Auth**: `[Authorize]` (wymagane dla wszystkich endpointów)

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Lista zadań projektu (opcjonalny filtr statusu/kolumny) |
| `POST` | `/` | Tworzenie zadania |
| `PUT` | `/{taskId}` | Aktualizacja zadania |
| `DELETE` | `/{taskId}` | Miękkie usunięcie (soft-delete) |
| `POST` | `/{taskId}/restore` | Przywracanie usuniętego zadania |
| `POST` | `/reorder` | Masowe przesortowanie (drag & drop) |

---

### 8.5 Kolumny i tablica zadań

**Route**: `api/projects/{projectId}/columns`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Lista kolumn z liczbą zadań |
| `POST` | `/` | Tworzenie kolumny (nazwa, kolor, limit WIP, pozycja) |
| `PUT` | `/{columnId}` | Aktualizacja (nazwa, kolor, limit WIP, isCompleted, koordynaty canvas) |
| `DELETE` | `/{columnId}` | Usunięcie kolumny (opcjonalny `moveTasksTo` — migracja zadań) |
| `POST` | `/reorder` | Zmiana kolejności kolumn |

---

### 8.6 Powiązania zadań

**Route**: `api/projects/{projectId}/tasks/{taskId}/links`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Lista powiązań (wychodzące + przychodzące) |
| `POST` | `/` | Tworzenie powiązania z detekcją cykli |
| `DELETE` | `/{linkId}` | Usunięcie powiązania (wraz z odwrotnym) |

**Typy powiązań**: `blocks`, `blocked_by`, `depends_on`, `related_to`, `duplicate_of`, `parent_of`, `child_of`

> Tworzenie powiązania automatycznie tworzy odwrotne powiązanie (np. `blocks` → `blocked_by`).

---

### 8.7 Załączniki zadań

**Route**: `api/projects/{projectId}/tasks/{taskId}/attachments`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Lista załączników zadania |
| `POST` | `/` | Przesyłanie pliku (limit 50 MB) |
| `POST` | `/link` | Linkowanie istniejącego pliku projektu |
| `GET` | `/{attachmentId}` | Pobieranie załącznika |
| `DELETE` | `/{attachmentId}` | Usunięcie załącznika |

---

### 8.8 Ustawienia tablicy

**Route**: `api/projects/{projectId}/board-settings`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Pobranie ustawień (auto-tworzenie domyślnych) |
| `PUT` | `/` | Aktualizacja ustawień (viewMode: board/canvas, zoom, pan, showCompletedTasks, defaultColumnId) |

---

### 8.9 Pliki projektu

**Route**: `api/projects/{projectId}/files` i `api/projects/{projectId}/gallery`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/{projectId}/files` | JWT | Lista plików (filtrowanie widoczności) |
| `GET` | `/{projectId}/gallery` | Anonim | Lista mediów (zdjęcia/wideo) |
| `POST` | `/{projectId}/files` | JWT | Przesyłanie pliku (limit 100 MB) |
| `GET` | `/{projectId}/files/{fileId}` | Anonim | Pobieranie pliku |
| `DELETE` | `/{projectId}/files/{fileId}` | JWT | Miękkie usunięcie pliku |

---

### 8.10 Dokumenty projektu

**Route**: `api/projects/{projectId}/docs`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/` | JWT | Lista dokumentów |
| `GET` | `/{docId}` | JWT | Pobranie dokumentu |
| `POST` | `/` | JWT | Tworzenie dokumentu |
| `PUT` | `/{docId}` | JWT | Aktualizacja dokumentu |
| `DELETE` | `/{docId}` | JWT | Miękkie usunięcie (autor lub właściciel) |

---

### 8.11 Artefakty projektu

**Route**: `api/projects/{projectId}/artifacts`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Wszystkie artefakty paszportu projektu |
| `GET` | `/{type}` | Artefakt po typie |
| `POST` | `/generate` | Generowanie paszportu projektu przez AI |
| `PUT` | `/{type}` | Upsert pojedynczego artefaktu |

---

### 8.12 Aktualności projektu

**Route**: `api/projects/{projectId}/news`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/` | Anonim | Lista aktualności |
| `GET` | `/{newsId}` | Anonim | Pojedynczy post |
| `POST` | `/` | JWT | Tworzenie postu |
| `PUT` | `/{newsId}` | JWT | Aktualizacja postu |
| `DELETE` | `/{newsId}` | JWT | Usuwanie postu |
| `POST` | `/{newsId}/like` | JWT | Przełączanie polubienia |
| `GET` | `/{newsId}/like` | Anonim | Status polubienia |
| `GET` | `/{newsId}/comments` | Anonim | Lista komentarzy |
| `POST` | `/{newsId}/comments` | JWT | Dodawanie komentarza |
| `PUT` | `/{newsId}/comments/{commentId}` | JWT | Edycja komentarza (okno 1h) |
| `DELETE` | `/{newsId}/comments/{commentId}` | JWT | Usuwanie komentarza |

---

### 8.13 Zgłoszenia problemów (Issues)

**Route**: `api/projects/{projectId}/issues`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `POST` | `/` | Tworzenie zgłoszenia (typy: technical, legal, conflict, abuse, violation, security, other) |
| `GET` | `/` | Lista zgłoszeń (członkowie lub admini) |
| `POST` | `/{issueId}/cancel` | Anulowanie zgłoszenia (tylko zgłaszający, status "open") |
| `PUT` | `/{issueId}` | Aktualizacja zgłoszenia |

---

### 8.14 Metryki projektu

**Route**: `api/projects/{projectId}/metrics`  
**Auth**: `[Authorize]` (członek/właściciel)

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Kompleksowe metryki (zadania, prędkość, burndown, wkłady, oś czasu) |
| `GET` | `/velocity` | Prędkość zespołu (zadania/tydzień, estymowane vs rzeczywiste godziny; konfigurowalny zakres 1-52 tygodni) |
| `GET` | `/contributions` | Wkład członków (completion rate, godziny per osoba) |

---

### 8.15 Umiejętności projektu

**Route**: `api/projects/{projectId}/skills`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/` | Anonim | Tech stack projektu |
| `POST` | `/` | JWT | Dodanie umiejętności (właściciel) |
| `PUT` | `/{projectSkillId}` | JWT | Aktualizacja właściwości |
| `DELETE` | `/{projectSkillId}` | JWT | Usunięcie umiejętności |

---

### 8.16 Subskrypcje projektu

**Route**: `api/projects/{projectId}/subscriptions`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `POST` | `/` | Subskrypcja projektu |
| `DELETE` | `/` | Anulowanie subskrypcji |

---

### 8.17 Showcase projektu

**Route**: `api/projects/{projectId}/showcase` i `api/showcase`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/{projectId}/showcase` | Anonim | Pobranie showcase projektu |
| `GET` | `/showcase` | Anonim | Lista wszystkich showcase'ów |
| `POST` | `/{projectId}/showcase` | JWT | Tworzenie showcase |
| `PUT` | `/{projectId}/showcase` | JWT | Aktualizacja showcase |
| `POST` | `/{projectId}/showcase/like` | JWT | Polubienie |
| `POST` | `/{projectId}/showcase/unlike` | JWT | Cofnięcie polubienia |
| `POST` | `/{projectId}/showcase/feature` | Admin | Wyróżnienie |
| `POST` | `/{projectId}/showcase/unfeature` | Admin | Cofnięcie wyróżnienia |
| `DELETE` | `/{projectId}/showcase` | JWT | Usunięcie showcase |
| `GET` | `/{projectId}/showcase/comments` | Anonim | Komentarze (wątkowane) |
| `POST` | `/{projectId}/showcase/comments` | JWT | Dodanie komentarza (z odpowiedziami) |
| `PUT` | `/{projectId}/showcase/comments/{commentId}` | JWT | Edycja komentarza |
| `DELETE` | `/{projectId}/showcase/comments/{commentId}` | JWT | Miękkie usunięcie komentarza |

---

### 8.18 Użytkownicy

**Route**: `api/users`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/` | Anonim | Wyszukiwanie użytkowników |
| `GET` | `/search` | Anonim | Wyszukiwanie (alias) |
| `GET` | `/{id}` | Anonim | Profil użytkownika |
| `POST` | `/{userId}/follow` | JWT | Obserwowanie użytkownika |
| `DELETE` | `/{userId}/follow` | JWT | Zaprzestanie obserwowania |
| `GET` | `/suggested` | JWT | Sugerowani użytkownicy |
| `GET` | `/{userId}/followers` | Anonim | Lista obserwujących |
| `GET` | `/{userId}/following` | Anonim | Lista obserwowanych |
| `GET` | `/me/settings` | JWT | Ustawienia prywatności |
| `PUT` | `/me/settings` | JWT | Aktualizacja ustawień prywatności |
| `GET` | `/{userId}/activities` | Anonim | Feed aktywności użytkownika |
| `GET` | `/{userId}/stats` | Anonim | Statystyki użytkownika (widoczność zależna od ustawień) |

---

### 8.19 Profil

**Route**: `api/profile`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/me` | JWT | Własny pełny profil (cache 10 min) |
| `GET` | `/{id}` | Anonim | Profil publiczny (cache 15 min) |
| `PUT` | `/me` | JWT | Aktualizacja profilu `[ProfanityFilter]` |
| `POST` | `/deactivate` | JWT | Dezaktywacja konta |
| `POST` | `/activate` | JWT | Reaktywacja konta |
| `POST` | `/avatar` | JWT | Przesyłanie avatara (limit 5 MB, typy graficzne) |
| `DELETE` | `/avatar` | JWT | Usunięcie avatara |
| `GET` | `/privacy` | JWT | Ustawienia prywatności |
| `PUT` | `/privacy` | JWT | Aktualizacja prywatności |

---

### 8.20 Czat

**Route**: `api/chat`  
**Auth**: `[Authorize]` (wszystkie endpointy)

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/conversations` | Lista konwersacji użytkownika |
| `POST` | `/conversations/direct/{otherUserId}` | Tworzenie/pobranie konwersacji bezpośredniej |
| `POST` | `/conversations/project/{projectId}` | Tworzenie/pobranie czatu grupowego projektu |
| `GET` | `/conversations/{id}/messages` | Wiadomości (z paginacją) |
| `POST` | `/conversations/{id}/messages` | Wysyłanie wiadomości `[ProfanityFilter]` |
| `PUT` | `/messages/{messageId}` | Edycja wiadomości `[ProfanityFilter]` |
| `DELETE` | `/messages/{messageId}` | Usunięcie wiadomości |
| `POST` | `/conversations/group` | Tworzenie czatu grupowego |
| `POST` | `/conversations/{id}/participants/{userId}` | Dodanie uczestnika |
| `DELETE` | `/conversations/{id}/participants/{userId}` | Usunięcie uczestnika |
| `POST` | `/conversations/{id}/leave` | Opuszczenie grupy |
| `PUT` | `/conversations/{id}/mute` | Wyciszenie/odciszenie |
| `GET` | `/conversations/{id}/participants` | Lista uczestników |

---

### 8.21 Powiadomienia

**Route**: `api/notifications`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/` | JWT | Powiadomienia użytkownika (paginacja, opcja `unreadOnly`) |
| `POST` | `/mark-read/{id}` | JWT | Oznaczenie jako przeczytane |
| `POST` | `/mark-all-read` | JWT | Oznaczenie wszystkich jako przeczytane |
| `POST` | `/` | Admin/Curator | Tworzenie powiadomienia + push SignalR |

---

### 8.22 Feed (kanał aktywności)

**Route**: `api/feed`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Spersonalizowany feed (focus na aktualności, wzbogacony o polubienia, komentarze, załączniki) |

---

### 8.23 Aktywności

**Route**: `api/activities`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/` | Anonim | Lista aktywności (filtry: projectId, eventType, visibility; paginacja) |

---

### 8.24 Rekomendacje AI

**Route**: `api/recommendations`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/me` | JWT | Spersonalizowane rekomendacje (opcjonalne odświeżenie z ML Service) |
| `GET` | `/project/{projectId}` | Admin/Curator | Odwrotne dopasowanie (użytkownicy do projektu) |
| `PUT` | `/{id}/view` | JWT | Oznaczenie jako wyświetlone |
| `PUT` | `/{id}/action` | JWT | Oznaczenie jako aktywne |
| `DELETE` | `/{id}` | JWT | Usunięcie rekomendacji |
| `POST` | `/refresh-all` | Admin | Masowe odświeżenie rekomendacji |
| `POST` | `/refresh/{userId}` | Admin/Curator | Odświeżenie per użytkownik |

---

### 8.25 Planowanie AI

**Route**: `api/projects/{projectId}/ai/plans`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `POST` | `/tech-stack` | Generowanie tech stacku przez AI |
| `POST` | `/` | Generowanie planu AI |
| `GET` | `/{planId}` | Pobranie planu |
| `POST` | `/{planId}/apply` | Zastosowanie planu |
| `POST` | `/refine` | Udoskonalenie planu |
| `POST` | `/tech-stack/refine` | Udoskonalenie tech stacku |
| `POST` | `/diagram` | Generowanie diagramu architektury |

---

### 8.26 AI (legacy)

**Route**: `api/ai`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `POST` | `/tech-stack` | Sugestie tech stacku z ML Service (legacy) |

---

### 8.27 Umiejętności (katalog)

**Route**: `api/skills`

**Katalog umiejętności:**

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/` | Anonim | Lista umiejętności (paginacja, kategoria) |
| `GET` | `/{id}` | Anonim | Umiejętność po ID |
| `GET` | `/search` | Anonim | Wyszukiwanie umiejętności |
| `GET` | `/suggest` | Anonim | Autouzupełnianie (alias-aware) |
| `GET` | `/categories` | Anonim | Kategorie umiejętności |
| `GET` | `/resolve` | Anonim | Rozwiązywanie nazw na ID (dopasowanie exact) |
| `POST` | `/` | Admin/Curator | Tworzenie umiejętności |
| `PUT` | `/{id}` | Admin/Curator | Aktualizacja |
| `DELETE` | `/{id}` | Admin/Curator | Usunięcie (blokowane jeśli w użyciu) |

**Umiejętności użytkownika:**

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/my` | JWT | Moje umiejętności |
| `POST` | `/my/{skillId}` | JWT | Dodanie umiejętności do profilu |
| `PUT` | `/my/{skillId}` | JWT | Aktualizacja biegłości/doświadczenia |
| `DELETE` | `/my/{skillId}` | JWT | Usunięcie z profilu |
| `GET` | `/user/{userId}` | Anonim | Umiejętności użytkownika (widok publiczny) |
| `PUT` | `/{skillId}/verify/{userId}` | Admin/Curator | Weryfikacja umiejętności |

---

### 8.28 Odznaki (badges)

**Route**: `api/badges`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/` | Anonim | Lista odznak (opcjonalna kategoria) |
| `GET` | `/{id}` | Anonim | Szczegóły odznaki |
| `GET` | `/me` | JWT | Moje odznaki z postępem |
| `GET` | `/user/{userId}` | Anonim | Odznaki użytkownika |
| `POST` | `/award/{userId}/{code}` | Admin | Przyznanie odznaki |
| `PUT` | `/progress/{userId}/{code}` | JWT | Aktualizacja postępu |
| `GET` | `/stats/{userId}` | Anonim | Statystyki odznak |
| `POST` | `/` | Admin | Tworzenie definicji odznaki |
| `PUT` | `/{id}` | Admin | Aktualizacja odznaki |
| `DELETE` | `/{id}` | Admin | Usunięcie odznaki |

---

### 8.29 Recenzje

**Route**: `api/reviews`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/project/{projectId}` | Anonim | Recenzje projektu (paginacja) |
| `POST` | `/` | JWT | Tworzenie recenzji (ocena 1-5, wymaga bycia członkiem zespołu) |
| `PUT` | `/{reviewId}` | JWT | Edycja recenzji (tylko autor, okno 24h) |
| `DELETE` | `/{reviewId}` | JWT | Usunięcie (autor lub admin/curator) |

---

### 8.30 Integracje

**Route**: `api/integrations`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `GET` | `/project/{projectId}` | JWT | Integracje projektu |
| `POST` | `/project/{projectId}` | JWT | Tworzenie integracji |
| `GET` | `/{integrationId}` | JWT | Szczegóły integracji |
| `GET` | `/{integrationId}/token` | Anonim/HMAC | Odszyfrowany token (uwierzytelnianie wewnętrzne) |
| `PUT` | `/{integrationId}` | JWT | Aktualizacja integracji |
| `PUT` | `/{integrationId}/toggle` | JWT | Włączenie/wyłączenie |
| `POST` | `/{integrationId}/sync` | JWT | Wymuszona synchronizacja przez gateway |
| `DELETE` | `/{integrationId}` | JWT | Usunięcie integracji |
| `GET` | `/oauth/{serviceType}/authorize` | JWT | URL autoryzacji OAuth |
| `GET` | `/oauth/{serviceType}/callback` | Anonim | Callback OAuth |
| `POST` | `/webhook/{integrationId}` | Anonim/HMAC | Webhook (walidacja HMAC) |
| `GET` | `/by-repository` | Anonim/HMAC | Wyszukiwanie integracji po repozytorium |

---

### 8.31 Zaproszenia

**Route**: `api/invitations`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `POST` | `/send` | Wysłanie zaproszenia lub prośby o dołączenie |
| `GET` | `/incoming` | Oczekujące zaproszenia przychodzące (paginacja) |
| `GET` | `/sent` | Oczekujące zaproszenia wysłane (paginacja) |
| `POST` | `/respond` | Akceptacja/odrzucenie zaproszenia |
| `DELETE` | `/{invitationId}` | Anulowanie zaproszenia (tylko nadawca) |

---

### 8.32 Moderacja

**Route**: `api/moderation`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/report` | JWT | Zgłoszenie moderacyjne (max 3 oczekujące per cel) |
| `GET` | `/queue` | Admin/Curator | Kolejka oczekujących zgłoszeń |
| `POST` | `/decision` | Admin/Curator | Rozpatrzenie zgłoszenia |

---

### 8.33 Społeczność (feedback)

**Route**: `api/community`

| Metoda | Ścieżka | Auth | Opis |
|--------|---------|------|------|
| `POST` | `/feedback` | JWT | Tworzenie feedbacku (bug/suggestion/feature/question) |
| `GET` | `/feedback` | Anonim | Lista feedbacków (sortowanie: votes/created/updated) |
| `GET` | `/feedback/{id}` | Anonim | Szczegóły z komentarzami |
| `POST` | `/feedback/{id}/vote` | JWT | Głosowanie (upvote/cancel) |
| `DELETE` | `/feedback/{id}/vote` | JWT | Usunięcie głosu |
| `POST` | `/feedback/{id}/comments` | JWT | Dodanie komentarza |
| `PUT` | `/feedback/{id}/status` | Admin/Curator | Zmiana statusu/priorytetu/przypisania |
| `PUT` | `/feedback/{id}` | JWT | Edycja (autor, status "open") |
| `DELETE` | `/feedback/{id}` | JWT | Usunięcie (autor lub admin) |
| `PUT` | `/feedback/{id}/comments/{cId}` | JWT | Edycja komentarza (autor, 1h) |
| `DELETE` | `/feedback/{id}/comments/{cId}` | JWT | Soft-delete komentarza |

---

### 8.34 Wsparcie (support)

**Route**: `api/support`  
**Auth**: `[Authorize]`

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `POST` | `/tickets` | Tworzenie ticketa (kategorie: question, bug, feature, billing, other) |
| `GET` | `/tickets` | Moje tickety (filtr statusu/kategorii, paginacja) |
| `GET` | `/tickets/{ticketId}` | Szczegóły ticketa z wiadomościami |
| `POST` | `/tickets/{ticketId}/messages` | Dodanie wiadomości (automatyczne zmiany statusu) |
| `PUT` | `/tickets/{ticketId}/close` | Zamknięcie ticketa (tylko autor) |
| `POST` | `/tickets/{ticketId}/reopen` | Ponowne otwarcie (closed/resolved → open) |
| `GET` | `/tickets/{ticketId}/history` | Historia zmian ticketa |

---

### 8.35 Panel administracyjny

**Route**: `api/admin`  
**Auth**: `[Authorize]` + walidacja roli admin/curator w DB

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `POST` | `/users/block` | Blokowanie użytkownika |
| `POST` | `/users/unblock` | Odblokowanie |
| `POST` | `/users/verify` | Weryfikacja użytkownika |
| `POST` | `/users/change-role` | Zmiana roli (admin; superadmin dla chronionych) |
| `POST` | `/projects/action` | Moderacja projektu (hide/archive/feature/unfeature) |
| `GET` | `/stats` | Statystyki dashboardu |
| `GET` | `/stats/extended` | Rozszerzone statystyki |
| `GET` | `/users` | Lista użytkowników z filtrami |
| `GET` | `/issues` | Lista zgłoszeń |
| `GET` | `/issues/{issueId}` | Szczegóły zgłoszenia |
| `POST` | `/issues/{issueId}/assign` | Przypisanie do admina |
| `POST` | `/issues/{issueId}/resolve` | Rozwiązanie (akcje: warn/suspend/remove/dismiss) |
| `PUT` | `/issues/{issueId}/escalate` | Eskalacja |

---

### 8.36 Wsparcie administracyjne

**Route**: `api/admin/support`  
**Auth**: `[Authorize]` + walidacja admin/curator

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/tickets` | Wszystkie tickety (filtry: status, priorytet, kategoria, assignee, wyszukiwanie, data) |
| `POST` | `/tickets/assign` | Przypisanie ticketa do admina |
| `POST` | `/tickets/resolve` | Rozwiązanie ticketa |
| `POST` | `/tickets/{id}/reassign` | Ponowne przypisanie |
| `PUT` | `/tickets/{id}/priority` | Zmiana priorytetu (low/medium/high/urgent) |
| `POST` | `/tickets/{id}/escalate` | Eskalacja do urgent (powiadomienie adminom + user) |
| `GET` | `/tickets/all` | Alternatywna lista z filtrami |
| `GET` | `/stats` | Statystyki wsparcia (per status, priorytet, kategoria, średni czas odpowiedzi) |

---

### 8.37 Super Admin

**Route**: `api/superadmin`  
**Auth**: `[Authorize]` + `[IpWhitelist]` + weryfikacja hasła per akcja

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/audit-logs` | Logi audytu (paginacja, filtrowanie: akcja, userId, severity) |
| `POST` | `/hard-delete/user` | Trwałe usunięcie użytkownika + powiązanych danych |
| `POST` | `/hard-delete/project` | Trwałe usunięcie projektu + powiązanych danych |
| `POST` | `/promote-admin` | Promocja do admina |
| `POST` | `/demote-admin` | Degradacja admina |
| `GET` | `/system-info` | Przegląd systemu (użytkownicy, projekty, ostatnie krytyczne akcje) |
| `GET` | `/admins` | Lista adminów/kuratorów/superadminów |
| `POST` | `/verify-password` | Pre-check hasła przed niebezpiecznymi operacjami |

> ⚠️ Wszystkie operacje mutujące wymagają potwierdzenia hasła w tele żądania.

---

### 8.38 Metadane

**Route**: `api/metadata`  
**Auth**: `[AllowAnonymous]` (wszystkie)

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `GET` | `/` | Indeks metadanych (dostępne sekcje/route'y) |
| `GET` | `/skills/categories` | Kategorie umiejętności |
| `GET` | `/badges/categories` | Kategorie odznak |
| `GET` | `/projects/statuses` | Statusy projektów |
| `GET` | `/projects/difficulties` | Poziomy trudności |
| `GET` | `/projects/visibilities` | Tryby widoczności |

---

### 8.39 Synchronizacja GitHub

**Route**: `api/internal/github-tasks`  
**Auth**: `[Authorize]` + `X-Service-Name` (wywołania wewnętrzne)

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| `PATCH` | `/projects/{projectId}/tasks/{taskId}/link` | Linkowanie zadania z GitHub Issue |
| `POST` | `/projects/{projectId}/tasks` | Tworzenie zadania z GitHub Issue (webhook: issue opened) |
| `PATCH` | `/projects/{projectId}/tasks/{taskId}/complete` | Ukończenie (webhook: issue closed) |
| `PATCH` | `/projects/{projectId}/tasks/{taskId}/reopen` | Ponowne otwarcie (webhook: issue reopened) |
| `PATCH` | `/projects/{projectId}/tasks/{taskId}/content` | Aktualizacja treści (webhook: issue edited) |
| `PATCH` | `/projects/{projectId}/tasks/{taskId}/labels` | Aktualizacja etykiet/priorytetu (webhook: labeled) |
| `GET` | `/projects/{projectId}/tasks/by-issue/{gitHubIssueId}` | Wyszukanie zadania po GitHub Issue ID |

---

## 9. Komunikacja w czasie rzeczywistym (SignalR)

System używa SignalR z Redis backplane do komunikacji WebSocket.

### 9.1 ChatHub

**Endpoint**: `/chatHub`

Hub do czatu w czasie rzeczywistym. Obsługuje:
- Wysyłanie/odbieranie wiadomości
- Powiadomienia o pisaniu (typing indicators)
- Dołączanie/opuszczanie pokojów konwersacji
- Aktualizacje statusu online

### 9.2 NotificationHub

**Endpoint**: `/notificationHub`

Hub do powiadomień push:
- Nowe powiadomienia w czasie rzeczywistym
- Aktualizacje licznika nieprzeczytanych
- Operacje mark-as-read

### Konfiguracja SignalR

| Parametr | Wartość | Opis |
|----------|---------|------|
| `EnableDetailedErrors` | `true` (dev) / `false` (prod) | Szczegóły błędów |
| `MaximumReceiveMessageSize` | 8192 (8 KB) | Ochrona przed DoS |
| Redis backplane | StackExchange.Redis | Synchronizacja między instancjami |

---

## 10. Serwisy aplikacyjne

### 10.1 Serwisy projektów

| Serwis | Interfejs | Opis |
|--------|-----------|------|
| `ProjectServicesFacade` | `IProjectServices` | Fasada łącząca serwisy projektu |
| `ProjectFilterService` | `IProjectFilterService` | Filtrowanie i wyszukiwanie projektów |
| `ProjectPermissionService` | `IProjectPermissionService` | Sprawdzanie uprawnień |
| `TechStackMatcher` | `ITechStackMatcher` | Dopasowanie tech stacku |
| `ProjectTeamService` | `IProjectTeamService` | Zarządzanie zespołem |
| `ProjectNewsService` | `IProjectNewsService` | Aktualności projektu |
| `ProjectIssuesService` | `IProjectIssuesService` | Zgłoszenia problemów |

### 10.2 Serwisy użytkowników

| Serwis | Interfejs | Opis |
|--------|-----------|------|
| `UserServicesFacade` | `IUserServices` | Fasada serwisów użytkownika |
| `UserProfileService` | `IUserProfileService` | Zarządzanie profilem |
| `UserFollowService` | `IUserFollowService` | System follow/unfollow |
| `UserActivityService` | `IUserActivityService` | Feed aktywności |
| `UserSearchService` | `IUserSearchService` | Wyszukiwanie użytkowników |
| `UserStatsService` | `IUserStatsService` | Statystyki użytkownika |

### 10.3 Serwisy AI

| Serwis | Interfejs | Opis |
|--------|-----------|------|
| `AiPlanningService` | `IAiPlanningService` | Orkiestracja planowania AI |
| `AiStrategyRouter` | `IAiStrategyRouter` | Routing do strategii AI |
| `V1AiPlannerStrategy` | `IAiPlannerStrategy` | Implementacja strategii planowania v1 |
| `AiPlanValidator` | `IAiPlanValidator` | Walidacja planów AI |
| `AiPlanApplier` | `IAiPlanApplier` | Aplikowanie planów AI |
| `AiEntitlementService` | `IAiEntitlementService` | Uprawnienia AI (domyślnie: AllowAll) |
| `AiCapabilityPolicy` | `IAiCapabilityPolicy` | Polityka dostępnych funkcji AI |
| `DbAiUsageMeter` | `IAiUsageMeter` | Pomiar użycia AI (zapis do DB) |

### 10.4 Serwisy infrastrukturalne

| Serwis | Interfejs | Opis |
|--------|-----------|------|
| `CacheService` | `ICacheService` | Abstrakcja Redis cache |
| `OutboxEventBusDecorator` | `IEventBusService` | Dekorator: zapis zdarzeń do Outbox |
| `RabbitMQEventBusService` | — | Publikacja do RabbitMQ |
| `OutboxEventService` | `IOutboxEventService` | Zarządzanie tabelą Outbox |
| `OutboxEventProcessorWorker` | `IHostedService` | Worker przetwarzający Outbox |
| `S3ObjectStorageService` | `IObjectStorageService` | Object storage (S3/SeaweedFS) |
| `PrometheusMetricsService` | `IMetricsService` | Metryki Prometheus |
| `EmailService` | `IEmailService` | Wysyłka email (SMTP/SendGrid) |
| `EmailTemplateService` | `IEmailTemplateService` | Szablony email |

### 10.5 Serwisy komunikacji między serwisami

| Serwis | Interfejs | Opis |
|--------|-----------|------|
| `MLServiceClient` | `IMLServiceClient` | Klient HTTP ML Service (+ Circuit Breaker) |
| `IntegrationGatewayClient` | `IIntegrationGatewayClient` | Klient HTTP Integration Gateway |
| `NotificationServiceClient` | `INotificationServiceClient` | Klient HTTP Notification Service |

### 10.6 Pozostałe serwisy

| Serwis | Interfejs | Opis |
|--------|-----------|------|
| `ChatService` | `IChatService` | Logika czatu |
| `BadgesService` | `IBadgesService` | System odznak |
| `AchievementTriggerService` | `IAchievementTriggerService` | Wyzwalanie osiągnięć |
| `ActivityLogService` | `IActivityLogService` | Dziennik aktywności |
| `NotificationHelperService` | `INotificationHelperService` | Pomocnik powiadomień |
| `AuditService` | `IAuditService` | Dziennik audytu |
| `EncryptionService` | `IEncryptionService` | Szyfrowanie AES |
| `ProfanityFilter` | — | Filtr wulgaryzmów |

---

## 11. Infrastruktura i zależności zewnętrzne

### 11.1 PostgreSQL

- **Połączenie główne** (`DefaultConnection`): Read/Write, pool 5-100
- **Połączenie tylko do odczytu** (`ReadOnlyConnection`): Read-only, pool 2-50
- **ORM**: Entity Framework Core 9
- **Migracje**: Zarządzane przez `DevHunt.DatabaseMigrator`
- **ReadWriteDbContextFactory**: Fabryka tworzenia kontekstu z rozdziałem R/W

### 11.2 Redis

- **Cache**: Buforowanie profili, projektów, uprawnień (TTL 10-15 min)
- **SignalR Backplane**: Synchronizacja hubów między instancjami
- **Rate Limiting**: Backend dla AspNetCoreRateLimit
- **Opcjonalny w dev**: Feature flag `Features:Redis:Enabled`

### 11.3 RabbitMQ

- **Exchange**: `devhunt.events` (topic)
- **Wzorzec Outbox**: Zdarzenia zapisywane do tabeli Outbox, następnie wysyłane do RabbitMQ przez worker
- **Feature flag**: `Features:EventBus:Enabled` — jeśli wyłączony, używa `NoOpEventBusService`

### 11.4 SeaweedFS (Object Storage)

- **Protokół**: S3-compatible API
- **Bucketty**: Inicjalizowane przy starcie (graceful degradation w razie błędu)
- **Użycie**: avatary, pliki projektów, załączniki zadań
- **Limity**: pliki do 100 MB, avatary do 5 MB, załączniki do 50 MB

### 11.5 HTTP Clients (Polly Resilience)

Wszyscy klienci HTTP korzystają z:
- **Circuit Breaker**: Automatyczne odcinanie przy wielu błędach
- **Retry z backoffem**: Ponawianie z exponential backoff
- Konfiguracja w `InfrastructureExtensions.AddResilientHttpClients()`

---

## 12. Obserwowalność

### 12.1 Logowanie (Serilog)

- **Sinki**: Console, OpenSearch (Elasticsearch-compatible)
- **Format indeksu**: `devhunt-logs-{yyyy.MM.dd}`
- **Wzbogacanie**: CorrelationId, UserId, TraceId, SpanId
- **Minimalny poziom**: Information (dev), filtry: Microsoft=Warning, System=Warning

### 12.2 Metryki (Prometheus)

- **Endpoint**: `GET /metrics`
- **Ochrona**: Dostęp tylko z localhost lub z tokenem `X-Metrics-Token`
- **Zbierane**: Liczba żądań, czas odpowiedzi, kody statusu, metryki biznesowe
- **Middleware**: `MetricsMiddleware` — automatyczne zliczanie

### 12.3 Tracing (OpenTelemetry)

- **Protokół eksportu**: OTLP (OpenTelemetry Protocol)
- **Cel**: OpenObserve (`tracing-service:4317`)
- **Instrumentacja**: ASP.NET Core, HTTP Client
- **Nazwa serwisu**: `DevHunt.CoreApi`

---

## 13. Health Checks

**Endpoint**: `GET /health` (AllowAnonymous)

| Sprawdzenie | Typ | Opis |
|------------|-----|------|
| PostgreSQL | `AddNpgSql` | Połączenie do bazy danych |
| Redis | `AddRedis` | Połączenie do Redis (tylko gdy włączone) |

Odpowiedzi:
- `200 OK` — `Healthy`
- `503 Service Unavailable` — `Unhealthy`

---

## 14. Wzorzec Outbox

System implementuje **Transactional Outbox Pattern** (REL-002) dla niezawodnej dostawy zdarzeń:

```
┌────────────────┐     ┌─────────────────┐     ┌──────────────┐
│  Kontroler/    │     │   Tabela DB     │     │              │
│  Serwis        │────▶│   OutboxEvent   │────▶│   RabbitMQ   │
│                │     │                 │     │              │
│ Zapis + Event  │     │ Oczekujące      │     │  Publikacja  │
│ w jednej       │     │ zdarzenia       │     │  zdarzeń     │
│ transakcji     │     │                 │     │              │
└────────────────┘     └───────┬─────────┘     └──────────────┘
                               │
                    ┌──────────▼──────────┐
                    │ OutboxEventProcessor │
                    │ Worker (Background)  │
                    │ Cykliczna publikacja  │
                    └─────────────────────┘
```

**Przepływ**:
1. Kontroler/serwis wywołuje `IEventBusService.PublishAsync()`
2. `OutboxEventBusDecorator` zapisuje zdarzenie do tabeli `OutboxEvent` w tej samej transakcji co dane biznesowe
3. `OutboxEventProcessorWorker` (hosted service) cyklicznie odczytuje oczekujące zdarzenia
4. Worker publikuje do RabbitMQ i oznacza jako przetworzone

**Gwarancje**: At-least-once delivery (przynajmniej jednokrotna dostawa).

---

## 15. Konteneryzacja (Docker)

### Dockerfile (multi-stage build)

```
Etap 1: Build (mcr.microsoft.com/dotnet/sdk:10.0)
├── Kopiowanie .csproj + Directory.Build.props
├── dotnet restore (cache NuGet)
├── Kopiowanie kodu źródłowego
└── dotnet publish -c Release

Etap 2: Runtime (mcr.microsoft.com/dotnet/aspnet:10.0)
├── Użytkownik non-root (appuser) — SEC-015
├── curl zainstalowany (health checks)
└── ENTRYPOINT: dotnet DevHunt.CoreApi.dll
```

### Bezpieczeństwo kontenerowe

- ✅ Użytkownik non-root (`appuser`)
- ✅ Minimalne zależności (`--no-install-recommends`)
- ✅ Czyszczenie apt cache
- ✅ Multi-stage build (obraz runtime bez SDK)

---

## 16. Format odpowiedzi API

### Sukces z danymi

```json
{
  "data": { ... },
  "meta": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 100
  }
}
```

### Błąd

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Nieprawidłowe dane wejściowe",
    "details": [
      { "field": "email", "message": "Nieprawidłowy format email" }
    ]
  }
}
```

### Kody statusu HTTP

| Kod | Opis |
|-----|------|
| `200` | Sukces |
| `201` | Utworzono |
| `204` | Brak treści (sukces bez ciała) |
| `400` | Błąd walidacji / nieprawidłowe żądanie |
| `401` | Brak uwierzytelnienia |
| `403` | Brak uprawnień |
| `404` | Zasób nie znaleziony |
| `409` | Konflikt (np. duplikat) |
| `429` | Zbyt wiele żądań (rate limit) |
| `500` | Błąd wewnętrzny serwera |
| `502` | Błąd zewnętrznego serwisu (ML, Integration Gateway) |

---

## Glosariusz

| Termin | Definicja |
|--------|-----------|
| **Outbox Pattern** | Wzorzec zapewniający niezawodność dostarczania zdarzeń przez zapis do lokalnej tabeli |
| **Circuit Breaker** | Wzorzec automatycznie odcinający połączenie do niestabilnego serwisu |
| **Backplane** | Warstwa synchronizacji między wieloma instancjami (Redis dla SignalR) |
| **Rate Limiting** | Ograniczanie liczby żądań od jednego klienta w określonym czasie |
| **CSRF** | Cross-Site Request Forgery — atak wymuszający nieautoryzowane żądania |
| **HMAC** | Hash-based Message Authentication Code — uwierzytelnianie wiadomości |
| **Graceful Degradation** | Kontynuacja działania serwisu z ograniczoną funkcjonalnością w razie awarii |
| **Soft-delete** | Logiczne usunięcie (oznaczenie jako usunięte) bez fizycznego usuwania z bazy |
| **WIP Limit** | Work-In-Progress Limit — ograniczenie liczby zadań w danej kolumnie |

---

> **Następna dokumentacja**: Auth Service, Frontend, ML Service, Integration Gateway, Notification Service, Infrastructure (DB/Redis/RabbitMQ)
