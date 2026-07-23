---
title: Eventing & Outbox — reliable domain event delivery via RabbitMQ
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Services/EventBusService.cs
  - DevHunt.CoreApi/Services/OutboxEventBusDecorator.cs
  - DevHunt.CoreApi/Services/OutboxEventService.cs
  - DevHunt.CoreApi/Services/OutboxEventProcessorWorker.cs
  - DevHunt.Infrastructure/OutboxEvent.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

Transactional outbox pattern wrapping a RabbitMQ topic exchange.
Controllers never talk to RabbitMQ directly: they call
`IEventBusService.PublishAsync`, which writes a row to
`OutboxEvents`. A background worker drains that table and
publishes to RabbitMQ. If RabbitMQ is absent, the entire
event bus collapses to a no-op (see Known traps).

## Components

| Class | Location | Lifetime | Role |
|---|---|---|---|
| `IEventBusService` | EventBusService.cs:49 | — | abstraction injected into controllers/services |
| `NoOpEventBusService` | EventBusService.cs:57 | Singleton | fallback when bus is disabled |
| `RabbitMQEventBusService` | EventBusService.cs:77 | Singleton | actual AMQP publisher; also registered as its own concrete type |
| `OutboxEventBusDecorator` | OutboxEventBusDecorator.cs:8 | Scoped | wraps `IEventBusService`; saves to DB instead of publishing |
| `IOutboxEventService` / `OutboxEventService` | OutboxEventService.cs | Scoped | CRUD on `OutboxEvents` table |
| `OutboxEventProcessorWorker` | OutboxEventProcessorWorker.cs | Hosted service | polls every 5 s; resolves `RabbitMQEventBusService` directly |
| `DomainEvents` | EventBusService.cs:230 | static | factory methods for named event records; ~30 event types defined |
| `OutboxEvent` | Infrastructure/OutboxEvent.cs | entity | persistent row: `Id`, `EventType`, `Payload` (JSON), `Status`, `RetryCount`, `MaxRetries=3` |

## How requests flow through it

```
Controller
  └─ IEventBusService.PublishAsync(DomainEvent)
       (injected as OutboxEventBusDecorator when bus enabled)
       └─ IOutboxEventService.SaveEventAsync
            └─ INSERT OutboxEvents (Status=Pending)

OutboxEventProcessorWorker (every 5 s, batch ≤ 100 rows)
  ├─ GetPendingEventsAsync: WHERE Status=Pending AND RetryCount < MaxRetries
  │    ORDER BY CreatedAt
  ├─ For each row:
  │    ├─ Deserialize Payload → DomainEvent
  │    ├─ RabbitMQEventBusService.PublishAsync → AMQP BasicPublish
  │    │    Exchange: devhunt.events (topic, durable)
  │    │    RoutingKey: {entityType.lower}.{eventType}
  │    │    (3 internal retries with linear backoff: 1 s, 2 s, 3 s)
  │    ├─ on success → Status=Completed, ProcessedAt=now
  │    └─ on failure → RetryCount++
  │         if RetryCount ≥ MaxRetries → Status=Failed
  │         else → Status=Pending (re-queued for next poll)
```

## Registration (Program.cs:220-237)

```
if RabbitMQ:ConnectionString set AND Features:EventBus:Enabled (default true):
    Singleton  RabbitMQEventBusService          ← concrete type for worker
    Scoped     IOutboxEventService → OutboxEventService
    Scoped     IEventBusService   → OutboxEventBusDecorator
    Hosted     OutboxEventProcessorWorker
else:
    Singleton  IEventBusService   → NoOpEventBusService
```

`RabbitMQEventBusService` is registered **both** as the concrete
type and (indirectly) as the implementation the decorator chain
eventually reaches. The worker resolves the concrete type
directly to skip the decorator.

## Exchange and queue topology

- Exchange: `devhunt.events` (topic, durable)
- Dead-Letter Exchange: `devhunt.events.dlx` (topic, durable)
- Dead-Letter Queue: `devhunt.events.dlq` (bound to DLX with `#`)
- Routing key format: `{entityType}.{eventType}` — e.g.,
  `project.created`, `team.member.joined`, `showcase.liked`
- All messages published with `Persistent = true`

## Known traps

- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  `Features:EventBus:Enabled=false` or absent connection string
  → `NoOpEventBusService`; no outbox, no rows, no errors.
- [gotchas/outbox-double-publish-on-scale-out.md](../gotchas/outbox-double-publish-on-scale-out.md) —
  `OutboxEventStatus.Processing` declared but never written; no
  row-level lock; two worker instances can publish the same
  event twice.

## What I should NOT assume
- **`DomainEvents` factory methods are not exhaustive.** ~30
  named methods cover the major domains visible at this commit.
  Controllers may also construct `DomainEvent` records inline;
  don't assume the static class is the only call site.
- **The worker is a `BackgroundService`, not a queue consumer.**
  It polls the DB table, not a RabbitMQ queue. The only
  RabbitMQ consumer wiring visible in this repo is on the
  ml-service side (recommendations queue). Notification-service
  and integration-gateway subscribe via their own connection
  code (not in this repo's source).
- **RabbitMQ internal retries (3 attempts, up to 3 s delay)
  happen inside `RabbitMQEventBusService.PublishAsync`.** The
  outbox retries (up to `MaxRetries=3`, next poll cycle) are a
  separate outer loop. A single event can trigger at most 3 × 3
  = 9 AMQP publish attempts before being marked `Failed`.
