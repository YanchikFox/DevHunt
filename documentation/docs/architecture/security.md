---
sidebar_position: 5
title: Security
description: Current security layers for gateway, Auth Service, Core API, CSRF, rate limiting, and metrics.
sidebar_label: Security
---

# Security

> _If any detail here contradicts the code, trust the code — not this page._

DevHunt security is layered across the edge gateway, Auth Service, Core API middleware, controller filters, and service-level authorization checks. This page documents what is wired today, including caveats that matter during reviews.

## Edge Gateway

Nginx terminates TLS and routes traffic to frontend, Auth Service, and Core API.

Current HTTPS posture:

- TLS 1.2 and 1.3 only.
- HSTS header on HTTPS responses.
- `X-Frame-Options: DENY`.
- `X-Content-Type-Options: nosniff`.
- `X-XSS-Protection: 1; mode=block`.
- `Referrer-Policy: strict-origin-when-cross-origin`.
- `X-User-Id` is stripped before proxying to upstream services.

Important caveats:

- CSP is only a commented template in `nginx.conf`; it is not enforced by the gateway.
- Nginx does not perform rate limiting in the verified config.
- `/api/` intentionally does not forward WebSocket upgrade headers; SignalR must use `/chatHub` or `/notificationHub`.

## Authentication

Auth Service issues and validates JWT-related auth flows. In Production, startup rejects weak JWT keys shorter than 32 chars or containing weak patterns such as `secret`, `default`, `changeme`, `123456`, or `password`.

JWT extraction checks the `access_token` httpOnly cookie first, then falls back to the `Authorization` header.

OAuth providers are registered only when both client id and client secret are configured. Google and GitHub callback paths are `/api/auth/callback/google` and `/api/auth/callback/github`.

## CSRF

Auth Service and Core API both use anti-forgery token flows, but the Core API flow has two cookie names:

| Cookie/Header        | Purpose                                               |
| -------------------- | ----------------------------------------------------- |
| `CSRF-TOKEN`         | HttpOnly validation cookie created by antiforgery     |
| `XSRF-REQUEST-TOKEN` | JavaScript-readable cookie set by Core API middleware |
| `X-CSRF-TOKEN`       | request header the client sends on mutating requests  |

Core API's global CSRF filter validates POST/PUT/DELETE/PATCH controller actions. Exempt paths include login/register/refresh flows, password reset paths, integration callback paths, and webhook paths.

Development mode intentionally allows mutating requests with no CSRF header and no CSRF cookie, logging a debug warning instead. Do not use development behavior as a production security assertion.

The filter also allows a specific "different claims-based user" antiforgery edge case, relying on JWT validation and regenerating tokens.

## Core API Middleware Security Order

Security-relevant Core API middleware runs in this order:

1. CORS.
2. correlation id and log enrichment.
3. metrics.
4. upload validation.
5. security headers.
6. CSRF token generation.
7. authentication.
8. authorization.
9. IP rate limiting.
10. active-user check.
11. maintenance-mode check.
12. hubs and controllers.

Rate limiting intentionally runs before the active-user DB check so brute-force attempts against inactive accounts are still throttled.

## Rate Limiting

Core API and Auth Service use `AspNetCoreRateLimit`. Core API chooses Redis-backed stores when Redis is enabled and configured; otherwise it uses in-memory stores.

Without Redis, every replica has independent counters. Effective rate limit becomes the configured limit multiplied by the number of replicas.

Actual endpoint thresholds live in configuration, not in this page.

## Active User And Maintenance Mode

`UserActiveCheckMiddleware` runs for authenticated Core API HTTP requests. It reads the user row on every authenticated request, blocks inactive users with 403, and throttles `LastLogin` updates to once per 60 seconds.

`MaintenanceModeMiddleware` checks `PlatformSettings.Key="maintenance_mode"` and returns 503 for non-admin users when enabled. It always allows health, metrics, CSRF token endpoint, SignalR hubs, and admin/superadmin users.

Maintenance mode uses `IMemoryCache`, so the setting is per-process for up to 30 seconds in multi-replica deployments.

## Metrics Guard

Auth Service and Core API expose `/metrics`, but access is hand-rolled:

- allowed from private/loopback IP prefixes such as `127.0.0.1`, `::1`, `172.*`, and `10.*`;
- or allowed with an `X-Metrics-Token` header matching `METRICS_TOKEN`.

This is not an ASP.NET Core authorization policy. Proxy configuration that makes every request appear private can leak metrics.

## Review Hotspots

When reviewing security-sensitive code, check these details first:

- resource ownership before mutation, especially in project, team, task, chat, and integration paths;
- URL fields from users must pass `SecurityHelpers.IsValidUrl`;
- rich-text fields must pass `SecurityHelpers.SanitizeHtml`;
- role checks should use helper methods/constants rather than hard-coded role strings;
- event publication must be awaited after durable state changes;
- SignalR hub connections currently rely on JWT authorization and do not repeat the HTTP active-user middleware check.
