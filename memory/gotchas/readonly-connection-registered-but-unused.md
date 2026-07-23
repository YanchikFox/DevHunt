---
title: ReadOnlyConnection registered but CreateReadContext() never called
type: gotcha
status: verified
sources:
  - DevHunt.Infrastructure/ReadWriteDbContextFactory.cs
  - DevHunt.CoreApi/Program.cs
  - DevHunt.CoreApi/Services/Ai/DbAiUsageMeter.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## The trap

`ReadWriteDbContextFactory` is registered as a singleton
(Program.cs:216) and exposes two methods:

- `CreateWriteContext()` → `DefaultConnection` (primary)
- `CreateReadContext()` → `ReadOnlyConnection` if set,
  fallback to `DefaultConnection` with `NoTracking`

At this commit, **`CreateReadContext()` is never called
anywhere in the codebase**. The only consumer of the
factory is `DbAiUsageMeter`, which calls `CreateWriteContext()`
twice. All controllers inject `DevHuntDbContext` directly and
always hit the primary.

`ReadOnlyConnection` is defined in `appsettings.json` and
`appsettings.Development.json` but the value is not routed
anywhere at runtime.

## Why this matters

A developer who sees `ReadWriteDbContextFactory` and
`ReadOnlyConnection` in appsettings may assume that read
traffic is already being load-balanced to a replica. It is not.
Every EF query — including paged lists, search, and feed
assembly — hits the primary.

If a read replica is added to the infrastructure and
`ReadOnlyConnection` is pointed at it, no queries will
automatically shift until code is changed to call
`CreateReadContext()` at the relevant call sites.

## Safe baseline

Single-node Postgres (compose default): no impact — both
connection strings point to the same host.

Multi-node with a replica: configure `ReadOnlyConnection`
AND audit which services should use `CreateReadContext()`
before deploying. The factory exists; the call sites do not.
