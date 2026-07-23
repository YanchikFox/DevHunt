---
sidebar_position: 3
title: How To Debug The Backend
description: Debugging guide for Core API, Auth Service, EF Core, eventing, middleware, and observability.
sidebar_label: How To Debug The Backend
---

# How To Debug The Backend

> _If any detail here contradicts the code, trust the code — not this page._

Start with the layer that rejects the request before it reaches your code. Core API has several middleware gates before controllers run.

## First Checks

```bash
dotnet build DevHunt.CoreApi/DevHunt.CoreApi.csproj
dotnet build DevHunt.AuthService/DevHunt.AuthService.csproj
dotnet test DevHunt.CoreApi.Tests/DevHunt.CoreApi.Tests.csproj
dotnet test DevHunt.AuthService.Tests/DevHunt.AuthService.Tests.csproj
```

If the service is running, check health before chasing controller logic:

```bash
curl -fsS http://localhost:7001/health
curl -fsS http://localhost:7002/health
```

## Core API Request Rejection Order

For Core API, inspect failures in this order:

1. CORS.
2. file upload limits.
3. security headers and CSRF token generation.
4. JWT authentication.
5. authorization policy or role check.
6. IP rate limit.
7. active-user check.
8. maintenance mode.
9. MVC global CSRF validation.
10. controller/service logic.

The global CSRF filter validates mutating verbs. In Development, a request with no CSRF header and no CSRF cookie is allowed; production behavior is stricter.

## Auth Problems

Auth Service reads JWT from the `access_token` httpOnly cookie first and `Authorization` header second. If OAuth routes look missing, check whether provider client id and secret were configured at startup.

Auth Service and Core API have different CSRF/CORS setup. A token or CORS assumption from one service may not apply to the other.

## EF Core Problems

Core API and Auth Service both use `DevHuntDbContext`, but the host registration differs.

Important checks:

- migrations are run by `DevHunt.DatabaseMigrator`, not on API startup;
- Core API has `EnableDynamicJson()` in its database setup;
- Auth Service does not wire `EnableDynamicJson()`;
- `ReadOnlyConnection` exists, but `CreateReadContext()` is unused at the verified commit.

If data looks stale, do not assume read-replica routing. Most reads hit `DefaultConnection`.

## Event Problems

If expected downstream work does not happen, first confirm eventing is actually enabled:

- `Features:EventBus:Enabled` must be true;
- `RabbitMQ:ConnectionString` must be non-empty;
- Core API must register `OutboxEventBusDecorator` and `OutboxEventProcessorWorker`.

When eventing falls back to `NoOpEventBusService`, publish calls do not throw. There will also be no outbox row to inspect.

Outbox rows move from `Pending` to `Completed` or `Failed`. `Processing` exists in the status model but is not written by the verified worker.

## Logs, Traces, And Metrics

Core API emits:

- Serilog logs to console and optionally OpenObserve HTTP sink;
- OpenTelemetry traces to OpenObserve OTLP endpoint;
- Prometheus metrics at `/metrics`.

`/metrics` is guarded by private/loopback IP checks or `X-Metrics-Token`, not by a normal auth policy.

Two observability traps matter while debugging:

- `LogEnrichmentMiddleware` runs before authentication, so its `UserId` field is always anonymous.
- metrics path normalization is incomplete; routes with IDs can create high-cardinality metrics.

## Real-Time Problems

SignalR hubs are mounted at `/chatHub` and `/notificationHub`. Nginx forwards WebSocket upgrades only on those paths.

Without Redis backplane, group fan-out is local to one Core API process. If messages are intermittent only under multiple replicas, check Redis and `Features:Redis:Enabled` before changing hub code.
