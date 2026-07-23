---
sidebar_position: 2
title: Security Checklist
description: Operational security checklist based on current gateway, API, CSRF, rate limiting, secrets, and runtime caveats.
sidebar_label: Security Checklist
---

# Security Checklist

> _If any detail here contradicts the code, trust the code — not this page._

Use this checklist before exposing a DevHunt environment outside a trusted developer machine.

## Secrets And Startup

- Set `POSTGRES_PASSWORD`; Compose requires it for DB-backed services.
- Set `RABBITMQ_DEFAULT_PASS`; RabbitMQ and event consumers depend on it.
- Set `JWT_KEY` to at least 32 strong characters. Production Auth Service rejects weak JWT keys.
- Set `ENCRYPTION_KEY` and `ENCRYPTION_IV` with the exact lengths required by Core API configuration.
- Replace local/default OpenObserve credentials before production.
- Configure OAuth client ids/secrets only for providers that should be active.
- In Production, use Azure Key Vault only when `KeyVault:Uri` and identity credentials are intentionally configured.

## Edge Gateway

- Keep TLS termination at Nginx or an equivalent trusted edge.
- Confirm `X-User-Id` is stripped before upstream proxying.
- Do not expose backend container ports directly in production.
- Do not expose RabbitMQ management UI, Postgres, Redis, or SeaweedFS admin ports publicly.
- Remember CSP is not enforced by the verified Nginx config; the CSP line is commented out.
- Keep SignalR WebSocket paths at `/chatHub` and `/notificationHub`; `/api/*` does not forward WebSocket upgrades.

## API Protection

- Auth Service and Core API must share compatible JWT issuer/audience/key settings.
- Core API mutating controller actions need CSRF protection unless on an intentional exempt path.
- Do not treat Development CSRF behavior as production behavior; Development allows missing CSRF token/cookie for local tools.
- Rate limiting must use Redis-backed stores in multi-replica deployments.
- Metrics endpoints need either private/loopback source IPs or `X-Metrics-Token`; verify proxy `RemoteIpAddress` behavior.

## Data And Content Safety

- Validate user-provided URL fields with `SecurityHelpers.IsValidUrl` before assignment.
- Sanitize rich-text fields with `SecurityHelpers.SanitizeHtml`.
- Do not hard-code role strings in backend checks; use the security helpers.
- Do not use raw SQL unless it is parameterized and there is a concrete reason EF Core cannot express the query.
- Do not log encrypted BYOK values, access tokens, refresh tokens, or object-storage credentials.

## Eventing And Async Work

- Confirm RabbitMQ connection string is present and `Features:EventBus:Enabled` is true before depending on event side effects.
- Await `IEventBusService.PublishAsync` after saving durable state.
- Do not use fire-and-forget event publishing.
- Inspect `OutboxEvents` when async side effects are missing.
- Before scaling Core API workers, fix duplicate outbox publish risk with row locking or atomic status transitions.

## Real-Time And Scale-Out

- Enable Redis and `Features:Redis:Enabled=true` before depending on cross-replica SignalR fan-out.
- Treat presence as approximate; the presence TTL is 2 minutes sliding and there is no heartbeat write.
- Remember hub connections use JWT authorization but do not repeat the HTTP active-user middleware check.
- Fix process-local maintenance and feature-flag caches before relying on instant cross-replica toggles.

## Deployment Decision Gate

Do not call a deployment production-ready until these are true:

- no default development secrets remain;
- direct infrastructure ports are closed or restricted;
- Redis and RabbitMQ are healthy and intentionally configured;
- `/metrics` is not reachable from untrusted networks;
- object-storage public endpoint is intentional;
- Core API is either single-replica or known scale-out hazards are fixed.
