---
title: DevHunt.Infrastructure — shared data + EF Core library
type: system
status: verified
sources:
  - DevHunt.Infrastructure/DevHunt.Infrastructure.csproj
  - DevHunt.Infrastructure/DevHuntDbContext.cs
  - DevHunt.Infrastructure/Migrations/
  - DevHunt.AuthService/DevHunt.AuthService.csproj
  - DevHunt.CoreApi/DevHunt.CoreApi.csproj
  - DevHunt.DatabaseMigrator/DevHunt.DatabaseMigrator.csproj
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Class library (`Microsoft.NET.Sdk`, **net10.0**, `OutputType` library) —
not a deployable service. Holds the EF Core `DevHuntDbContext`,
all entity types, all migrations, and shared infrastructure helpers
(`EnvLoader`, `DesignTimeDbContextFactory`,
`ReadWriteDbContextFactory`).

Referenced via `<ProjectReference>` from:
- `DevHunt.AuthService` — uses `DevHuntDbContext` for refresh tokens, users, roles.
- `DevHunt.CoreApi` — uses `DevHuntDbContext` for everything else.
- `DevHunt.DatabaseMigrator` — uses it solely to call `MigrateAsync`.

## "Entry-point"

There is no runtime entry-point. The closest equivalent is
[DevHunt.Infrastructure/DevHuntDbContext.cs:29](DevHunt.Infrastructure/DevHuntDbContext.cs#L29) —
the public class everyone instantiates.

A `DesignTimeDbContextFactory` is also provided so `dotnet ef
migrations add` can construct the context outside a host.

## What this project owns

- **EF Core mappings** for ≈45 aggregates (User, Project, TaskItem,
  TeamMember, Invitation, Conversation, Message, Skill,
  ShowcaseProject, ProjectFile, ProjectDocument, ActivityRecord,
  Recommendation, Integration, Achievement, RefreshToken,
  OutboxEvent, …). Full catalogue belongs in
  `data/entity-catalog.md` (Step 3).
- **Migrations** — `Migrations/` holds files dated through
  `20260428120000_AddAiMessageTransparency` at this commit.
- **Configuration helpers** — `Configuration/` (e.g.,
  `EnvLoader`).
- **Read/write DbContext factory**
  (`ReadWriteDbContextFactory`) used by Core API to switch between
  `DefaultConnection` and `ReadOnlyConnection`.

## NuGet surface

Only two: `Npgsql.EntityFrameworkCore.PostgreSQL` and
`Microsoft.EntityFrameworkCore.Design` (private/build-time). All
other persistence-adjacent packages live in the consuming projects.

## What I should NOT assume

- **The XML doc-comment at
  [DevHunt.Infrastructure/DevHuntDbContext.cs:6](DevHunt.Infrastructure/DevHuntDbContext.cs#L6) is wrong.**
  It claims "Automatic migrations on startup" and "Seed data for
  development." Neither is true today: both Core API and Auth
  Service explicitly *removed* the `ApplyMigrationsAsync` call
  (see comments at `DevHunt.CoreApi/Program.cs:338` and
  `DevHunt.AuthService/Program.cs:336`); migrations now run only
  via `DevHunt.DatabaseMigrator`. Trust the structural code, not
  the prose.
- **`OnConfiguring` only suppresses one warning** — the
  `PendingModelChangesWarning` (line 38). It does not configure
  the connection; that happens at the consumer's
  `AddDbContext`/`UseNpgsql` call.
- **`EnableDynamicJson` is not set here.** Per the auto-memory
  note (`memory/project_npgsql_jsonb.md` at the user level),
  jsonb columns with complex CLR types require
  `EnableDynamicJson` on the `NpgsqlDataSource`. That setup
  lives in the consumer's DI extension (likely
  `DevHunt.CoreApi/Extensions/AddDatabaseContext`), not here. If
  jsonb regressions appear, look there first.
