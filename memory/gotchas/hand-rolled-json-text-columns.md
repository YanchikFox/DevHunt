---
title: Several entities serialize JSON into plain `text` columns by hand — not jsonb
type: gotcha
status: verified
sources:
  - DevHunt.Infrastructure/Integration.cs
  - DevHunt.Infrastructure/Recommendation.cs
  - DevHunt.Infrastructure/ShowcaseProject.cs
  - DevHunt.Infrastructure/ProjectRole.cs
  - DevHunt.CoreApi/Extensions/DatabaseExtensions.cs
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review:
---

## The fact

Five entity properties hold structured data, but the column type
is plain `text` and the entity itself does the JSON
serialization on read/write via `System.Text.Json`:

- `Integration.ConfigJson` — wrapped by a `Config` property,
  [DevHunt.Infrastructure/Integration.cs:83-89](DevHunt.Infrastructure/Integration.cs#L83-L89).
- `Recommendation.ReasoningJson` — wrapped by `Reasoning`,
  [DevHunt.Infrastructure/Recommendation.cs:84-87](DevHunt.Infrastructure/Recommendation.cs#L84-L87).
- `ShowcaseProject.ScreenshotsJson` (string[]) and
  `ShowcaseProject.MetricsJson` (Dict<string,object>),
  [DevHunt.Infrastructure/ShowcaseProject.cs:103-120](DevHunt.Infrastructure/ShowcaseProject.cs#L103-L120).
- `ProjectRole.RequiredSkillsJson` (string[]),
  [DevHunt.Infrastructure/ProjectRole.cs:82-85](DevHunt.Infrastructure/ProjectRole.cs#L82-L85).

EF Core does not see these as JSON. PostgreSQL stores them as
`text`. Querying their internal structure from the database
requires `::jsonb` casts at SQL time, which EF will not generate.

## Don't confuse with the real jsonb columns

Two columns *are* mapped to PostgreSQL `jsonb` via
`HasColumnType("jsonb")` in `DevHuntDbContext.OnModelCreating`:

- `Message.AiMetadataJson`
  ([DevHuntDbContext.cs:233-234](DevHunt.Infrastructure/DevHuntDbContext.cs#L233-L234))
- `AiMessageDetails.FullPayloadJson`
  ([DevHuntDbContext.cs:254](DevHunt.Infrastructure/DevHuntDbContext.cs#L254))

Those work because `DevHunt.CoreApi/Extensions/DatabaseExtensions.cs:21`
calls `dataSourceBuilder.EnableDynamicJson()` before building the
data source. **AuthService does NOT call `EnableDynamicJson`** —
its `Program.cs:113-114` uses a plain `UseNpgsql(connectionString)`.
That is fine today only because AuthService never reads or writes
those two jsonb columns.

## How to spot it

- Anywhere you see `…Json` suffix on a property in an entity, peek
  at the file. If there's a sibling property of the structured
  type with `JsonSerializer.Serialize`/`Deserialize` in the
  getter/setter — it's the hand-rolled flavor.
- LINQ queries that try to filter on the inner structure compile,
  but EF translates them to client-side evaluation of the
  deserialized property — full table scan. If a query against
  these properties is suddenly slow, this is why.

## What to do (and not do)

- **Do not** add new hand-rolled `*Json` text columns. Use
  `HasColumnType("jsonb")` and let `EnableDynamicJson` do the
  work. Migrate existing ones to jsonb only after coordinating
  (the migration needs an explicit `USING …::jsonb` cast).
- **Before adding a new jsonb column to any entity touched by
  AuthService**, add `EnableDynamicJson()` to AuthService's
  `Npgsql` data-source builder too. Today AuthService bypasses
  this and would fail at runtime on a complex CLR type.
- **Do not "fix"** the existing five hand-rolled columns
  reflexively. They predate the jsonb pattern; conversion is a
  data migration, not a code refactor.
