---
title: Persistence & EF Core — DbContext setup, RW/RO split, dynamic JSON
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Extensions/DatabaseExtensions.cs
  - DevHunt.Infrastructure/ReadWriteDbContextFactory.cs
  - DevHunt.CoreApi/Services/Ai/DbAiUsageMeter.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

How Core API connects to PostgreSQL: a single DI-scoped
`DevHuntDbContext`, a companion factory for explicit
RW/RO connection selection, and Npgsql-level configuration
that enables dynamic JSON serialization.

AuthService has its own separate connection setup — this
entry covers Core API only.

## Components

| Class | Location | Role |
|---|---|---|
| `DatabaseExtensions.AddDatabaseContext` | DatabaseExtensions.cs:15 | registers `DevHuntDbContext` via `NpgsqlDataSourceBuilder` |
| `DevHuntDbContext` | Infrastructure/DevHuntDbContext.cs | EF Core DbContext; 60 entities; 45-migration history |
| `ReadWriteDbContextFactory` | Infrastructure/ReadWriteDbContextFactory.cs | singleton; creates explicit RW/RO DbContext instances on demand |
| `DbAiUsageMeter` | Services/Ai/DbAiUsageMeter.cs | only actual consumer of `ReadWriteDbContextFactory.CreateWriteContext()` |

## How requests flow through it

```
Program.cs
  └─ AddDatabaseContext(configuration)
       ├─ NpgsqlDataSourceBuilder(DefaultConnection)
       │    └─ .EnableDynamicJson()          ← jsonb ↔ CLR types
       └─ services.AddDbContext<DevHuntDbContext>(options.UseNpgsql(dataSource))
            └─ Scoped lifetime — one context per HTTP request

Controller (typical path)
  └─ constructor-injects DevHuntDbContext
       └─ always connects to DefaultConnection (primary/write DB)

DbAiUsageMeter (atypical path)
  └─ constructor-injects ReadWriteDbContextFactory (Singleton)
       └─ _dbFactory.CreateWriteContext()
            └─ new DevHuntDbContext with DefaultConnection, no change tracking flag
```

## ReadWriteDbContextFactory — state at this commit

Registered at Program.cs:216 as a Singleton.
Exposes two methods:

- `CreateWriteContext()` — `DefaultConnection`, standard tracking.
- `CreateReadContext()` — `ReadOnlyConnection` if configured,
  fallback to `DefaultConnection`; `NoTracking` query behavior.

**`CreateReadContext()` is never called anywhere in the codebase
at this commit.** Only `DbAiUsageMeter` calls `CreateWriteContext()`,
and standard controllers inject `DevHuntDbContext` directly.
The RO path exists as infrastructure but is not exercised.

## EnableDynamicJson

Wired in `DatabaseExtensions.cs:21` only.
Enables Npgsql to map `jsonb` columns to/from complex CLR
types (e.g., `Dictionary<string,object>`, arbitrary POCOs)
without explicit `HasConversion`.

**This is NOT wired in AuthService.** AuthService configures
its own NpgsqlDataSource via `AuthDbContext` — no
`EnableDynamicJson()` there. Any jsonb column added to a table
shared by AuthService will not serialize correctly from that
connection.

## Configuration keys

| Key | Source | Purpose |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | appsettings / env | primary (write) DB |
| `ConnectionStrings:ReadOnlyConnection` | appsettings / env | read replica; factory falls back to DefaultConnection if absent |

## Known traps

- [gotchas/readonly-connection-registered-but-unused.md](../gotchas/readonly-connection-registered-but-unused.md) —
  `ReadWriteDbContextFactory.CreateReadContext()` is never called at
  this commit; `ReadOnlyConnection` config key is effectively dead.

## What I should NOT assume

- **Most reads go to the primary.** Despite `ReadWriteDbContextFactory`
  existing, injected `DevHuntDbContext` always uses `DefaultConnection`.
  Don't assume reads are replica-routed unless a service explicitly
  calls `CreateReadContext()`.
- **`ReadWriteDbContextFactory` is a Singleton but creates new
  DbContext instances each call.** Each `CreateWriteContext()` /
  `CreateReadContext()` call returns a freshly constructed
  `DevHuntDbContext` — it is not pooling contexts. Callers are
  responsible for disposing (`await using`).
- **`NoTracking` on read contexts is a query-time optimization,
  not a read-only connection.** A `CreateReadContext()` instance
  can still call `SaveChangesAsync` and it will write if the
  underlying connection string points to a writable host.
- **`ApplyConfigurationsFromAssembly` is in `OnModelCreating`.**
  Entity mappings in `Configuration/EntityConfigurations/*.cs`
  apply automatically; there is no explicit per-entity call to
  `modelBuilder.Entity<T>()` for those types. When adding a new
  entity, drop a config class in that folder — don't add an
  `OnModelCreating` block.
