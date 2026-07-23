---
title: MaintenanceModeMiddleware uses IMemoryCache — toggle lag up to 30s per replica
type: gotcha
status: verified
sources:
  - DevHunt.CoreApi/Middleware/MaintenanceModeMiddleware.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## The trap

`MaintenanceModeMiddleware` caches the `maintenance_mode`
platform setting in `IMemoryCache` with a 30-second TTL
(MaintenanceModeMiddleware.cs:22, `CacheDuration`).

`IMemoryCache` is in-process. Under multiple replicas, each
instance has its own independent cache. Toggling maintenance
mode via the admin API invalidates the cache on the instance
that handled the admin request. Other replicas do not receive
the invalidation signal — they continue serving their cached
value until the 30-second TTL expires naturally.

## Effect

- **Turn maintenance ON**: up to 30 s per replica during which
  non-admin traffic is still served normally. With N replicas,
  some traffic will leak through until every instance's cache
  expires.
- **Turn maintenance OFF**: up to 30 s per replica during which
  users get 503 even after the platform is back up.

The variance is per-replica — each instance has a different
TTL expiry depending on when it last read from the DB.

## Correct behavior would require

Either:
1. Replace `IMemoryCache` with `IDistributedCache` (Redis)
   for the maintenance flag — the same Redis instance all
   replicas share.
2. Keep `IMemoryCache` but add a Redis pub/sub invalidation
   channel: the admin toggle publishes to the channel, all
   replicas subscribe and clear their local entry immediately.

## When this fires

Only under multi-replica Core API. Single-instance Compose
deployment is safe — there is one process, one cache.

## See also

[cross-cutting/scale-out-readiness.md](../cross-cutting/scale-out-readiness.md) —
architectural overview of all single-instance-safe / scale-out-unsafe
patterns in DevHunt.
