---
title: Scale-out readiness — single-instance-safe, multi-replica-unsafe patterns
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Services/Ai/Llm/AiInFlightRegistry.cs
  - DevHunt.CoreApi/Services/OutboxEventProcessorWorker.cs
  - DevHunt.CoreApi/Middleware/MaintenanceModeMiddleware.cs
  - DevHunt.CoreApi/Extensions/InfrastructureExtensions.cs
  - DevHunt.CoreApi/Extensions/RateLimitingExtensions.cs
  - DevHunt.CoreApi/Services/PresenceService.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

DevHunt Core API is written in a single-instance-safe model.
All known failure modes work correctly with one replica and
fail silently — no exception, no log error — when two or more
replicas share the same load balancer. The codebase already
includes k8s manifests, suggesting scale-out is an intended
future state. This entry catalogs every known instance of the
pattern so they can be addressed together before multi-replica
deployment.

## Known instances

| Gotcha | What breaks | Fix direction |
|---|---|---|
| [ai-plan-cancel-process-local.md](../gotchas/ai-plan-cancel-process-local.md) | AI stream cancel silently no-ops if cancel hits different replica | Redis-backed registry or sticky sessions |
| [outbox-double-publish-on-scale-out.md](../gotchas/outbox-double-publish-on-scale-out.md) | Two workers publish same outbox event twice; `Processing` status never written | `SELECT … FOR UPDATE SKIP LOCKED` or atomic status CAS |
| [maintenance-mode-cache-not-distributed.md](../gotchas/maintenance-mode-cache-not-distributed.md) | Maintenance toggle propagates to each replica independently with up to 30 s lag | Replace `IMemoryCache` with Redis or add pub/sub invalidation |
| [signalr-fanout-fails-without-redis-backplane.md](../gotchas/signalr-fanout-fails-without-redis-backplane.md) | Chat messages and notifications silently skip clients on other replicas | Redis backplane is already wired but conditional on `Features:Redis:Enabled` |
| [rate-limit-counters-per-instance-without-redis.md](../gotchas/rate-limit-counters-per-instance-without-redis.md) | Effective rate limit becomes N × config per window; also conditional on Redis | Same fix: ensure Redis is configured in production |
| [feature-flag-cache-not-distributed.md](../gotchas/feature-flag-cache-not-distributed.md) | Flag toggle via admin API clears only the local replica's cache; others serve stale values up to 30 s | Replace `IMemoryCache` with `IDistributedCache` in `CachedFeatureFlagService` |

**Related but less critical** (no separate gotcha):
- `PresenceService` sliding TTL is 2 min with no heartbeat.
  Under scale-out presence expires on idle connections whether
  or not Redis is available. Under single-instance this is just
  a UX quirk; under multi-replica it compounds with the
  SignalR backplane issue.

## Common pattern

Every instance in this list shares three properties:

1. **Works correctly under single-instance Compose deployment.**
   The current production configuration is a single container
   per service, so none of these bugs have been observable.

2. **No warning at startup.** The code does not log "running
   in single-instance mode" or similar. There is no assertion
   or health-check item that verifies shared state is actually
   shared.

3. **Manifests as silent correctness failures**, not crashes
   or 5xx responses. Symptoms appear as intermittent missed
   messages, partial maintenance enforcement, cancel actions
   that seem to succeed but have no effect, or attackers
   bypassing rate limits — all of which look like unrelated
   bugs rather than a systemic cause.

## What needs to change before multi-replica deployment

These are preconditions, not suggestions:

1. **Redis must be on and configured.** `Features:Redis:Enabled=true`
   and a live `RedisConnection` activates three fixes in one:
   SignalR backplane, distributed rate limit counters, and the
   distributed cache layer that `PresenceService` already uses.
   This is the highest-leverage single change.

2. **Outbox row locking.** Add `SELECT … FOR UPDATE SKIP LOCKED`
   or an atomic status update before the worker processes a
   batch. Redis being on does not fix this one — it requires a
   DB-side change in `OutboxEventService.GetPendingEventsAsync`
   and/or `OutboxEventProcessorWorker`.

3. **AI cancel registry.** Move `IAiInFlightRegistry` off the
   in-process singleton. Options: Redis-backed cancel tokens,
   or sticky sessions per `requestId`. Redis being on does not
   fix this automatically — it requires a code change.

4. **Maintenance mode cache.** Replace `IMemoryCache` with
   `IDistributedCache` in `MaintenanceModeMiddleware` (or add
   Redis pub/sub invalidation). Redis being on does not fix this
   automatically — requires a targeted code change.

5. **Feature flag cache.** Same pattern as #4 — replace
   `IMemoryCache` with `IDistributedCache` in
   `CachedFeatureFlagService`. `Invalidate()` must then delete
   from Redis so all replicas see the change immediately.

## Detection

When you find another class in the same family (in-process
singleton or local cache that should be shared across replicas),
add a gotcha entry for it and link it here. Add a "See also"
backlink in the gotcha pointing to this entry.

**Audited — safe (no mutable shared state):**
- `IEmailTemplateService` / `EmailTemplateService` — stateless;
  all methods are pure template generators, no fields or cache.
- `IProfanityFilterService` / `ProfanityFilterService` — all
  state is `IReadOnlyList` loaded once from embedded resource
  at construction; `static readonly` compiled regex patterns;
  no mutation after init.
- `IAiSkillResolver` / `AiSkillResolver` — all data is
  `static readonly` dictionaries; `Resolve()` is a pure function.

**Still to audit:**
- Any other Singleton in Program.cs that holds mutable state
  (e.g. `IPresenceService` — already uses `IDistributedCache`,
  so likely safe under Redis but degrades under in-memory fallback)
