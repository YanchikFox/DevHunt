---
title: DevHunt.DatabaseMigrator — one-shot EF migrations job
type: system
status: verified
sources:
  - DevHunt.DatabaseMigrator/Program.cs
  - DevHunt.DatabaseMigrator/DevHunt.DatabaseMigrator.csproj
  - DevHunt.DatabaseMigrator/Dockerfile
  - DevHunt.DatabaseMigrator/appsettings.json
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Console executable (`Microsoft.NET.Sdk`, **net10.0**, `OutputType=Exe`)
that runs database migrations once and exits. Designed to be a
Compose init container or Kubernetes Job — the inline comment
(lines 10–21) calls this out and explains why per-service
auto-migration was removed (race conditions on parallel startup).

Compose service `db-migrator`. `restart: "no"`. `core-api` and
`auth-service` declare
`db-migrator: { condition: service_completed_successfully }`,
so they will not start until this job exits 0.

## Entry-point

[DevHunt.DatabaseMigrator/Program.cs:25](DevHunt.DatabaseMigrator/Program.cs#L25) —
`static async Task<int> Main(string[] args)`.

## Boot wiring

- `EnvLoader.Load()` (line 27) — same helper as the web services.
- Logging: console, minimum `Information` (lines 29–32).
- Configuration: `appsettings.json` (optional) → environment
  variables → command line (lines 38–43).
- Reads `ConnectionStrings:DefaultConnection`; exits 1 if missing
  (lines 45–50).
- Builds `DbContextOptions<DevHuntDbContext>` directly (no DI
  container) with Npgsql + the same logger factory (lines 55–57).

## Migration loop (lines 59–94)

- Up to **10 attempts**.
- On each attempt, instantiates a fresh `DevHuntDbContext` and
  calls `Database.MigrateAsync()`.
- Catches **only** `NpgsqlException`, `TimeoutException`, and
  `DbUpdateException` for the retry path. Other exceptions log
  and exit 1 immediately (lines 89–93).
- Backoff is exponential: `Math.Pow(2, attempt)` seconds, so
  delays are 2, 4, 8, 16, … up to attempt 10 (~512s).
- Returns 0 only after a successful `MigrateAsync` call;
  otherwise 1.

## Outbound dependencies

- **PostgreSQL** only.

No Redis, no RabbitMQ, no S3, no logging endpoint, no metrics, no
HTTP server.

## What I should NOT assume

- **The retry list does not cover every transient error.**
  PostgreSQL connection failures usually surface as
  `NpgsqlException`, but generic `IOException`/`SocketException`
  paths are not retried. If the migrator dies with a non-Npgsql
  exception, do not blindly add a retry — investigate root cause.
- **Migrations are not idempotent at the SQL level** (they're
  idempotent only via EF Core's `__EFMigrationsHistory` table).
  Don't truncate that table to "force a re-run" without
  coordinating with the user.
- **Compose has no health check** for this service (it isn't a
  long-running process). Dependents rely on
  `service_completed_successfully`, so a non-zero exit will keep
  Core API and Auth Service from booting at all.
