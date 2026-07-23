---
title: Feature Flags — DB-backed flags, IMemoryCache, admin toggle
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Services/FeatureFlagService.cs
  - DevHunt.CoreApi/Controllers/SuperAdminController.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

A lightweight feature-flag system backed by the `FeatureFlags`
DB table. Flags gate UI behaviour (surfaced via
`GET /api/feature-flags`) and can guard server-side code paths
via `IFeatureFlagService.IsEnabledAsync`. The cache layer
reduces DB reads but is in-process only — see Known traps.

## Components

| Class | Location | Lifetime | Role |
|---|---|---|---|
| `IFeatureFlagService` | Services/FeatureFlagService.cs:7 | — | abstraction |
| `CachedFeatureFlagService` | Services/FeatureFlagService.cs:19 | Scoped | DB read + IMemoryCache layer |
| `FeatureFlag` entity | Infrastructure (DbSet at DbContext:117) | — | persisted flag: Key + Enabled |

## How requests flow through it

```
Consumer → IFeatureFlagService.IsEnabledAsync(key)
  → IMemoryCache.TryGetValue("feature_flag:{key}")
      hit  → return cached bool (TTL up to 30 s)
      miss → db.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(key)
               found  → return flag.Enabled
               missing → return true   ← unknown flags default to ENABLED
             → IMemoryCache.Set("feature_flag:{key}", result, 30 s)

GET /api/feature-flags (Program.cs:409, RequireAuthorization)
  → IFeatureFlagService.GetAllAsync
      → IMemoryCache.TryGetValue("feature_flags:all")
          miss → db.FeatureFlags.AsNoTracking().ToDictionaryAsync()
               → IMemoryCache.Set("feature_flags:all", dict, 30 s)

SuperAdminController (line 474) → flag toggle → db SaveChanges
  → IFeatureFlagService.Invalidate(key)
      → IMemoryCache.Remove("feature_flag:{key}")
      → IMemoryCache.Remove("feature_flags:all")
```

## Cache keys

| Key | Content | TTL |
|---|---|---|
| `feature_flag:{key}` | single bool | 30 s absolute |
| `feature_flags:all` | `Dictionary<string,bool>` | 30 s absolute |

## Known traps

- [gotchas/feature-flag-cache-not-distributed.md](../gotchas/feature-flag-cache-not-distributed.md) —
  `CachedFeatureFlagService` uses `IMemoryCache` (per-process).
  `Invalidate()` called by `SuperAdminController` clears only
  the local replica's cache; other replicas serve stale values
  for up to 30 s.

## What I should NOT assume

- **Unknown flags default to `true` (enabled).** A flag key
  that does not exist in the DB is treated as enabled, not
  disabled. This means a freshly deployed feature that relies
  on a flag being off must have the flag row inserted before
  deployment, or it will be exposed immediately.
- **`Invalidate` is synchronous.** It removes from `IMemoryCache`
  directly. There is no async flush or wait. The next request
  will hit the DB and repopulate.
- **`CachedFeatureFlagService` is Scoped, but `IMemoryCache`
  is Singleton.** The cache state persists across requests on
  the same replica. The Scoped lifetime of the service does
  not limit the TTL of the cached values.
- **There is no watch or push mechanism.** The frontend calls
  `GET /api/feature-flags` to read flags; it must re-request
  to see changes. There is no SignalR push for flag updates.
