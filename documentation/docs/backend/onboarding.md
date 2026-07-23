---
sidebar_position: 0
title: Backend Onboarding
description: Orientation for backend engineers joining the DevHunt codebase.
---

# Backend Onboarding

> _If any detail here contradicts the code, trust the code — not this page._

Use this path to get productive without reading everything at once.

## Mental Model

The backend is four projects, not one service:

| Project | Role |
|---|---|
| `DevHunt.CoreApi` | Main product API — projects, tasks, chat, AI, badges, showcase, admin |
| `DevHunt.AuthService` | Identity only — JWT, OAuth, refresh tokens, TOTP, account lifecycle |
| `DevHunt.Infrastructure` | Shared library — `DevHuntDbContext`, EF entities, migrations |
| `DevHunt.DatabaseMigrator` | One-shot console app — runs `Database.MigrateAsync()` and exits |

Core API and Auth Service are separate processes that share `DevHuntDbContext` through `DevHunt.Infrastructure`, but each registers it differently — Core API wires `EnableDynamicJson()`, Auth Service does not.

## First Map

| Area | Where to start |
|---|---|
| Core API boot | `DevHunt.CoreApi/Program.cs` |
| Auth Service boot | `DevHunt.AuthService/Program.cs` |
| EF Core entities | `DevHunt.Infrastructure/DevHuntDbContext.cs` |
| Migrations | `DevHunt.Infrastructure/Migrations/` |
| Compose wiring | `docker-compose.yml` |
| Secrets layout | `docker-compose.override.yml.example` |

## First Commands

Build and run tests before touching anything:

```bash
dotnet build DevHunt.CoreApi/DevHunt.CoreApi.csproj
dotnet build DevHunt.AuthService/DevHunt.AuthService.csproj
dotnet test DevHunt.CoreApi.Tests/DevHunt.CoreApi.Tests.csproj
dotnet test DevHunt.AuthService.Tests/DevHunt.AuthService.Tests.csproj
```

Run the full stack through Docker (see [How To Run](./how-to-run) for details):

```bash
cp docker-compose.override.yml.example docker-compose.override.yml
docker compose up -d db cache-service message-broker object-storage db-migrator
docker compose up -d auth-service core-api
```

After boot, verify:

```bash
curl -fsS http://localhost:7001/health   # Auth Service
curl -fsS http://localhost:7002/health   # Core API
```

## Request Rejection Order

When a Core API request fails, work through these layers in order before reaching controller logic:

1. CORS
2. File upload size limit
3. Security headers / CSRF token generation
4. JWT authentication
5. Authorization policy or role check
6. IP rate limit
7. Active-user check (reads DB on every authenticated request)
8. Maintenance mode
9. Global CSRF filter (validates POST/PUT/DELETE/PATCH)
10. Controller / service logic

Rate limiting intentionally runs before the active-user DB check so brute-force attempts against inactive accounts are still throttled.

## Coding Standards

- Await event publication after every durable state change. Fire-and-forget breaks the reliability model.
- URL fields from users must go through `SecurityHelpers.IsValidUrl`.
- Rich-text fields must go through `SecurityHelpers.SanitizeHtml`.
- Use role constants, not hard-coded role strings, in authorization checks.
- Do not call `CreateReadContext()` — it exists in config but is unused. All reads hit `DefaultConnection`.

## Good First Debug Targets

- Follow a request through the Core API middleware pipeline: `Program.cs` → `LogEnrichmentMiddleware` → `UserActiveCheckMiddleware`.
- Add an EF Core migration to understand the migrator flow (`dotnet ef migrations add` from `DevHunt.Infrastructure`).
- Trace an event: controller → `IEventBusService` → `OutboxEvents` table → `OutboxEventProcessorWorker` → RabbitMQ.
- Compare Auth Service and Core API CSRF setup — they differ.
- Check what happens when `RabbitMQ:ConnectionString` is empty (Core API falls back to `NoOpEventBusService`).
