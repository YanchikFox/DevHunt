---
sidebar_position: 1
title: Database Overview
description: Source-derived database overview for EF Core, PostgreSQL, migrations, and entity catalog.
sidebar_label: Database Overview
---

# Database Overview

> _If any detail here contradicts the code, trust the code — not this page._

DevHunt's primary database is PostgreSQL with pgvector. The EF Core model lives in `DevHunt.Infrastructure`; Core API and Auth Service both reference it.

Use the generated [database ERD](../../architecture/generated/database-erd) for a visual map and [Glossary](../../reference/glossary) for concise entity definitions.

## Ownership

| Component                  | Role                                                                  |
| -------------------------- | --------------------------------------------------------------------- |
| `DevHunt.Infrastructure`   | `DevHuntDbContext`, entity classes, entity configurations, migrations |
| `DevHunt.CoreApi`          | main EF Core consumer; wires `EnableDynamicJson()`                    |
| `DevHunt.AuthService`      | identity EF Core consumer; separate DB setup                          |
| `DevHunt.DatabaseMigrator` | applies migrations with `Database.MigrateAsync()`                     |
| `ml-service`               | reads/writes through asyncpg, not EF Core                             |

## Entity Layout

Entity classes live in three places:

- `DevHunt.Infrastructure/*.cs`
- `DevHunt.Infrastructure/Models/*.cs`
- inline types inside other entity files, such as message details and project helper types

Mappings live partly in `DevHuntDbContext.OnModelCreating` and partly in `Configuration/EntityConfigurations/*`.

## Important Storage Details

- `TaskItem` is exposed through DbSet `Tasks`.
- Some configured table names use underscores, including `Activity_Records`, `Conversation_Participants`, and `Channel_Role_Definitions`.
- `Message.AiMetadataJson` and `AiMessageDetails.FullPayloadJson` are confirmed jsonb columns.
- Several other `*Json` fields are plain text JSON, not jsonb.
- `ModerationReport.TargetId` and `Notification.RelatedEntityId` are polymorphic ids, not real foreign keys.

## Read/Write Split

`ReadWriteDbContextFactory` can create write and read contexts. At the verified commit, only `CreateWriteContext()` is used by `DbAiUsageMeter`; `CreateReadContext()` is not called. Most reads hit `DefaultConnection`.

Do not assume read-replica routing is active just because `ReadOnlyConnection` exists.

## Migration Rule

APIs do not apply migrations at startup. `DevHunt.DatabaseMigrator` runs first and exits. Compose blocks Auth Service and Core API until it completes successfully.
