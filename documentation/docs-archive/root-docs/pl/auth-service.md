# DevHunt Auth Service — Dokumentacja Techniczna

> **Wersja**: 1.0  
> **Data aktualizacji**: 11 lutego 2026  
> **Technologia**: ASP.NET Core (.NET 10.0), Entity Framework Core 9, PostgreSQL 16  
> **Port domyślny**: 7001 (HTTP), Swagger: `/swagger`

---

## Spis treści

1. [Przegląd systemu](#1-przegląd-systemu)
2. [Architektura](#2-architektura)
3. [Stos technologiczny](#3-stos-technologiczny)
4. [Struktura projektu](#4-struktura-projektu)
5. [Konfiguracja i uruchomienie](#5-konfiguracja-i-uruchomienie)
6. [Pipeline middleware](#6-pipeline-middleware)
7. [Bezpieczeństwo](#7-bezpieczeństwo)
   - 7.1 [JWT (JSON Web Tokens)](#71-jwt-json-web-tokens)
   - 7.2 [Refresh Tokens](#72-refresh-tokens)
   - 7.3 [OAuth 2.0 (GitHub, Google)](#73-oauth-20-github-google)
   - 7.4 [CSRF (Double Submit Cookie)](#74-csrf-double-submit-cookie)
   - 7.5 [Rate Limiting](#75-rate-limiting)
   - 7.6 [Hashowanie haseł (BCrypt)](#76-hashowanie-haseł-bcrypt)
   - 7.7 [Cookies (HttpOnly)](#77-cookies-httponly)
8. [Endpointy API](#8-endpointy-api)
   - 8.1 [Rejestracja](#81-rejestracja)
   - 8.2 [Logowanie](#82-logowanie)
   - 8.3 [Odświeżanie tokenów](#83-odświeżanie-tokenów)
   - 8.4 [Bieżący użytkownik (Me)](#84-bieżący-użytkownik-me)
   - 8.5 [Wylogowanie](#85-wylogowanie)
   - 8.6 [Weryfikacja e-mail](#86-weryfikacja-e-mail)
   - 8.7 [Ponowne wysłanie kodu weryfikacji](#87-ponowne-wysłanie-kodu-weryfikacji)
   - 8.8 [Żądanie resetu hasła](#88-żądanie-resetu-hasła)
   - 8.9 [Reset hasła](#89-reset-hasła)
   - 8.10 [Logowanie OAuth (External Login)](#810-logowanie-oauth-external-login)
   - 8.11 [Callback OAuth](#811-callback-oauth)
9. [Serwisy](#9-serwisy)
   - 9.1 [AuthServicesFacade (fasada)](#91-authservicesfacade-fasada)
   - 9.2 [JwtTokenService](#92-jwttokenservice)
   - 9.3 [RefreshTokenService](#93-refreshtokenservice)
   - 9.4 [OAuthService](#94-oauthservice)
   - 9.5 [AuthCookieService](#95-authcookieservice)
   - 9.6 [AuthValidationService](#96-authvalidationservice)
   - 9.7 [RegistrationService](#97-registrationservice)
   - 9.8 [LoginService](#98-loginservice)
   - 9.9 [EmailService](#99-emailservice)
10. [Modele danych](#10-modele-danych)
11. [System e-mail](#11-system-e-mail)
12. [Metryki i obserwowalność](#12-metryki-i-obserwowalność)
13. [Health Checks](#13-health-checks)
14. [Docker](#14-docker)
15. [Testy](#15-testy)
16. [Zmienne środowiskowe](#16-zmienne-środowiskowe)

---

## 1. Przegląd systemu

**Auth Service** to dedykowany mikroserwis uwierzytelniania i autoryzacji platformy DevHunt. Odpowiada za:

- **Rejestrację użytkowników** z walidacją e-mail i weryfikacją kodu 6-cyfrowego
- **Logowanie** lokalne (e-mail + hasło) oraz przez OAuth 2.0 (GitHub, Google)
- **Zarządzanie tokenami** — JWT access tokens (30 min) + refresh tokens (7 dni) z rotacją
- **Resetowanie haseł** — bezpieczny proces na bazie tokenów jednorazowych
- **Wysyłkę e-mail** — szablony HTML do weryfikacji konta i resetowania hasła
- **Ochronę CSRF** — wzorzec Double Submit Cookie
- **Rate limiting** — ochrona endpoints przed atakami brute-force

Serwis jest **bezstanowy** (stateless) — stan sesji przechowywany jest w tokenach JWT, a refresh tokeny w bazie PostgreSQL. Współdzieli bazę danych z Core API przez wspólny projekt `DevHunt.Infrastructure`.

---

## 2. Architektura

```
┌─────────────────────────────────────────────────────┐
│              Frontend (Next.js 16) :3000             │
│           Przechowuje tokeny w HttpOnly cookies      │
└──────────────────────┬──────────────────────────────┘
                       │ HTTP (REST)
┌──────────────────────▼──────────────────────────────┐
│           API Gateway (Nginx) :80/443                │
│         Reverse proxy → /api/auth/* → :7001          │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│              Auth Service :7001                      │
│  ┌─────────────┐ ┌───────────┐ ┌──────────────┐     │
│  │ AuthController│ │ Middleware│ │   Filters    │     │
│  └──────┬──────┘ └─────┬─────┘ └──────┬───────┘     │
│         │              │              │              │
│  ┌──────▼──────────────▼──────────────▼───────┐     │
│  │          AuthServicesFacade                 │     │
│  │  ┌──────────┐ ┌─────────────┐ ┌──────────┐ │     │
│  │  │  Tokens  │ │  UserAuth   │ │  OAuth   │ │     │
│  │  │  ├─JWT   │ │  ├─Register │ │  ├─GitHub│ │     │
│  │  │  ├─Refresh│ │  ├─Login   │ │  └─Google│ │     │
│  │  │  └─Cookie│ │  └─Validate │ │          │ │     │
│  │  └──────────┘ └─────────────┘ └──────────┘ │     │
│  └─────────────────────┬───────────────────────┘     │
│                        │                             │
│  ┌─────────────────────▼───────────────────────┐     │
│  │            EmailService (MailKit)            │     │
│  └─────────────────────────────────────────────┘     │
└──────────────────────┬──────────────────────────────┘
                       │
          ┌────────────┼────────────┐
          │            │            │
   ┌──────▼─────┐ ┌───▼────┐ ┌────▼─────┐
   │ PostgreSQL │ │ Redis  │ │  SMTP    │
   │   :5432    │ │ :6379  │ │ (Gmail)  │
   │ users,     │ │ rate   │ │ emails   │
   │ refresh_   │ │ limit  │ │ verif.   │
   │ tokens     │ │ counters│ │ reset    │
   └────────────┘ └────────┘ └──────────┘
```

### Przepływ uwierzytelniania

```
         Użytkownik
             │
      ┌──────▼───────┐
      │  Rejestracja  │
      │ POST /register│
      └──────┬───────┘
             │ tworzenie konta + BCrypt hash
             │ wysyłka kodu 6-cyfrowego
      ┌──────▼────────────┐
      │ Weryfikacja e-mail │
      │ POST /verify-email │
      └──────┬────────────┘
             │ aktywacja konta
      ┌──────▼───────┐
      │  Logowanie   │
      │ POST /login  │
      └──────┬───────┘
             │ walidacja hasła (constant-time)
             │ generuje: access_token (JWT, 30 min)
             │           refresh_token (256-bit, 7 dni)
      ┌──────▼───────────┐
      │ Autoryzowany     │
      │ dostęp do API    │
      └──────┬───────────┘
             │ gdy access_token wygaśnie:
      ┌──────▼─────────────┐
      │ POST /refresh       │
      │ old refresh → rotacja│
      │ new access + refresh │
      └────────────────────┘
```

---

## 3. Stos technologiczny

| Warstwa | Technologia | Wersja |
|---------|-------------|--------|
| Framework | ASP.NET Core | .NET 10.0 |
| ORM | Entity Framework Core | 9.x |
| Baza danych | PostgreSQL | 16 |
| Cache/Rate Limiting | Redis | 7 |
| Hashowanie haseł | BCrypt.Net-Next | 4.x |
| JWT | Microsoft.AspNetCore.Authentication.JwtBearer | 10.x |
| E-mail | MailKit | 4.x |
| OAuth GitHub | AspNet.Security.OAuth.GitHub | 10.x |
| OAuth Google | Google.Apis.Auth | — |
| CSRF | Niestandardowa implementacja (Double Submit Cookie) | — |
| Rate Limiting | AspNetCoreRateLimit | 5.x |
| Metryki | prometheus-net.AspNetCore | 8.x |
| Tracing | OpenTelemetry (.NET) | 1.x |
| Logi | Serilog + Serilog.Sinks.Http (OpenObserve) | — |
| Sekrety (prod) | Azure Key Vault | — |
| Konteneryzacja | Docker (multi-stage) | — |

---

## 4. Struktura projektu

```
DevHunt.AuthService/
├── Program.cs                       # Punkt wejścia, konfiguracja DI, middleware
├── DevHunt.AuthService.csproj       # Definicja projektu, pakiety NuGet
├── Dockerfile                       # Obraz Docker (multi-stage build)
├── appsettings.json                 # Konfiguracja produkcyjna
├── appsettings.Development.json     # Konfiguracja deweloperska
├── DevHunt.AuthService.http         # Pliki testowe HTTP (REST Client)
│
├── Controllers/
│   └── AuthController.cs            # Jedyny kontroler — 11 endpointów
│
├── Services/
│   ├── AuthServicesFacade.cs        # Fasada grupująca wszystkie serwisy auth
│   ├── JwtTokenService.cs           # Generowanie tokenów JWT (access)
│   ├── RefreshTokenService.cs       # Zarządzanie refresh tokenami (HMAC-SHA256)
│   ├── OAuthService.cs              # OAuth 2.0 flow (GitHub, Google)
│   ├── AuthCookieService.cs         # HttpOnly cookies dla tokenów
│   ├── AuthValidationService.cs     # Walidacja e-mail i haseł
│   ├── RegistrationService.cs       # Logika rejestracji użytkowników
│   ├── LoginService.cs              # Logika logowania (constant-time)
│   └── EmailService.cs              # Wysyłka e-mail (MailKit, szablony HTML)
│
├── Middleware/
│   ├── MetricsMiddleware.cs         # Metryki Prometheus (HTTP, logowanie, rejestracja)
│   └── LogEnrichmentMiddleware.cs   # Wzbogacanie logów (TraceId, UserId)
│
├── Filters/
│   └── AuthCsrfValidationFilter.cs  # Filtr CSRF dla modyfikujących requestów
│
├── Extensions/
│   └── LoggingExtensions.cs         # Konfiguracja Serilog → OpenObserve
│
├── Models/
│   ├── AuthDtos.cs                  # Rekordy DTO (request/response)
│   ├── OAuthModels.cs               # Modele OAuth (providers, config)
│   └── PasswordValidation.cs        # Wynik walidacji hasła
│
└── Properties/
    └── launchSettings.json          # Profile uruchomienia
```

### Współdzielone zależności (DevHunt.Infrastructure)

Auth Service korzysta z projektu `DevHunt.Infrastructure`, który dostarcza:

- **`DevHuntDbContext`** — wspólny kontekst Entity Framework (tabele: `Users`, `RefreshTokens`, `Skills`, `Projects`, ...)
- **`User`** — encja użytkownika (e-mail, password hash, rola, OAuth IDs, weryfikacja)
- **`RefreshToken`** — encja refresh tokena (HMAC hash, wygaśnięcie, rewokacja)
- **`ReadWriteDbContextFactory`** — fabryka tworzenia osobnych kontekstów DB

---

## 5. Konfiguracja i uruchomienie

### 5.1 appsettings.json (produkcja)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db;Database=devhunt;Username=postgres;Password=..."
  },
  "Jwt": {
    "Key": "...",           // Min. 32 znaki, HMAC-SHA256
    "Issuer": "DevHunt.AuthService",
    "Audience": "DevHunt.CoreApi",
    "ExpirationMinutes": 30
  },
  "Cors": {
    "AllowedOrigins": ["https://devhunt.pl"]
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "...",
    "Password": "...",       // App password
    "FromEmail": "noreply@devhunt.pl",
    "FromName": "DevHunt"
  },
  "OAuth": {
    "GitHub": {
      "ClientId": "...",
      "ClientSecret": "..."
    },
    "Google": {
      "ClientId": "...",
      "ClientSecret": "..."
    }
  }
}
```

### 5.2 appsettings.Development.json (deweloperski)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=devhunt;Username=postgres;Password=devhunt_pass"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:3001"
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### 5.3 Azure Key Vault (produkcja)

W środowisku produkcyjnym Auth Service pobiera sekrety z Azure Key Vault. Konfiguracja jest warunkowa — aktywuje się gdy zmienna `AZURE_KEY_VAULT_URI` jest ustawiona:

```csharp
var keyVaultUri = builder.Configuration["AZURE_KEY_VAULT_URI"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}
```

Sekrety przechowywane w Key Vault:
- `Jwt--Key` — klucz podpisu JWT
- `ConnectionStrings--DefaultConnection` — connection string do PostgreSQL
- `OAuth--GitHub--ClientSecret` — GitHub OAuth secret
- `OAuth--Google--ClientSecret` — Google OAuth secret
- `Smtp--Password` — hasło do serwera SMTP

### 5.4 Uruchomienie lokalne

```bash
# Wymagana infrastruktura
docker-compose up -d db cache-service

# Uruchomienie serwisu (hot reload)
dotnet watch run --project DevHunt.AuthService

# Lub przez task VS Code:
# "Run AuthService (Hot Reload)"
```

### 5.5 Uruchomienie w Docker

```bash
docker-compose up -d auth-service
# Serwis dostępny na http://localhost:7001
# Swagger UI: http://localhost:7001/swagger
```

---

## 6. Pipeline middleware

Kolejność middleware w `Program.cs` jest kluczowa dla poprawnego działania:

```
Request
  │
  ├── 1. LogEnrichmentMiddleware         # Wzbogaca logi: TraceId, UserId
  │       Dodaje do kontekstu Serilog:
  │       - TraceId (z Activity.Current lub HttpContext)
  │       - UserId (z Claims, jeśli zalogowany)
  │
  ├── 2. Serilog RequestLogging          # Loguje każdy request HTTP
  │       Format: "HTTP {Method} {Path} responded {StatusCode} in {Elapsed}ms"
  │
  ├── 3. CORS                            # Cross-Origin Resource Sharing
  │       Origins: localhost:3000/3001 (dev) lub devhunt.pl (prod)
  │       Dozwolone: Credentials, Any Header, Any Method
  │
  ├── 4. IpRateLimiting                  # Rate limiting per-IP (via Redis/pamięć)
  │       Konfiguracja w sekcji "IpRateLimiting" (appsettings.json)
  │
  ├── 5. Authentication                  # JWT Bearer validation
  │       Sprawdza token w nagłówku Authorization
  │       Waliduje: Issuer, Audience, Signing Key, Lifetime
  │
  ├── 6. Authorization                   # Weryfikacja reguł autoryzacji
  │
  ├── 7. Prometheus HTTP Metrics         # prometheus-net: http_requests_total, ...
  │
  ├── 8. MetricsMiddleware              # Niestandardowe metryki auth:
  │       - auth_http_requests_total (method, path, status)
  │       - auth_http_request_duration_seconds (histogram)
  │       - auth_login_attempts_total (success/failure)
  │       - auth_registration_attempts_total (success/failure)
  │
  ├── 9. Routing                         # Mapowanie URL → Controller
  │
  └── 10. AuthController                 # Obsługa requestu
          Z filtrem: AuthCsrfValidationFilter
```

---

## 7. Bezpieczeństwo

### 7.1 JWT (JSON Web Tokens)

Auth Service generuje **access tokeny JWT** podpisane algorytmem **HMAC-SHA256**.

**Parametry tokena:**

| Parametr | Wartość |
|----------|---------|
| Algorytm | HmacSha256 |
| Czas życia | 30 minut |
| Issuer | `DevHunt.AuthService` |
| Audience | `DevHunt.CoreApi` |

**Claims w tokenie:**

| Claim | Opis | Przykład |
|-------|------|---------|
| `sub` | ID użytkownika (GUID) | `a1b2c3d4-...` |
| `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` | ID użytkownika (duplikat) | `a1b2c3d4-...` |
| `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress` | E-mail użytkownika | `jan@example.com` |
| `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` | Rola użytkownika | `participant` |
| `jti` | Unikalny identyfikator tokena | `GUID` |

**Generowanie tokena (`JwtTokenService`):**

```csharp
public string GenerateAccessToken(User user)
{
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expiration = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "30");

    var token = new JwtSecurityToken(
        issuer: _configuration["Jwt:Issuer"],
        audience: _configuration["Jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(expiration),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

**Walidacja tokenu (w pipeline middleware):**

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
```

---

### 7.2 Refresh Tokens

Refresh tokeny zapewniają **bezpieczną rotację sesji** bez konieczności ponownego logowania. Implementacja w `RefreshTokenService` (224 linie).

**Cechy implementacji:**

| Cecha | Szczegóły |
|-------|-----------|
| Generowanie | 256-bit losowy (RandomNumberGenerator) → Base64 URL-safe |
| Przechowywanie | HMAC-SHA256 hash w bazie (nigdy plaintext!) |
| Czas życia | 7 dni (domyślnie) |
| Rotacja | Po każdym użyciu: stary token rewokowany, nowy wygenerowany |
| Reuse Detection | Użycie rewokowanego tokena → rewokacja WSZYSTKICH tokenów użytkownika |
| Okno reuse | 10 sekund tolerancji (ochrona przed race conditions) |
| Śledzenie użycia | `UsageCount`, `LastUsedAt` na każdym tokenie |

**Schemat encji `RefreshToken`:**

```csharp
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; }           // HMAC-SHA256
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; } = 0;
    public bool IsRevoked { get; set; } = false;
    public string? RevocationReason { get; set; }    // "rotated", "logout", "reuse_detected", "admin"
    public DateTime? RevokedAt { get; set; }
    public User User { get; set; }
}
```

**Proces rotacji tokenów:**

```
1. Klient wysyła: POST /api/auth/refresh { refreshToken: "abc123..." }
2. RefreshTokenService.ValidateAndUseRefreshTokenAsync("abc123..."):
   a. Oblicza HMAC-SHA256 hash "abc123..."
   b. Szuka w DB: WHERE TokenHash = hash AND NOT IsRevoked AND ExpiresAt > UTC_NOW
   c. Jeśli znaleziony → aktualizuje UsageCount++, LastUsedAt
   d. Jeśli nie znaleziony → sprawdza czy istnieje rewokowany token z tym hashem
      → Jeśli tak I minęło > 10s od rewokacji → REUSE DETECTED!
      → Rewokuje WSZYSTKIE tokeny użytkownika (RevocationReason: "reuse_detected")
3. Stary refresh token jest rewokowany (RevocationReason: "rotated")
4. Nowy refresh token jest generowany i zwracany z nowym access tokenem
```

**Hashowanie tokenów (`ComputeTokenHash`):**

```csharp
private string ComputeTokenHash(string token)
{
    var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
    using var hmac = new HMACSHA256(key);
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(token));
    return Convert.ToBase64String(hash);
}
```

---

### 7.3 OAuth 2.0 (GitHub, Google)

Auth Service wspiera uwierzytelnianie przez zewnętrzne providery OAuth 2.0. Implementacja w `OAuthService` (404 linie).

**Wspierani providerzy:**

| Provider | Authorization URL | Token URL | User Info URL |
|----------|-------------------|-----------|---------------|
| GitHub | `github.com/login/oauth/authorize` | `github.com/login/oauth/access_token` | `api.github.com/user` + `/user/emails` |
| Google | `accounts.google.com/o/oauth2/v2/auth` | `oauth2.googleapis.com/token` | Token ID (JWT, weryfikowany przez `GoogleJsonWebSignature`) |

**Scopes:**

| Provider | Scopes |
|----------|--------|
| GitHub | `read:user`, `user:email` |
| Google | `openid`, `email`, `profile` |

**Parametr state (ochrona CSRF):**

Stan OAuth jest podpisywany HMAC-SHA256 z timestampem i nonce:

```
state = base64url({
    "t": timestamp (Unix),   // Ważność: 10 minut
    "n": nonce (GUID),       // Jednorazowy identyfikator
    "s": HMAC-SHA256 signature
})
```

Walidacja przy callbacku sprawdza:
1. Podpis HMAC jest prawidłowy (nie zmieniony)
2. Timestamp nie starszy niż 10 minut
3. Nonce jest obecny

**Przepływ OAuth:**

```
1. GET /api/auth/external-login?provider=github
   → Generuje state z HMAC podpisem
   → Redirect 302 → https://github.com/login/oauth/authorize?
       client_id=...&redirect_uri=...&state=...&scope=read:user,user:email

2. Użytkownik loguje się na GitHub i autoryzuje aplikację

3. GitHub redirectuje: GET /api/auth/callback?code=...&state=...
   → Walidacja state (HMAC + timestamp)
   → Wymiana code → access_token (POST github.com/login/oauth/access_token)
   → Pobranie profilu (GET api.github.com/user)
   → Pobranie e-mail (GET api.github.com/user/emails, szuka primary + verified)

4. Logika find-or-create użytkownika:
   a. Szukaj po GithubId → znaleziony → logowanie
   b. Szukaj po e-mail → znaleziony → linkowanie konta GitHub → logowanie
   c. Nie znaleziony → tworzenie nowego konta (bez hasła, IsEmailVerified = true)

5. Generowanie access_token + refresh_token
   → Redirect do frontend z tokenami w URL query:
     /auth/callback?accessToken=...&refreshToken=...
```

**Redirect URI validation:**

Dozwolone URI callbacka są konfigurowane w `appsettings.json`:
- Dev: `http://localhost:3000/auth/callback`
- Prod: `https://devhunt.pl/auth/callback`

---

### 7.4 CSRF (Double Submit Cookie)

Implementacja w `AuthCsrfValidationFilter.cs`. Chroni endpointy modyfikujące stan (POST, PUT, DELETE, PATCH) przed atakami CSRF.

**Mechanizm:**

1. Serwer generuje losowy token CSRF i umieszcza w **cookie** (`XSRF-TOKEN`, nie HttpOnly — czytelny przez JS)
2. Klient odczytuje cookie i dołącza wartość w nagłówku `X-XSRF-TOKEN`
3. Filtr porównuje wartość z cookie i nagłówka — muszą być identyczne

**Wyłączone endpointy (nie wymagają CSRF):**

| Endpoint | Powód wyłączenia |
|----------|-----------------|
| `POST /api/auth/login` | Pierwszy request, nie ma jeszcze cookie |
| `POST /api/auth/register` | Pierwszy request |
| `POST /api/auth/refresh` | Token w body wystarczy |
| `POST /api/auth/verify-email` | Link z e-maila |
| `POST /api/auth/forgot-password` | Publiczny endpoint |
| `POST /api/auth/reset-password` | Token z e-maila |

**Konfiguracja:**

```csharp
// W Program.cs — CSRF cookie jest ustawiane na każdym responsie
app.Use(async (context, next) =>
{
    if (!context.Request.Cookies.ContainsKey("XSRF-TOKEN"))
    {
        var token = Guid.NewGuid().ToString();
        context.Response.Cookies.Append("XSRF-TOKEN", token, new CookieOptions
        {
            HttpOnly = false,    // JS must read this
            Secure = !isDev,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }
    await next();
});
```

---

### 7.5 Rate Limiting

Ochrona przed atakami brute-force i nadużyciami. Implementacja przez bibliotekę **AspNetCoreRateLimit** z backendem Redis (produkcja) lub pamięcią (dev).

**Reguły rate limiting:**

| Endpoint | Limit | Okno | Cel ochrony |
|----------|-------|------|-------------|
| `POST /api/auth/login` | 5 requestów | 1 minuta | Brute-force hasła |
| `POST /api/auth/login` | 30 requestów | 15 minut | Rozproszone ataki |
| `POST /api/auth/login` | 60 requestów | 1 godzina | Intensywne próby |
| `POST /api/auth/register` | 3 requestów | 1 minuta | Spam kont |
| `POST /api/auth/register` | 10 requestów | 1 godzina | Masowe tworzenie |
| `POST /api/auth/refresh` | 20 requestów | 1 minuta | Token abuse |
| `POST /api/auth/resend-verification` | 1 request | 1 minuta | Spam e-mail |
| `POST /api/auth/resend-verification` | 3 requesty | 1 godzina | Spam e-mail |
| `POST /api/auth/forgot-password` | 1 request | 1 minuta | E-mail enumeration |
| `POST /api/auth/forgot-password` | 3 requesty | 1 godzina | Reset abuse |
| `POST /api/auth/reset-password` | 3 requestów | 1 minuta | Brute-force tokena |
| `POST /api/auth/reset-password` | 10 requestów | 1 godzina | Intensywne próby |

**Konfiguracja w `appsettings.json`:**

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "HttpStatusCode": 429,
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/auth/login",
        "Period": "1m",
        "Limit": 5
      }
      // ... pozostałe reguły
    ]
  },
  "IpRateLimitPolicies": {
    "IpRules": []
  }
}
```

**Tryb Redis vs pamięć:**

```csharp
// Produkcja — Redis
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "auth-rate-limit:";
    });
    builder.Services.AddSingleton<IRateLimitCounterStore, DistributedCacheRateLimitCounterStore>();
}
else
{
    // Dev — in-memory
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
}
```

---

### 7.6 Hashowanie haseł (BCrypt)

Hasła użytkowników są hashowane algorytmem **BCrypt** z automatycznie generowaną solą.

```csharp
// Rejestracja — hashowanie
user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

// Logowanie — weryfikacja (constant-time)
var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
```

**Ochrona timing attack przy logowaniu (`LoginService`):**

Aby zapobiec wyciekowi informacji o istnieniu konta, `LoginService` wykonuje **zawsze** operację hashowania, nawet gdy użytkownik nie istnieje:

```csharp
public async Task<LoginResult> AuthenticateAsync(LoginRequest request)
{
    var normalizedEmail = NormalizeEmail(request.Email);
    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

    if (user is null)
    {
        // Dummy hash — constant-time, aby atakujący nie mógł
        // odróżnić "użytkownik nie istnieje" od "złe hasło"
        BCrypt.Net.BCrypt.Verify("dummy", 
            "$2a$11$K9GnP1e5KbyJSjn.I5pVCeGPU0VxXfHGlk...");
        return new LoginResult(false, ErrorMessage: "Invalid email or password.");
    }

    if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return new LoginResult(false, ErrorMessage: "Invalid email or password.");

    if (!user.IsEmailVerified)
        return new LoginResult(false, User: user, RequiresEmailVerification: true);

    return new LoginResult(true, User: user);
}
```

**Legacy e-mail normalization z self-healing:**

`LoginService` obsługuje starsze formaty e-maili — jeśli użytkownik nie zostanie znaleziony po normalizacji, sprawdzane są alternatywne warianty (Trim, ToLower). Gdy użytkownik zostanie znaleziony po alternatywnej normalizacji, e-mail jest automatycznie naprawiany w bazie.

---

### 7.7 Cookies (HttpOnly)

Tokeny są przechowywane w **HttpOnly cookies**, niedostępnych z poziomu JavaScript.

**Konfiguracja cookies (`AuthCookieService`):**

| Cookie | Czas życia | HttpOnly | Secure | SameSite | Path |
|--------|-----------|----------|--------|----------|------|
| `access_token` | 30 minut | ✅ Tak | ✅ (prod) | Lax | `/` |
| `refresh_token` | 7 dni | ✅ Tak | ✅ (prod) | Lax | `/` |

**Ustawianie cookies:**

```csharp
public void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken)
{
    response.Cookies.Append("access_token", accessToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = _isProduction,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddMinutes(30),
        Path = "/"
    });

    response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = _isProduction,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddDays(7),
        Path = "/"
    });
}
```

**Usuwanie cookies przy wylogowaniu:**

```csharp
public void ClearAuthCookies(HttpResponse response)
{
    response.Cookies.Delete("access_token");
    response.Cookies.Delete("refresh_token");
    response.Cookies.Delete("XSRF-TOKEN");
}
```

---

## 8. Endpointy API

Wszystkie endpointy znajdują się w `AuthController` pod trasą bazową **`/api/auth`**.

### 8.1 Rejestracja

```
POST /api/auth/register
```

**Ciało żądania:**

```json
{
  "email": "jan@example.com",
  "password": "MojeHasło123!",
  "fullName": "Jan Kowalski"
}
```

**Walidacja:**

| Pole | Reguły |
|------|--------|
| `email` | Wymagany, format e-mail (MailAddress), unikalny w bazie |
| `password` | 8-128 znaków, min. 1 wielka litera, 1 mała, 1 cyfra, 1 znak specjalny |
| `fullName` | Wymagany, niepusty |

**Szczegóły walidacji hasła (`AuthValidationService.ValidatePasswordRequirements`):**

```csharp
public PasswordValidationResult ValidatePasswordRequirements(string password)
{
    return new PasswordValidationResult
    {
        MeetsMinLength = password.Length >= 8,
        MeetsMaxLength = password.Length <= 128,
        HasUppercase = password.Any(char.IsUpper),
        HasLowercase = password.Any(char.IsLower),
        HasDigit = password.Any(char.IsDigit),
        HasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c))
    };
}
```

**Przepływ:**

1. Walidacja e-mail (format + unikalność) i hasła (6 reguł)
2. Tworzenie użytkownika z rolą `participant`, hashowanie hasła BCrypt
3. Generowanie 6-cyfrowego kodu weryfikacyjnego (`Random.Shared.Next(100000, 999999)`)
4. Ważność kodu: 24 godziny
5. Wysyłka e-mail z kodem weryfikacyjnym (jeśli SMTP skonfigurowane)
6. **Auto-weryfikacja** w dev: jeśli SMTP nie skonfigurowane, konto jest automatycznie weryfikowane

**Odpowiedź — sukces (200):**

```json
{
  "message": "Registration successful. Please check your email for the verification code.",
  "userId": "a1b2c3d4-...",
  "autoVerified": false
}
```

**Odpowiedź — błędy (400):**

```json
{
  "error": "Email is already registered"
}
```

```json
{
  "error": "Password does not meet requirements",
  "requirements": {
    "meetsMinLength": true,
    "meetsMaxLength": true,
    "hasUppercase": false,
    "hasLowercase": true,
    "hasDigit": true,
    "hasSpecialChar": false
  }
}
```

---

### 8.2 Logowanie

```
POST /api/auth/login
```

**Ciało żądania:**

```json
{
  "email": "jan@example.com",
  "password": "MojeHasło123!"
}
```

**Przepływ:**

1. Normalizacja e-maila (trim + lowercase)
2. Wyszukanie użytkownika w bazie (z legacy self-healing)
3. Weryfikacja hasła BCrypt (**constant-time** — dummy hash dla nieistniejących)
4. Sprawdzenie weryfikacji e-mail (jeśli nie zweryfikowane → błąd z flagą)
5. Generowanie access token (JWT, 30 min)
6. Generowanie refresh token (256-bit, 7 dni, hash HMAC-SHA256 → DB)
7. Ustawienie HttpOnly cookies
8. Metryki: `auth_login_attempts_total{success="true"}`

**Odpowiedź — sukces (200):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "X7cB9dF2...",
  "expiresAt": "2026-02-11T15:30:00Z",
  "user": {
    "id": "a1b2c3d4-...",
    "email": "jan@example.com",
    "fullName": "Jan Kowalski",
    "role": "participant"
  }
}
```

**Odpowiedź — niezweryfikowany e-mail (401):**

```json
{
  "error": "Email not verified. Please check your inbox for the verification code.",
  "requiresEmailVerification": true,
  "userId": "a1b2c3d4-..."
}
```

**Odpowiedź — błędne dane (401):**

```json
{
  "error": "Invalid email or password."
}
```

---

### 8.3 Odświeżanie tokenów

```
POST /api/auth/refresh
```

**Ciało żądania:**

```json
{
  "refreshToken": "X7cB9dF2..."
}
```

**Przepływ:**

1. Walidacja refresh tokena (HMAC hash → szukaj w DB)
2. Jeśli token ważny → aktualizacja `UsageCount` i `LastUsedAt`
3. Rewokacja starego tokena (reason: `"rotated"`)
4. Wygenerowanie nowego refresh tokena
5. Wygenerowanie nowego access tokena JWT
6. Ustawienie nowych HttpOnly cookies

**Reuse Detection:**

Jeśli przesłany token jest **już rewokowany** i minęło więcej niż 10 sekund od rewokacji:
- Wszystkie aktywne refresh tokeny użytkownika są **natychmiast rewokowane** (reason: `"reuse_detected"`)
- Użytkownik musi się ponownie zalogować
- To chroni przed atakami typu **replay** — jeśli atakujący ukradł stary token

**Odpowiedź — sukces (200):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...(nowy)",
  "refreshToken": "Y8dC0eG3...(nowy)",
  "expiresAt": "2026-02-11T16:00:00Z",
  "user": { ... }
}
```

**Odpowiedź — nieprawidłowy token (401):**

```json
{
  "error": "Invalid or expired refresh token"
}
```

---

### 8.4 Bieżący użytkownik (Me)

```
GET /api/auth/me
Authorization: Bearer <access_token>
```

**Wymagana autoryzacja:** Tak (JWT Bearer)

**Przepływ:**

1. Ekstrakcja `userId` z claims JWT
2. Pobranie użytkownika z bazy
3. Zwrócenie profilu bez wrażliwych danych

**Odpowiedź — sukces (200):**

```json
{
  "id": "a1b2c3d4-...",
  "email": "jan@example.com",
  "fullName": "Jan Kowalski",
  "role": "participant",
  "isEmailVerified": true,
  "avatar": "/avatars/a1b2c3d4.png",
  "createdAt": "2026-01-15T10:00:00Z"
}
```

**Odpowiedź — brak autoryzacji (401):**

```json
{
  "error": "Unauthorized"
}
```

---

### 8.5 Wylogowanie

```
POST /api/auth/logout
```

**Ciało żądania:**

```json
{
  "refreshToken": "X7cB9dF2..."
}
```

**Przepływ:**

1. Rewokacja refresh tokena w bazie (reason: `"logout"`)
2. Usunięcie cookies (`access_token`, `refresh_token`, `XSRF-TOKEN`)

**Odpowiedź — sukces (200):**

```json
{
  "message": "Logged out successfully"
}
```

---

### 8.6 Weryfikacja e-mail

```
POST /api/auth/verify-email
```

**Ciało żądania:**

```json
{
  "userId": "a1b2c3d4-...",
  "code": "847291"
}
```

**Przepływ:**

1. Wyszukanie użytkownika po `userId`
2. Sprawdzenie czy `VerificationToken` zgadza się z `code`
3. Sprawdzenie czy `VerificationTokenExpiresAt` nie minął (24h)
4. Ustawienie `IsEmailVerified = true`
5. Wyczyszczenie tokena weryfikacji

**Odpowiedź — sukces (200):**

```json
{
  "message": "Email verified successfully"
}
```

**Odpowiedż — błędy (400):**

```json
{
  "error": "Invalid or expired verification code"
}
```

---

### 8.7 Ponowne wysłanie kodu weryfikacji

```
POST /api/auth/resend-verification
```

**Ciało żądania:**

```json
{
  "email": "jan@example.com"
}
```

**Przepływ:**

1. Wyszukanie użytkownika po e-mail
2. Sprawdzenie czy e-mail nie jest już zweryfikowany
3. Generowanie nowego 6-cyfrowego kodu
4. Ustawienie nowego `VerificationTokenExpiresAt` (24h)
5. Wysyłka e-mail z nowym kodem

**Rate Limiting:** 1 request / minutę, 3 requesty / godzinę

**Odpowiedź — sukces (200):**

```json
{
  "message": "Verification code sent"
}
```

---

### 8.8 Żądanie resetu hasła

```
POST /api/auth/forgot-password
```

**Ciało żądania:**

```json
{
  "email": "jan@example.com"
}
```

**Przepływ:**

1. Wyszukanie użytkownika po e-mail
2. **Zawsze zwraca sukces** (nawet jeśli użytkownik nie istnieje — ochrona enumeracji)
3. Jeśli użytkownik istnieje:
   - Generowanie bezpiecznego tokena (`RandomNumberGenerator`, URL-safe Base64)
   - Ustawienie `PasswordResetToken` i `PasswordResetTokenExpiresAt` (1 godzina)
   - Wysyłka e-mail z linkiem resetowania

**Rate Limiting:** 1 request / minutę, 3 requesty / godzinę

**Odpowiedź — zawsze (200):**

```json
{
  "message": "If this email exists, a password reset link has been sent."
}
```

---

### 8.9 Reset hasła

```
POST /api/auth/reset-password
```

**Ciało żądania:**

```json
{
  "email": "jan@example.com",
  "token": "X7cB9dF2aE...",
  "newPassword": "NoweHasło456!"
}
```

**Przepływ:**

1. Wyszukanie użytkownika po e-mail
2. Walidacja tokena resetowania (porównanie + ważność 1h)
3. Walidacja nowego hasła (te same 6 reguł co przy rejestracji)
4. Hashowanie nowego hasła BCrypt
5. Wyczyszczenie tokenów resetowania
6. Rewokacja WSZYSTKICH aktywnych refresh tokenów użytkownika (reason: `"password_reset"`)

**Odpowiedź — sukces (200):**

```json
{
  "message": "Password reset successfully"
}
```

---

### 8.10 Logowanie OAuth (External Login)

```
GET /api/auth/external-login?provider=github
GET /api/auth/external-login?provider=google
```

**Query Parameters:**

| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `provider` | string | Tak | `github` lub `google` |

**Przepływ:**

1. Generowanie HMAC-podpisanego state parametra (timestamp + nonce)
2. Budowanie URL autoryzacji OAuth z odpowiednimi scope
3. **Redirect 302** do providera

**Odpowiedź:**

```
HTTP 302 Found
Location: https://github.com/login/oauth/authorize?
  client_id=xxx&
  redirect_uri=http://localhost:7001/api/auth/callback&
  state=eyJ0IjoxNzA3...&
  scope=read:user%20user:email
```

---

### 8.11 Callback OAuth

```
GET /api/auth/callback?code=xxx&state=yyy
```

**Query Parameters:**

| Parametr | Typ | Opis |
|----------|-----|------|
| `code` | string | Authorization code z providera OAuth |
| `state` | string | Zakodowany state z podpisem HMAC |

**Przepływ:**

1. Walidacja `state` (HMAC podpis + timestamp < 10 min)
2. Wymiana `code` na access token (POST do providera)
3. Pobranie profilu użytkownika z providera
4. Logika find-or-create (patrz sekcja 7.3)
5. Generowanie access + refresh tokenów DevHunt
6. **Redirect 302** do frontendu z tokenami w URL

**Odpowiedź — sukces:**

```
HTTP 302 Found
Location: http://localhost:3000/auth/callback?
  accessToken=eyJhbGci...&
  refreshToken=X7cB9dF2...
```

**Odpowiedź — błąd:**

```
HTTP 302 Found
Location: http://localhost:3000/auth/callback?error=oauth_failed
```

---

## 9. Serwisy

### 9.1 AuthServicesFacade (fasada)

Wzorzec **Facade** grupujący wszystkie serwisy uwierzytelniania. Zmniejsza liczbę zależności wstrzykiwanych do kontrolera (zamiast 7+ serwisów → 1 fasada).

**Interfejs hierarchiczny:**

```csharp
public interface IAuthServices
{
    ITokenServices Tokens { get; }     // JWT + RefreshToken + Cookie
    IUserAuthServices User { get; }    // Registration + Login + Validation
    IOAuthService OAuth { get; }       // GitHub + Google

    // Skróty (convenience accessors):
    IAuthValidationService Validation { get; }
    IJwtTokenService Jwt { get; }
    IAuthCookieService Cookie { get; }
    IRegistrationService Registration { get; }
    ILoginService Login { get; }
}

public interface ITokenServices
{
    IJwtTokenService Jwt { get; }
    IRefreshTokenService RefreshToken { get; }
    IAuthCookieService Cookie { get; }
}

public interface IUserAuthServices
{
    IRegistrationService Registration { get; }
    ILoginService Login { get; }
    IAuthValidationService Validation { get; }
}
```

**Użycie w kontrolerze:**

```csharp
// Zamiast wielu zależności:
public AuthController(
    DevHuntDbContext dbContext,
    IConfiguration configuration,
    ILogger<AuthController> logger,
    IAuthServices auth   // ← jedna fasada
)

// Dostęp do serwisów:
var accessToken = _auth.Jwt.GenerateAccessToken(user);
var (refreshToken, entity) = await _auth.Tokens.RefreshToken
    .CreateRefreshTokenAsync(user.Id, TimeSpan.FromDays(7));
_auth.Cookie.SetAuthCookies(Response, accessToken, refreshToken);
var isValid = _auth.Validation.IsValidEmail(email);
```

---

### 9.2 JwtTokenService

Odpowiedzialny za generowanie tokenów JWT (access tokens).

| Metoda | Opis |
|--------|------|
| `GenerateAccessToken(User user)` | Generuje JWT z claims: Sub, NameIdentifier, Email, Role, Jti |

**Parametry konfiguracyjne:**
- `Jwt:Key` — klucz podpisu (min. 32 znaki, HMAC-SHA256)
- `Jwt:Issuer` — wydawca tokena (`DevHunt.AuthService`)
- `Jwt:Audience` — odbiorca tokena (`DevHunt.CoreApi`)
- `Jwt:ExpirationMinutes` — czas życia (domyślnie 30)

---

### 9.3 RefreshTokenService

Zarządzanie cyklem życia refresh tokenów. Najważniejszy serwis pod kątem bezpieczeństwa sesji.

| Metoda | Opis |
|--------|------|
| `CreateRefreshTokenAsync(Guid userId, TimeSpan lifetime)` | Generuje 256-bit token, hash HMAC → DB |
| `ValidateAndUseRefreshTokenAsync(string token)` | Waliduje hash, aktualizuje usage, zwraca encję |
| `RevokeRefreshTokenAsync(string token, string reason)` | Rewokuje token z podanym powodem |
| `RevokeAllUserTokensAsync(Guid userId, string reason)` | Rewokuje WSZYSTKIE tokeny użytkownika |

**Powody rewokacji (`RevocationReason`):**

| Powód | Kiedy |
|-------|-------|
| `"rotated"` | Normalny refresh — stary token zastąpiony nowym |
| `"logout"` | Użytkownik wylogował się |
| `"reuse_detected"` | Wykryto ponowne użycie rewokowanego tokena |
| `"password_reset"` | Użytkownik zmienił hasło |
| `"admin"` | Administrator ręcznie unieważnił sesję |

---

### 9.4 OAuthService

Pełna implementacja przepływu OAuth 2.0 Authorization Code Grant.

| Metoda | Opis |
|--------|------|
| `GetAuthorizationUrl(string provider)` | Buduje URL autoryzacji z HMAC-podpisanym state |
| `ExchangeCodeForToken(string provider, string code)` | Wymienia code na access token |
| `GetUserInfo(string provider, string accessToken)` | Pobiera profil użytkownika z API providera |
| `ValidateState(string state)` | Waliduje podpis HMAC i timestamp |
| `FindOrCreateUser(OAuthUserInfo info, string provider)` | Szuka/tworzy/linkuje konto użytkownika |
| `ValidateRedirectUri(string uri)` | Waliduje URI callbacka |

**Konfiguracja providerów (`OAuthProviderConfig`):**

```csharp
public record OAuthProviderConfig(
    OAuthEndpoints Endpoints,       // auth URL, token URL, userinfo URL
    OAuthCredentials Credentials,   // ClientId, ClientSecret
    string[] Scopes                 // np. ["read:user", "user:email"]
);
```

---

### 9.5 AuthCookieService

Zarządzanie cookies HTTP dla tokenów uwierzytelniania.

| Metoda | Opis |
|--------|------|
| `SetAuthCookies(HttpResponse response, string accessToken, string refreshToken)` | Ustawia access + refresh cookies |
| `ClearAuthCookies(HttpResponse response)` | Usuwa wszystkie cookies auth + CSRF |

---

### 9.6 AuthValidationService

Walidacja danych wejściowych uwierzytelniania.

| Metoda | Opis |
|--------|------|
| `IsValidEmail(string email)` | Walidacja formatu e-mail (System.Net.Mail.MailAddress) |
| `IsValidPassword(string password)` | Sprawdzenie min. 8 znaków, wszystkie grupy |
| `ValidatePasswordRequirements(string password)` | Szczegółowy wynik — 6 wymagań osobno |
| `NormalizeEmail(string email)` | Trim + ToLowerInvariant |

**Wymagania hasła (`PasswordValidationResult`):**

```csharp
public class PasswordValidationResult
{
    public bool MeetsMinLength { get; set; }    // >= 8
    public bool MeetsMaxLength { get; set; }    // <= 128
    public bool HasUppercase { get; set; }       // A-Z
    public bool HasLowercase { get; set; }       // a-z
    public bool HasDigit { get; set; }           // 0-9
    public bool HasSpecialChar { get; set; }     // !@#$%... (non-alphanumeric)

    public bool IsValid => MeetsMinLength && MeetsMaxLength &&
        HasUppercase && HasLowercase && HasDigit && HasSpecialChar;
}
```

---

### 9.7 RegistrationService

Logika tworzenia nowych kont użytkowników.

| Metoda | Opis |
|--------|------|
| `ValidateRegistrationAsync(RegisterRequest request)` | Walidacja e-mail + hasło, zwraca null (ok) lub string (error) |
| `CreateUserAsync(RegisterRequest request)` | Tworzy użytkownika: BCrypt hash, kod weryfikacyjny, rola participant |
| `ShouldAutoVerify()` | Sprawdza czy SMTP jest skonfigurowane; jeśli nie → auto-verify |
| `AutoVerifyUserAsync(User user)` | Ustawia `IsEmailVerified = true` bez kodu |

**Auto-weryfikacja w dev:**

Gdy SMTP nie jest skonfigurowane (`Smtp:Host` jest puste), użytkownicy są automatycznie weryfikowani przy rejestracji. To ułatwia rozwój lokalny bez serwera pocztowego.

---

### 9.8 LoginService

Logika uwierzytelniania hasłem z zabezpieczeniami timing attack.

| Metoda | Opis |
|--------|------|
| `AuthenticateAsync(LoginRequest request)` | Pełna walidacja + dummy hash + legacy normalizacja |

**Zwracany typ — `LoginResult`:**

```csharp
public record LoginResult(
    bool Success,
    User? User = null,
    string? ErrorMessage = null,
    bool RequiresEmailVerification = false
);
```

---

### 9.9 EmailService

Wysyłka e-maili przez MailKit/SMTP z szablonami HTML.

| Metoda | Opis |
|--------|------|
| `SendVerificationEmailAsync(string email, string code)` | E-mail z 6-cyfrowym kodem weryfikacyjnym |
| `SendPasswordResetEmailAsync(string email, string token)` | E-mail z linkiem do resetowania hasła |

**Zabezpieczenia:**

- **Log Sanitization** — adresy e-mail w logach są zaciemniane (`jan@example.com` → `j***@e***.com`) przez `LogSanitizer`
- **Tryb mock** — gdy SMTP nie skonfigurowane, e-mail jest "wysyłany" do logów zamiast na serwer
- **Timeout** — MailKit ma skonfigurowane timeouty na połączenie SMTP

**Szablon e-mail weryfikacji (HTML):**

```html
<h2>Weryfikacja konta DevHunt</h2>
<p>Twój kod weryfikacyjny:</p>
<div style="font-size: 32px; font-weight: bold; letter-spacing: 4px; 
            text-align: center; padding: 20px; background: #f0f0f0;">
  847291
</div>
<p>Kod jest ważny przez 24 godziny.</p>
```

**Szablon e-mail resetowania hasła:**

```html
<h2>Resetowanie hasła DevHunt</h2>
<p>Kliknij poniższy link, aby zresetować hasło:</p>
<a href="https://devhunt.pl/auth/reset-password?token=X7cB9dF2...&email=jan@example.com">
  Resetuj hasło
</a>
<p>Link jest ważny przez 1 godzinę.</p>
```

---

## 10. Modele danych

### 10.1 Encja `User` (DevHunt.Infrastructure)

Główna encja użytkownika, współdzielona między Auth Service i Core API.

| Pole | Typ | Opis |
|------|-----|------|
| `Id` | `Guid` | Klucz główny |
| `Email` | `string` | E-mail (unikalny, znormalizowany) |
| `PasswordHash` | `string?` | Hash BCrypt (null dla kont OAuth-only) |
| `FullName` | `string` | Imię i nazwisko |
| `Role` | `string` | Rola: `participant`, `company`, `curator`, `admin`, `superadmin` |
| `Avatar` | `string?` | URL avatara |
| `Bio` | `string?` | Biografia |
| `Location` | `string?` | Lokalizacja |
| `Github` | `string?` | Link do GitHub |
| `Linkedin` | `string?` | Link do LinkedIn |
| `Website` | `string?` | Link do strony |
| `GithubId` | `string?` | GitHub OAuth ID (linkowanie konta) |
| `GithubUsername` | `string?` | GitHub login |
| `GoogleId` | `string?` | Google OAuth ID |
| `IsEmailVerified` | `bool` | Czy e-mail zweryfikowany |
| `VerificationToken` | `string?` | 6-cyfrowy kod weryfikacyjny |
| `VerificationTokenExpiresAt` | `DateTime?` | Ważność kodu (24h) |
| `PasswordResetToken` | `string?` | Token resetu hasła (URL-safe Base64) |
| `PasswordResetTokenExpiresAt` | `DateTime?` | Ważność tokena resetu (1h) |
| `IsActive` | `bool` | Czy konto aktywne |
| `CreatedAt` | `DateTime` | Data utworzenia |
| `UpdatedAt` | `DateTime` | Ostatnia modyfikacja |

**Dostępne role i ich uprawnienia:**

| Rola | Opis |
|------|------|
| `participant` | Zwykły użytkownik — domyślna przy rejestracji |
| `company` | Użytkownik firmowy — dodatkowe uprawnienia organizacji |
| `curator` | Kurator — moderacja, przegląd projektów |
| `admin` | Administrator — pełna kontrola |
| `superadmin` | Super admin — najwyższy poziom uprawnień |

**Metody pomocnicze:**

```csharp
public bool IsAdminOrCurator() => Role is "admin" or "curator" or "superadmin";
public bool IsSuperAdmin() => Role == "superadmin";
```

### 10.2 Encja `RefreshToken` (DevHunt.Infrastructure)

| Pole | Typ | Opis |
|------|-----|------|
| `Id` | `Guid` | Klucz główny |
| `UserId` | `Guid` | FK → User |
| `TokenHash` | `string` | HMAC-SHA256 hash tokena (nigdy plaintext!) |
| `ExpiresAt` | `DateTime` | Data wygaśnięcia (7 dni od utworzenia) |
| `CreatedAt` | `DateTime` | Data utworzenia |
| `LastUsedAt` | `DateTime?` | Ostatnie użycie (rotacja) |
| `UsageCount` | `int` | Ile razy token był użyty |
| `IsRevoked` | `bool` | Czy rewokowany |
| `RevocationReason` | `string?` | Powód rewokacji |
| `RevokedAt` | `DateTime?` | Data rewokacji |

### 10.3 DTO (AuthDtos.cs)

```csharp
// Żądania
public record RegisterRequest(string Email, string Password, string FullName);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record VerifyEmailRequest(string UserId, string Code);
public record ResendVerificationRequest(string Email);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);

// Odpowiedzi
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

// OAuth
public record OAuthUserInfo(
    string Id,
    string Email,
    string Name,
    string? AvatarUrl
);
public record GitHubEmail(string Email, bool Primary, bool Verified);
```

### 10.4 Modele OAuth (OAuthModels.cs)

```csharp
public enum OAuthProvider { GitHub, Google }

public record OAuthProviderConfig(
    OAuthEndpoints Endpoints,
    OAuthCredentials Credentials,
    string[] Scopes
);

public record OAuthEndpoints(
    string AuthorizationUrl,
    string TokenUrl,
    string UserInfoUrl
);

public record OAuthCredentials(string ClientId, string ClientSecret);
public record CallbackUrl(string Url);
public record RedirectUri(string Uri);
public record OAuthCodeExchange(string Code, string RedirectUri);
```

---

## 11. System e-mail

### 11.1 Konfiguracja SMTP

```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "devhunt.noreply@gmail.com",
    "Password": "xxxx xxxx xxxx xxxx",  // Google App Password
    "FromEmail": "noreply@devhunt.pl",
    "FromName": "DevHunt"
  }
}
```

### 11.2 Tryby pracy

| Tryb | Warunek | Zachowanie |
|------|---------|------------|
| **Produkcyjny** | `Smtp:Host` jest ustawione | Wysyłka przez MailKit/SMTP |
| **Mock (dev)** | `Smtp:Host` puste lub brak | Log e-mail do konsoli, auto-weryfikacja |

### 11.3 Bezpieczeństwo logów

`EmailService` korzysta z `LogSanitizer` do zaciemniania danych wrażliwych w logach:

```
// Zamiast:
INFO: Sending verification email to jan.kowalski@gmail.com
// W logach pojawi się:
INFO: Sending verification email to j***@g***.com
```

---

## 12. Metryki i obserwowalność

### 12.1 Metryki Prometheus (MetricsMiddleware)

Auth Service eksportuje niestandardowe metryki Prometheus na endpoint `/metrics`.

**Countery:**

| Metryka | Etykiety | Opis |
|---------|----------|------|
| `auth_http_requests_total` | `method`, `path`, `status_code` | Łączna liczba requestów HTTP |
| `auth_login_attempts_total` | `success` (true/false) | Liczba prób logowania |
| `auth_registration_attempts_total` | `success` (true/false) | Liczba prób rejestracji |

**Histogramy:**

| Metryka | Etykiety | Opis |
|---------|----------|------|
| `auth_http_request_duration_seconds` | `method`, `path` | Czas obsługi requestów HTTP |

**Dodatkowo** — standardowe metryki `prometheus-net.AspNetCore`:
- `http_request_duration_seconds` (histogram)
- `http_requests_in_progress` (gauge)
- `http_requests_received_total` (counter)

### 12.2 Serilog → OpenObserve

Logi strukturalne wysyłane do OpenObserve przez HTTP sink:

```csharp
.WriteTo.Http(
    requestUri: "http://openobserve:5080/api/devhunt/auth-service/_json",
    queueLimitBytes: 1_000_000,
    textFormatter: new RenderedCompactJsonFormatter(),
    httpClient: new BasicAuthHttpClient("admin", "openobserve_password")
)
```

**Wzbogacanie logów (`LogEnrichmentMiddleware`):**

Każdy request jest automatycznie wzbogacany o:
- `TraceId` — identyfikator śledzenia (z OpenTelemetry `Activity.Current` lub HttpContext)
- `UserId` — ID zalogowanego użytkownika (z JWT claims)

**Format logów:**

```json
{
  "@t": "2026-02-11T12:00:00Z",
  "@mt": "HTTP POST /api/auth/login responded 200 in 45.2ms",
  "TraceId": "abc123...",
  "UserId": "a1b2c3d4-...",
  "RequestMethod": "POST",
  "RequestPath": "/api/auth/login",
  "StatusCode": 200,
  "Elapsed": 45.2
}
```

### 12.3 OpenTelemetry (Tracing)

Auth Service eksportuje trace'y do kolektora OpenTelemetry (OTLP):

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
    });
```

**Instrumentowane komponenty:**
- ASP.NET Core (requesty HTTP)
- HttpClient (wywołania do OAuth providerów)
- Entity Framework Core (zapytania do PostgreSQL)

---

## 13. Health Checks

Auth Service rejestruje health check do monitorowania dostępności:

```
GET /healthz
```

**Sprawdzane komponenty:**

| Komponent | Opis |
|-----------|------|
| PostgreSQL | Ping do bazy danych (`DbContext`) |

**Odpowiedź — zdrowy (200):**

```
Healthy
```

**Odpowiedź — niezdowy (503):**

```
Unhealthy
```

Health check jest wykorzystywany przez:
- Docker: `HEALTHCHECK CMD curl -f http://localhost:7001/healthz || exit 1`
- Kubernetes: liveness/readiness probes
- Load balancer: sprawdzenie dostępności instancji

---

## 14. Docker

### 14.1 Dockerfile (multi-stage build)

```dockerfile
# ============ Stage 1: Build ============
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Kopiowanie plików projektu i restore
COPY DevHunt.AuthService/DevHunt.AuthService.csproj DevHunt.AuthService/
COPY DevHunt.Infrastructure/DevHunt.Infrastructure.csproj DevHunt.Infrastructure/
RUN dotnet restore DevHunt.AuthService/DevHunt.AuthService.csproj

# Kopiowanie kodu źródłowego i build
COPY DevHunt.AuthService/ DevHunt.AuthService/
COPY DevHunt.Infrastructure/ DevHunt.Infrastructure/
RUN dotnet publish DevHunt.AuthService/DevHunt.AuthService.csproj \
    -c Release -o /app/publish

# ============ Stage 2: Runtime ============
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Bezpieczeństwo: non-root user (SEC-015)
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:7001/healthz || exit 1

EXPOSE 7001
ENV ASPNETCORE_URLS=http://+:7001

ENTRYPOINT ["dotnet", "DevHunt.AuthService.dll"]
```

### 14.2 docker-compose.yml (fragment)

```yaml
auth-service:
  build:
    context: .
    dockerfile: DevHunt.AuthService/Dockerfile
  ports:
    - "7001:7001"
  environment:
    - ConnectionStrings__DefaultConnection=Host=db;Database=devhunt;Username=postgres;Password=...
    - Jwt__Key=...
    - Redis__ConnectionString=cache-service:6379
  depends_on:
    db:
      condition: service_healthy
    cache-service:
      condition: service_started
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:7001/healthz"]
    interval: 30s
    timeout: 3s
    retries: 3
```

### 14.3 Nginx (API Gateway routing)

```nginx
location /api/auth/ {
    proxy_pass http://auth-service:7001;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

---

## 15. Testy

### 15.1 Struktura testów

Testy jednostkowe znajdują się w projekcie `DevHunt.AuthService.Tests`.

**Framework:** xUnit + FluentAssertions + Moq

**Plik testowy:** `AuthControllerTests.cs` (315 linii, 5 testów)

### 15.2 Architektura testów

Testy korzystają z:
- **InMemory Database** (`UseInMemoryDatabase`) — izolacja danych między testami (każdy test ma unikalne DB)
- **Moq** — mockowanie serwisów (AuthServicesFacade, RefreshTokenService, RegistrationService, LoginService)
- **FluentAssertions** — czytelne asercje

**Setup (konstruktor):**

```csharp
public AuthControllerTests()
{
    _dbContext = CreateInMemoryDbContext();          // Unique DB per test
    _configuration = CreateConfiguration();          // In-memory config
    _mockRefreshTokenService = CreateRefreshTokenServiceMock();
    _mockRegistrationService = CreateRegistrationServiceMock();
    _mockLoginService = CreateLoginServiceMock();
    _mockAuthServices = CreateAuthServicesFacadeMock();  // Pełna hierarchia mocków
    _controller = CreateController();
}
```

### 15.3 Pokrycie testowe

| Test | Opis | Co sprawdza |
|------|------|-------------|
| `Register_ShouldCreateUserAndSendVerificationEmail` | Rejestracja nowego użytkownika | Wywołanie ValidateRegistrationAsync + CreateUserAsync |
| `Login_ShouldReturnAccessAndRefreshTokens` | Logowanie z poprawnymi danymi | Access token + refresh token w odpowiedzi |
| `Refresh_WithValidToken_ShouldReturnNewTokens` | Odświeżanie z ważnym tokenem | Nowe tokeny, rotacja starego |
| `Refresh_WithInvalidToken_ShouldReturnUnauthorized` | Odświeżanie z nieprawidłowym tokenem | HTTP 401 Unauthorized |
| `Logout_ShouldRevokeRefreshToken` | Wylogowanie | Rewokacja tokena, wywołanie RevokeRefreshTokenAsync |

### 15.4 Uruchomienie testów

```bash
# Wszystkie testy Auth Service
dotnet test DevHunt.AuthService.Tests

# Verbose output
dotnet test DevHunt.AuthService.Tests --verbosity detailed

# Konkretny test
dotnet test DevHunt.AuthService.Tests --filter "Register_ShouldCreateUserAndSendVerificationEmail"
```

---

## 16. Zmienne środowiskowe

| Zmienna | Wymagana | Opis | Przykład |
|---------|----------|------|---------|
| `ConnectionStrings__DefaultConnection` | ✅ | PostgreSQL connection string | `Host=db;Database=devhunt;...` |
| `Jwt__Key` | ✅ | Klucz podpisu JWT (min. 32 znaki) | `super-secret-key-min-32-chars-long!` |
| `Jwt__Issuer` | ✅ | Wydawca JWT | `DevHunt.AuthService` |
| `Jwt__Audience` | ✅ | Odbiorca JWT | `DevHunt.CoreApi` |
| `Jwt__ExpirationMinutes` | ❌ | Czas życia access token | `30` |
| `Cors__AllowedOrigins__0` | ✅ | Dozwolone originy CORS | `http://localhost:3000` |
| `OAuth__GitHub__ClientId` | ❌ | GitHub OAuth Client ID | `Iv1.abc123...` |
| `OAuth__GitHub__ClientSecret` | ❌ | GitHub OAuth Client Secret | `ghs_...` |
| `OAuth__Google__ClientId` | ❌ | Google OAuth Client ID | `...apps.googleusercontent.com` |
| `OAuth__Google__ClientSecret` | ❌ | Google OAuth Client Secret | `GOCSPX-...` |
| `Smtp__Host` | ❌ | Host SMTP | `smtp.gmail.com` |
| `Smtp__Port` | ❌ | Port SMTP | `587` |
| `Smtp__Username` | ❌ | Login SMTP | `noreply@devhunt.pl` |
| `Smtp__Password` | ❌ | Hasło SMTP (App Password) | `xxxx xxxx xxxx xxxx` |
| `Smtp__FromEmail` | ❌ | Adres nadawcy | `noreply@devhunt.pl` |
| `Smtp__FromName` | ❌ | Nazwa nadawcy | `DevHunt` |
| `Redis__ConnectionString` | ❌ | Redis dla rate limiting | `cache-service:6379` |
| `AZURE_KEY_VAULT_URI` | ❌ | URI Azure Key Vault (prod) | `https://devhunt-kv.vault.azure.net/` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | ❌ | Endpoint OpenTelemetry | `http://openobserve:4317` |
| `ASPNETCORE_URLS` | ❌ | URL serwisu | `http://+:7001` |
| `ASPNETCORE_ENVIRONMENT` | ❌ | Środowisko | `Development` / `Production` |

---

> **Następny dokument:** [Frontend (Next.js 16)](frontend.md)
