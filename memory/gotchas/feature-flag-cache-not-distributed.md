---
title: Feature flag cache is per-process — toggle lag up to 30s per replica
type: gotcha
status: verified
sources:
  - DevHunt.CoreApi/Services/FeatureFlagService.cs
  - DevHunt.CoreApi/Controllers/SuperAdminController.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## The trap

`CachedFeatureFlagService` uses `IMemoryCache` (per-process,
singleton) to cache flag values for 30 seconds. When a
superadmin toggles a flag via the admin API:

1. `SuperAdminController` saves the change to the DB.
2. `IFeatureFlagService.Invalidate(key)` is called — removes
   from the local `IMemoryCache`.
3. The replica that handled the admin request now reads fresh
   values from the DB on the next call.
4. **Every other replica** still has the old value cached and
   will serve it until the 30-second TTL expires.

The effective rollout time for a flag change under N replicas
is up to 30 s × however many TTLs happen to be live at that
moment — all independent clocks, one per replica.

## Closest sibling

[gotchas/maintenance-mode-cache-not-distributed.md](maintenance-mode-cache-not-distributed.md) —
`MaintenanceModeMiddleware` has the exact same pattern:
`IMemoryCache` + 30 s TTL + per-process invalidation.

## Impact

- Disabling a flag to hide a broken feature: the feature
  remains visible to users on other replicas for up to 30 s.
- Enabling a flag for a staged rollout: traffic distributed
  across replicas gets inconsistent flag states during the
  30-s window.
- Unknown flags default to `true` (enabled), so a missing
  flag row does not protect against accidental exposure.

## What correct behavior requires

Replace `IMemoryCache` with `IDistributedCache` (Redis) for
the flag cache, matching the pattern already used by
`PresenceService`. Then `Invalidate` can write a sentinel or
delete the Redis key that all replicas share.

## See also

[cross-cutting/scale-out-readiness.md](../cross-cutting/scale-out-readiness.md) —
architectural overview of all single-instance-safe / scale-out-unsafe
patterns in DevHunt.
