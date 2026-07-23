---
title: Rate limit counters are per-instance without Redis — effective limit multiplied by replica count
type: gotcha
status: verified
sources:
  - DevHunt.CoreApi/Extensions/RateLimitingExtensions.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## The trap

`AddDistributedRateLimiting` (RateLimitingExtensions.cs:13)
selects the counter store based on Redis availability:

```
if Features:Redis:Enabled AND RedisConnection set:
    IRateLimitCounterStore → DistributedCacheRateLimitCounterStore
    IIpPolicyStore         → DistributedCacheIpPolicyStore
else:
    IRateLimitCounterStore → MemoryCacheRateLimitCounterStore
    IIpPolicyStore         → MemoryCacheIpPolicyStore
```

In-memory stores are per-process. With N replicas behind a
load balancer, each replica maintains an independent counter.
An IP address routed round-robin across N replicas can make
N × (configured limit) requests per window before any single
replica fires.

## Example

Config limit: 100 requests / minute.
Replicas: 3.
Effective limit: ~300 requests / minute (100 per replica).

No error is thrown. No log signals the bypass. From the
replica's perspective, the IP is within limit.

## When this fires

Whenever `Features:Redis:Enabled` is false or `RedisConnection`
is absent and Core API has more than one replica. The
in-memory fallback is the default in Development and any
environment where Redis is not explicitly configured.

## What correct behavior requires

Redis must be configured and `Features:Redis:Enabled=true`.
With `DistributedCacheRateLimitCounterStore`, all replicas
share counters via the same Redis instance and the configured
limit is the global limit, not a per-replica limit.

## See also

[cross-cutting/scale-out-readiness.md](../cross-cutting/scale-out-readiness.md) —
architectural overview of all single-instance-safe / scale-out-unsafe
patterns in DevHunt.
