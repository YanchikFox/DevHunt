---
title: Middleware Pipeline — request processing order in Core API
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Program.cs
  - DevHunt.CoreApi/Middleware/CorrelationIdMiddleware.cs
  - DevHunt.CoreApi/Middleware/LogEnrichmentMiddleware.cs
  - DevHunt.CoreApi/Middleware/MetricsMiddleware.cs
  - DevHunt.CoreApi/Middleware/FileUploadValidationMiddleware.cs
  - DevHunt.CoreApi/Middleware/SecurityHeadersMiddleware.cs
  - DevHunt.CoreApi/Middleware/CsrfTokenMiddleware.cs
  - DevHunt.CoreApi/Middleware/UserActiveCheckMiddleware.cs
  - DevHunt.CoreApi/Middleware/MaintenanceModeMiddleware.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

The ordered chain of ASP.NET Core middleware and terminal
endpoints in Core API. Every HTTP request (and WebSocket
upgrade) passes through this chain top-to-bottom. Knowing
the order is the fastest way to diagnose "why does this
request get a 4xx before hitting my controller."

## How requests flow through it

Steps in registration order (Program.cs):

```
1.  UseSwagger / UseSwaggerUI          [Dev+Staging only, ~L344]
    — /swagger UI; blocked in Production

2.  UseResponseCompression             [L353]
    — Brotli then Gzip; must be before any middleware
      that writes a response body

3.  UseCors                            [L356-363]
    — "DevCors": AllowAnyHeader/Method + AllowCredentials
    — "ProductionCors": specific origins, headers, methods

4.  UseCorrelationId                   [L366] ← CorrelationIdMiddleware
    — reads X-Correlation-ID or X-Request-ID; generates GUID if absent
    — writes both headers back in response
    — sets HttpContext.Items["CorrelationId"] for controllers
    — tags Activity for OpenTelemetry

5.  LogEnrichmentMiddleware            [L368]
    — pushes TraceId + UserId to Serilog LogContext
    ⚠ runs BEFORE UseAuthentication → UserId is always
      "anonymous" here; see Known traps

6.  MetricsMiddleware                  [L371]
    — Stopwatch wraps all downstream; emits
      http_requests_total + http_request_duration_seconds
      after response is written
    — business counters for support tickets, feedback,
      project issues, avatar uploads (path-pattern matched)

7.  FileUploadValidationMiddleware     [L374]
    — enforces FormOptions limits: 10 MB body, 4 MB per field
    — rejects over-limit multipart before controllers see it

8.  SecurityHeadersMiddleware          [L377]
    — X-Content-Type-Options: nosniff
    — X-Frame-Options: DENY
    — X-XSS-Protection: 1; mode=block
    — Strict-Transport-Security: 1 year (HTTPS requests only)
    — Content-Security-Policy (API-safe, no nonce)
    — Referrer-Policy: strict-origin-when-cross-origin
    — Permissions-Policy: geolocation/mic/camera disabled

9.  UseCsrfToken                       [L380] ← CsrfTokenMiddleware
    — generates token via IAntiforgery.GetAndStoreTokens
    — sets XSRF-REQUEST-TOKEN cookie (HttpOnly=false,
      SameSite=Strict, 2h TTL) so JavaScript can read it
    — skips /health and /metrics paths

10. UseAuthentication                  [L383]
    — validates JWT; populates context.User

11. UseAuthorization                   [L384]
    — enforces [Authorize] policies and endpoint metadata

12. IpRateLimitMiddleware              [L388] ← AspNetCoreRateLimit
    — IP-based rate limiting; config section "IpRateLimiting"
    — Redis backend (prod) or in-memory (dev)
    — runs AFTER auth so authenticated user info is available
      to rate limit rules, but keying is still by IP

13. UserActiveCheckMiddleware          [L392]
    — only fires for authenticated requests
    — loads User row; 403 + "Account is blocked" if !IsActive
    — updates User.LastLogin (throttled to once/60 s)
    — failed LastLogin update is logged as warning, not fatal

14. MaintenanceModeMiddleware          [L396]
    — reads PlatformSettings.Key="maintenance_mode" (cached 30 s)
    — 503 JSON for non-admin users when maintenance is on
    — always allows: /health, /metrics, /api/csrf-token,
      /chatHub, /notificationHub, roles "admin"/"superadmin"
    — cache key: "platform:maintenance_mode"
      (use MaintenanceModeMiddleware.CacheKey to invalidate)

── Terminal endpoints (MapXxx) ──────────────────────────────────

15. MapHealthChecks("/health")         [L399] — AllowAnonymous
16. MapGet("/api/csrf-token")          [L402] — AllowAnonymous
17. MapGet("/api/feature-flags")       [L409] — RequireAuthorization
18. MapGet("/metrics")                 [L417] — custom guard:
    localhost IP ranges OR METRICS_TOKEN header; 403 otherwise

19. MapHub<ChatHub>("/chatHub")        [L446]
20. MapHub<NotificationHub>("/notificationHub") [L447]
21. MapControllers()                   [L449]
    — GlobalCsrfValidationFilter runs as IAsyncAuthorizationFilter
      on all POST/PUT/DELETE/PATCH (see rate-limiting-and-csrf.md)
```

## Known traps

- **`LogEnrichmentMiddleware` runs before `UseAuthentication`.**
  At step 5, `context.User` has not been populated by JWT
  middleware yet. The `UserId` pushed to `LogContext` is always
  "anonymous" regardless of whether the request carries a valid
  token. Controllers and services that add their own log scope
  will override this, but the enrichment from step 5 itself is
  always anonymous.

- **`UserActiveCheckMiddleware` does a DB read on every
  authenticated request.** It calls
  `db.Users.FirstOrDefaultAsync` unconditionally for any
  authenticated user. There is no cache in front of this check.
  Under high authenticated traffic this is a per-request primary
  DB read.

- [gotchas/maintenance-mode-cache-not-distributed.md](../gotchas/maintenance-mode-cache-not-distributed.md) —
  `MaintenanceModeMiddleware` uses `IMemoryCache` (per-process);
  toggle propagates to other replicas only after 30 s TTL expiry.

## What I should NOT assume

- **CORS is environment-keyed, not config-keyed.** The policy
  name ("DevCors" / "ProductionCors") switches on
  `IsDevelopment()` at startup — you cannot change it at
  runtime without a restart.
- **`/metrics` access control is hand-rolled**, not an ASP.NET
  Core auth policy. It checks `RemoteIpAddress` prefix
  (`127.0.0.1`, `::1`, `172.*`, `10.*`) or the `METRICS_TOKEN`
  env var. Misconfiguring the proxy so `RemoteIpAddress` is
  always a private IP leaks Prometheus data.
- **`FileUploadValidationMiddleware` limits apply before
  controllers** — a 10 MB+ multipart body never reaches the
  action method.
