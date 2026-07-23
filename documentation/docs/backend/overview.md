---
sidebar_position: 1
title: Backend Overview
description: Source-derived overview of DevHunt backend services, data access, events, and runtime boundaries.
sidebar_label: Backend Overview
---

# Backend Overview

> _If any detail here contradicts the code, trust the code — not this page._

DevHunt backend work is split across two web APIs and one shared data library:

| Component                  | Runtime                 | Responsibility                                                                   |
| -------------------------- | ----------------------- | -------------------------------------------------------------------------------- |
| `DevHunt.CoreApi`          | ASP.NET Core / .NET 10  | Main business API, SignalR hubs, object storage, AI planning, outbox writer      |
| `DevHunt.AuthService`      | ASP.NET Core / .NET 10  | registration, login, OAuth, JWT, refresh tokens, TOTP, account lifecycle         |
| `DevHunt.Infrastructure`   | .NET class library      | `DevHuntDbContext`, entity classes, EF Core mappings, migrations, shared helpers |
| `DevHunt.DatabaseMigrator` | .NET console executable | one-shot EF migration job used before web services boot                          |

Core API owns most product behavior. Auth Service owns credentials and tokens. Both use `DevHuntDbContext`, but they configure their hosts independently.

## Core API Shape

Core API registers controllers for projects, tasks, chat, AI planning, showcase, badges, recommendations, moderation, support, admin, integrations, profiles, files, and feature flags.

Its request pipeline includes response compression, CORS, correlation id, log enrichment, metrics, upload validation, security headers, CSRF token generation, authentication, authorization, IP rate limiting, active-user checks, maintenance-mode checks, SignalR hubs, and controllers.

Important backend rule: business logic belongs in services. Controllers should map HTTP input/output and delegate.

## Auth Service Shape

Auth Service handles login/registration flows, OAuth provider registration, refresh-token rotation, TOTP, email verification, password reset, and JWT issuance.

OAuth providers are registered only when their client id and secret exist. Local boot success does not prove OAuth is available.

## Persistence Shape

`DevHunt.Infrastructure` owns the EF model. Core API registers `DevHuntDbContext` through a Npgsql data source with `EnableDynamicJson()`. Auth Service uses its own database setup and does not wire that dynamic JSON behavior.

`ReadWriteDbContextFactory` exists, but `CreateReadContext()` is not called at the verified commit. Most reads go to `DefaultConnection`.

Migrations are not run by Core API or Auth Service at startup. `DevHunt.DatabaseMigrator` runs `Database.MigrateAsync()` first, with retries, and Compose blocks both APIs until it completes successfully.

## Eventing Shape

When RabbitMQ is configured and `Features:EventBus:Enabled` is true, `IEventBusService` is backed by the outbox:

1. application code awaits `PublishAsync`;
2. `OutboxEventBusDecorator` writes an `OutboxEvents` row;
3. `OutboxEventProcessorWorker` polls and publishes to RabbitMQ exchange `devhunt.events`.

When RabbitMQ is missing or eventing is disabled, `IEventBusService` becomes `NoOpEventBusService`. Calls succeed but no row or message is written.

Do not use `_ = _eventBus.PublishAsync(...)`; it bypasses the reliability expectations around awaited state changes.

## Backend Risk Map

- Redis must be configured before relying on distributed rate limits or SignalR fan-out.
- Outbox workers can double-publish under multi-replica Core API because `Processing` status is declared but not written and no row lock is used.
- `UserActiveCheckMiddleware` reads the user row on every authenticated Core API HTTP request.
- `LogEnrichmentMiddleware` runs before authentication, so its user id value is anonymous.
- Maintenance mode and feature-flag cache invalidation are per-process in multi-replica deployments.
