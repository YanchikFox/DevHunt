---
title: Outbox double-publish under scale-out — Processing status never written
type: gotcha
status: verified
sources:
  - DevHunt.CoreApi/Services/OutboxEventService.cs
  - DevHunt.CoreApi/Services/OutboxEventProcessorWorker.cs
  - DevHunt.Infrastructure/OutboxEvent.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## The trap

`OutboxEventStatus` declares four values:

```
Pending = 0, Processing = 1, Completed = 2, Failed = 3
```

`OutboxEventService` only ever writes `Pending`, `Completed`,
and `Failed`. `Processing` is never set. The worker's
`GetPendingEventsAsync` selects all rows where
`Status = Pending AND RetryCount < MaxRetries`, then iterates
and publishes without first marking the row as `Processing`.

Under a single worker instance this is harmless. Under scale-out
(two or more replicas of Core API), two workers can fetch the
same batch in the same 5-second poll window and both call
`RabbitMQEventBusService.PublishAsync` on the same row.
The event is published twice.

## Closest sibling

[gotchas/ai-plan-cancel-process-local.md](ai-plan-cancel-process-local.md) —
`IAiInFlightRegistry` is also an in-process singleton that
breaks silently under scale-out. Same class of bug: works
correctly with one instance, silent correctness failure with many.

## What correct behavior would look like

Before publishing, the worker should atomically UPDATE the row
to `Status = Processing` (ideally with `WHERE Status = Pending`
in a single statement or with `SELECT … FOR UPDATE SKIP LOCKED`
to exclude already-claimed rows). The current code does not do
this.

## When this matters

Only when Core API runs with more than one replica. A single
container deployment is safe. Downstream consumers should be
idempotent regardless, but at this commit that is not
guaranteed for all event types.

## See also

[cross-cutting/scale-out-readiness.md](../cross-cutting/scale-out-readiness.md) —
architectural overview of all single-instance-safe / scale-out-unsafe
patterns in DevHunt.
