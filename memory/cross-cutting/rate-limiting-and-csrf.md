---
title: Rate Limiting & CSRF — IP rate limiting and anti-forgery token flow
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Extensions/RateLimitingExtensions.cs
  - DevHunt.CoreApi/Middleware/CsrfTokenMiddleware.cs
  - DevHunt.CoreApi/Filters/ValidateCsrfAttribute.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

Two independent protection layers that apply on top of JWT
authentication: IP-based request rate limiting (defence
against brute-force and abuse) and CSRF token validation
(defence against cross-site state-mutation).

## Components

| Class | Location | Role |
|---|---|---|
| `RateLimitingExtensions.AddDistributedRateLimiting` | Extensions/RateLimitingExtensions.cs | wires AspNetCoreRateLimit stores |
| `IpRateLimitMiddleware` | AspNetCoreRateLimit package | middleware that enforces rules; registered at step 12 of pipeline |
| `CsrfTokenMiddleware` | Middleware/CsrfTokenMiddleware.cs | per-request: generates token + sets cookie |
| `GlobalCsrfValidationFilter` | Filters/ValidateCsrfAttribute.cs:79 | IAsyncAuthorizationFilter; validates token on mutating verbs |
| `ValidateCsrfAttribute` | Filters/ValidateCsrfAttribute.cs:22 | opt-in per-action attribute (subset of global filter logic) |

## Rate limiting

### Configuration

Library: `AspNetCoreRateLimit` (`IpRateLimitMiddleware`).
Rules configured via `IpRateLimiting` section of
`appsettings.json`. Limit rules (periods, limits, endpoints)
live entirely in config — not visible in code.

Backend selection (conditional):
```
if Features:Redis:Enabled AND RedisConnection set:
    IIpPolicyStore        → DistributedCacheIpPolicyStore
    IRateLimitCounterStore → DistributedCacheRateLimitCounterStore
else (Development / Redis absent):
    IIpPolicyStore        → MemoryCacheIpPolicyStore
    IRateLimitCounterStore → MemoryCacheRateLimitCounterStore
```
`AsyncKeyLockProcessingStrategy` is registered in both paths.

### Pipeline position

Step 12 — **after** `UseAuthentication` (step 10) and
`UseAuthorization` (step 11), **before** `UserActiveCheckMiddleware`
(step 13). This order is intentional (comment at Program.cs:386):
rate-limiting fires before the active-user DB check so
brute-force attempts on inactive accounts do not bypass the limiter.

### Distributed vs in-memory

- [gotchas/rate-limit-counters-per-instance-without-redis.md](../gotchas/rate-limit-counters-per-instance-without-redis.md) —
  without Redis, each replica has independent counters;
  effective limit is N × config per window.

## CSRF

### Token flow

```
1. GET /api/csrf-token (or any request)
   → CsrfTokenMiddleware (step 9):
       IAntiforgery.GetAndStoreTokens(context)
       → sets CSRF-TOKEN cookie (HttpOnly=true, name from AddAntiforgery)
       → sets XSRF-REQUEST-TOKEN cookie (HttpOnly=false, SameSite=Strict, 2h)
         (this is the value JS reads and sends as a header)

2. Client POST/PUT/DELETE/PATCH
   → sends X-CSRF-TOKEN header with value from XSRF-REQUEST-TOKEN cookie

3. GlobalCsrfValidationFilter (IAsyncAuthorizationFilter, step in MVC pipeline)
   → antiforgery.ValidateRequestAsync(context)
     validates header matches CSRF-TOKEN cookie (Double Submit Cookie)
```

### Antiforgery configuration (Program.cs:252-260)

- `HeaderName`: `X-CSRF-TOKEN`
- `Cookie.Name`: `CSRF-TOKEN` (HttpOnly=true, SameSite=Lax,
  SecurePolicy=SameAsRequest)
- User identity binding suppressed (`SuppressXFrameOptionsHeader=false`)

### Exempt paths (GlobalCsrfValidationFilter:86-96)

Hardcoded `ExemptPaths` set (case-insensitive prefix match):
```
/api/auth/login
/api/auth/register
/api/auth/refresh
/api/auth/github-callback
/api/auth/forgot-password
/api/auth/reset-password
/api/integrations/callback
/api/webhooks
```
Any path containing `/webhooks/` is also exempt (signature-based
validation on those endpoints).

### Development mode behavior

`GlobalCsrfValidationFilter` checks `IsDevelopment()`. If no
`X-CSRF-TOKEN` header AND no `CSRF-TOKEN` cookie is present →
**allows the request** with a `LogDebug` warning. This means
Postman/curl works in dev without a CSRF token.

### "Different claims-based user" edge case

If the CSRF token was issued for a different authenticated
identity (e.g. after login/logout cycle), ASP.NET Core's
antiforgery throws a "different claims-based user" message.
`GlobalCsrfValidationFilter` catches this specific error,
**allows the request** (JWT validates the identity anyway),
and regenerates fresh tokens in the response.

## Known traps

- **Two cookie names for CSRF.** The validation cookie is
  `CSRF-TOKEN` (HttpOnly — JS cannot read). The cookie JS
  reads is `XSRF-REQUEST-TOKEN` (not HttpOnly). Mixing the
  two names in code or tests will cause silent 403 failures.
- **Rate limiting is in-memory by default in Development.**
  Changing `Features:Redis:Enabled=true` in `appsettings.Development.json`
  switches to Redis-backed counters; forgetting this means
  dev rate-limit behaviour does not reflect production.

## What I should NOT assume

- **Rate limit rules are not visible in this entry.** The
  actual limits (requests/minute, endpoint patterns, whitelist
  IPs) are in `IpRateLimiting` config — read `appsettings.json`
  directly if you need specific thresholds.
- **`ValidateCsrfAttribute` and `GlobalCsrfValidationFilter`
  are separate but overlapping.** The global filter covers
  all controllers. The per-action attribute is an opt-in
  supplement. Both call `antiforgery.ValidateRequestAsync` —
  if both apply to the same action, it validates twice (no
  functional difference, just redundant).
