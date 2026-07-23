---
sidebar_position: 4
title: Backend Troubleshooting
description: Known backend failure modes and where to inspect them first.
sidebar_label: Backend Troubleshooting
---

# Backend Troubleshooting

> _If any detail here contradicts the code, trust the code — not this page._

Use this page as a first-pass triage map. It points to the likely subsystem before you open controller code.

## APIs Do Not Start

Check `db-migrator` first. Auth Service and Core API depend on it with `service_completed_successfully`. A failed migration job blocks both APIs.

If the migrator fails:

- confirm `POSTGRES_PASSWORD` is set;
- confirm `db` is healthy;
- inspect whether the exception is in the retry list (`NpgsqlException`, `TimeoutException`, `DbUpdateException`);
- do not delete `__EFMigrationsHistory` as a shortcut.

## Requests Return 403

For Core API, distinguish:

- authorization failure from `[Authorize]` or policies;
- inactive user failure from `UserActiveCheckMiddleware`;
- CSRF failure from the global MVC filter;
- maintenance-mode failure for non-admin users.

Development can allow missing CSRF token/cookie. Production should not.

## Requests Return 429

Core API and Auth Service use `AspNetCoreRateLimit`. With Redis disabled or unreachable, counters are in-memory. Under multiple replicas, the effective limit becomes the configured limit multiplied by the replica count.

## Async Side Effects Missing

If emails, recommendations, integrations, or other event-driven side effects do not happen, verify event bus registration:

- `RabbitMQ:ConnectionString` is non-empty;
- `Features:EventBus:Enabled` is true;
- `OutboxEvents` rows are being written;
- `OutboxEventProcessorWorker` is running.

No RabbitMQ means `NoOpEventBusService`, not a thrown exception.

## Chat Or Notifications Are Intermittent

Check Redis backplane configuration before editing hub code. Without Redis SignalR backplane, group membership and broadcasts are process-local.

Also remember hub connections require JWT authorization but do not repeat the HTTP active-user middleware check.

## Uploads Fail But Core API Is Healthy

Core API bucket initialization is non-fatal. A healthy API can still fail uploads if SeaweedFS or bucket setup is broken.

Also check upload size limits: 10 MB total multipart body and 4 MB per field.

## Logs Look Anonymous

`LogEnrichmentMiddleware` runs before authentication. Its `UserId` value is always anonymous unless later controller/service code adds a more specific log scope.
