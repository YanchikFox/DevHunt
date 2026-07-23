---
title: DevHunt.AuthService — authentication / OAuth / JWT API
type: system
status: verified
sources:
  - DevHunt.AuthService/Program.cs
  - DevHunt.AuthService/DevHunt.AuthService.csproj
  - DevHunt.AuthService/Dockerfile
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

ASP.NET Core (`Microsoft.NET.Sdk.Web`, **net10.0**) Web API responsible for
authentication, OAuth flows, JWT issuance, refresh tokens, TOTP 2FA,
and email-based account lifecycle. Shares the `DevHuntDbContext` with
Core API via the `DevHunt.Infrastructure` project reference.

Compose service `auth-service`, container port `INTERNAL_PORT_AUTH`
(default 8080), host port `PORT_AUTH` (default 7001).

## Entry-point

[DevHunt.AuthService/Program.cs:24](DevHunt.AuthService/Program.cs#L24) —
`var builder = WebApplication.CreateBuilder(args);`

Top-of-file `EnvLoader.Load()` (line 23) is a custom helper from
`DevHunt.Infrastructure.Configuration` that loads `.env` files before
the configuration builder starts.

## Boot wiring (only what isn't obvious)

- **Configuration sources** (lines 27–60): `appsettings.json` →
  `appsettings.{Environment}.json` → environment variables. In
  Production with `KeyVault:Uri` set, also `AddAzureKeyVault`
  with `DefaultAzureCredential` and a 5-min reload interval.
  In non-Production, `AddUserSecrets<Program>`.
- **Tracing** (lines 66–110): OpenTelemetry → OTLP HTTP exporter
  pointed at `OpenObserve:OtlpTracesEndpoint`. In Production it
  refuses to start if the OpenObserve password is missing,
  shorter than 32 chars, equal to `ChangeMe123!`, or contains
  `default`/`changeme` (lines 90–101). `/metrics` requests are
  filtered out of trace recording.
- **JWT** (lines 154–206): bearer scheme; in Production the JWT
  key must be ≥32 chars and may not contain weak patterns
  (`secret`, `default`, `changeme`, `123456`, `password`).
  Token is read first from the `access_token` httpOnly cookie,
  with the `Authorization` header as fallback.
- **OAuth providers** (lines 208–231): Google and GitHub are
  registered **only when** their `ClientId` and `ClientSecret`
  are configured. Callback paths are
  `/api/auth/callback/google` and `/api/auth/callback/github`.
- **Rate limiting** (lines 236–259): `AspNetCoreRateLimit` with
  Redis-backed `DistributedCacheIpPolicyStore`/
  `DistributedCacheRateLimitCounterStore` when
  `RedisConnection` is set; otherwise falls back to in-memory
  stores (suitable for dev/test only).
- **Application services** (lines 262–274): `RefreshTokenService`,
  `MailKitEmailService`, `AuthValidationService`,
  `JwtTokenService`, `AuthCookieService`, `OAuthService`,
  `RegistrationService`, `LoginService`, `TotpService`
  (singleton), plus three sub-facades (`TokenServices`,
  `UserAuthServices`, `AuthServicesFacade as IAuthServices`)
  introduced to reduce constructor over-injection.
- **CSRF** (lines 277–290): cookie `CSRF-TOKEN` (HttpOnly,
  `SameSite=Strict`), header `X-CSRF-TOKEN`. Global filter
  `AuthCsrfValidationFilter` is registered on every controller.
- **Health check** (lines 293–299): `AddNpgSql` only if
  `DefaultConnection` is non-empty.
- **Swagger** (lines 301–349): bearer-only security definition,
  registered globally; UI exposed only in Development/Staging.

## App pipeline (lines 362–374)

`LogEnrichmentMiddleware` → `MetricsMiddleware` → `UseIpRateLimiting`
→ `UseAuthentication` → `UseAuthorization` → `MapControllers`.

## Endpoints (top-level)

- `POST/GET …` — controllers in `DevHunt.AuthService/Controllers/`
  (not enumerated here).
- `GET /api/auth/csrf-token` — anonymous; returns the antiforgery
  request token (lines 377–381).
- `GET /health` — anonymous (line 384).
- `GET /metrics` — anonymous, but gated to private/loopback IPs
  (`127.0.0.1`, `::1`, `172.*`, `10.*`) **or** to requests carrying
  the `X-Metrics-Token` header matching the `METRICS_TOKEN` env
  variable (lines 388–415).

## Outbound dependencies

- **PostgreSQL** via Npgsql (`ConnectionStrings:DefaultConnection`).
- **Redis** for rate limiting (`ConnectionStrings:RedisConnection`).
- **OpenObserve** for traces (and Serilog logs, configured in
  `LoggingExtensions.ConfigureSerilog`).
- **SMTP** via MailKit (`Email:*` configuration; default mailpit in
  compose).

No SignalR, no RabbitMQ, no S3 from this service.

## What I should NOT assume

- **No migrations on startup.** The explicit comment at lines
  336–338 says migrations are now run by
  `DevHunt.DatabaseMigrator`. Do not look for `MigrateAsync()` in
  this service's startup path.
- **CORS policy depends on environment.** In Development, headers
  and methods are unrestricted; in Production, only specific
  headers (`Content-Type`, `Authorization`, `X-Requested-With`,
  `X-CSRF-TOKEN`) and methods (`GET/POST/PUT/DELETE/OPTIONS` —
  no `PATCH` here, unlike Core API) are allowed.
- **Cookie security policy is `SameAsRequest`**, not `Always`
  (line 282) — meaning cookies are only marked `Secure` when the
  request is HTTPS. In dev over HTTP they will not be `Secure`.
