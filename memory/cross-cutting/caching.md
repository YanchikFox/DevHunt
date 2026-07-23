---
title: Caching — ICacheService, IDistributedCache, Redis vs in-memory
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Services/CacheService.cs
  - DevHunt.CoreApi/Extensions/InfrastructureExtensions.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

The shared caching layer for Core API. Two separate cache
abstractions coexist: `ICacheService` (the application-level
cache used by controllers and services) and `IMemoryCache`
(the framework-level in-process cache used by middleware and
certain services). They are independent — one being available
does not imply the other is backed by Redis.

## Components

| Class/Interface | Lifetime | Role |
|---|---|---|
| `ICacheService` / `CacheService` | Scoped | application cache: get/set/remove/pattern-delete |
| `IDistributedCache` | Singleton | .NET abstraction backing `CacheService` and `PresenceService` |
| `IConnectionMultiplexer` | Singleton | optional; required for `RemoveByPatternAsync` only |
| `IMemoryCache` | Singleton | in-process cache used by `MaintenanceModeMiddleware`, `CachedFeatureFlagService`, `AiRateLimiter` |

## How requests flow through it

```
AddRedisCache(configuration) → InfrastructureExtensions.cs:19
  if Features:Redis:Enabled AND RedisConnection set:
      AddStackExchangeRedisCache → IDistributedCache = Redis
      AddSingleton<IConnectionMultiplexer>
        (AbortOnConnectFail=false, ConnectRetry=3, ConnectTimeout/SyncTimeout=5s)
  else:
      AddDistributedMemoryCache → IDistributedCache = in-memory

AddMemoryCache() → Program.cs:218 → IMemoryCache = in-memory (always)
```

`CacheService` injects `IDistributedCache` (may be Redis or
in-memory) and optionally `IConnectionMultiplexer` (null when
Redis is absent).

## ICacheService contract

- `GetAsync<T>(key)` — deserializes JSON from distributed cache;
  returns `null` on miss or failure (fail-safe)
- `SetAsync<T>(key, value, expiration?)` — serializes to JSON;
  default TTL is **30 minutes**; silently skips on failure
- `RemoveAsync(key)` — removes single key; silently skips on failure
- `RemoveByPatternAsync(pattern)` — SCAN + delete via direct
  `IConnectionMultiplexer` (NOT via `IDistributedCache`);
  silently skips with LogWarning when `IConnectionMultiplexer`
  is null (i.e. no Redis)

## IMemoryCache vs ICacheService — which to use

`IMemoryCache` is per-process only, never distributed. It is
used by:
- `MaintenanceModeMiddleware` — platform maintenance flag (30s TTL)
- `CachedFeatureFlagService` — feature flags (30s TTL)
- `AiRateLimiter` — per-user request rate in AI flow

`ICacheService` / `IDistributedCache` is used for:
- Project, user profile caches in controllers (keyed by entity ID)
- `PresenceService` — online status (2 min sliding TTL)

**Do not use `IMemoryCache` for new shared state** — it cannot
be invalidated across replicas. Inject `ICacheService` instead.

## Key naming conventions (observed from code)

No enforced prefix registry. Patterns seen in the codebase:
- `presence:{userId}:{source}` — PresenceService
- `platform:maintenance_mode` — MaintenanceModeMiddleware
- `feature_flag:{key}` / `feature_flags:all` — CachedFeatureFlagService
- `project:{id}` — typical controller pattern (not exhaustive)

## Known traps

- **`RemoveByPatternAsync` silently no-ops without Redis.**
  If `IConnectionMultiplexer` is null (in-memory fallback),
  the method logs a warning and returns. Callers that depend
  on cache invalidation by pattern (e.g. clearing all keys for
  a project on update) get a no-op in dev/non-Redis environments.
- **Two separate cache abstractions can get out of sync.** A
  value written through `ICacheService` is in `IDistributedCache`;
  a value written through `IMemoryCache` (e.g. maintenance flag)
  is in a completely separate store. Calls to `RemoveAsync` on
  `ICacheService` will not clear `IMemoryCache` entries, and
  vice versa.

## What I should NOT assume

- **`IConnectionMultiplexer` is optional.** `CacheService`
  accepts it as `IConnectionMultiplexer? redis = null` in its
  constructor. Without Redis, the multiplexer is null; only
  `RemoveByPatternAsync` is affected — all other operations
  still work via `IDistributedCache` (in-memory).
- **`AbortOnConnectFail=false` means the API starts even if
  Redis is unreachable.** The multiplexer will retry on first
  use. An absence of startup errors does not imply Redis is
  actually connected.
