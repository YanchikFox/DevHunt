---
sidebar_position: 2
title: Migrations
description: Source-derived EF Core migration process and timeline warnings.
sidebar_label: Migrations
---

# Migrations

> _If any detail here contradicts the code, trust the code — not this page._

EF Core migrations live in `DevHunt.Infrastructure/Migrations/`. They are applied by `DevHunt.DatabaseMigrator`, not by Core API or Auth Service startup.

## How Migrations Run

`DevHunt.DatabaseMigrator`:

- loads configuration from optional `appsettings.json`, environment variables, and command line;
- requires `ConnectionStrings:DefaultConnection`;
- creates `DevHuntDbContext` directly;
- calls `Database.MigrateAsync()`;
- retries up to 10 times for `NpgsqlException`, `TimeoutException`, and `DbUpdateException`;
- exits `0` on success and `1` on failure.

Compose uses `service_completed_successfully` so Auth Service and Core API wait for this job.

## Current Timeline Anchor

At the verified commit, the newest migration recorded in memory is:

```text
20260428120000_AddAiMessageTransparency
```

It creates `AiMessageDetails`, a one-to-one table for message transparency data with jsonb `FullPayloadJson`.

## Known Timeline Traps

- `ParticipantOnlySeed` deletes data despite its name suggesting seed creation.
- `MakeActivityProjectOptional` also patches specific user skill arrays.
- `AddAdminExtensions` and `EnsureAdminTables` split admin/platform table setup in a way the names do not fully explain.
- `EnsureAdminTables` uses raw `CREATE TABLE IF NOT EXISTS` SQL as an idempotency follow-up.
- filename timestamps are not commit times.

Open the migration file before planning rollback, squashing, or rebasing. Names are hints, not contracts.

## Operational Cautions

- Do not truncate `__EFMigrationsHistory` to force migrations.
- Do not hand-edit designer or snapshot files casually.
- Do not add application data cleanup to a schema migration unless the user explicitly accepts the rollback/restore implications.
- Test migrations against PostgreSQL with pgvector when vector or jsonb behavior matters.
