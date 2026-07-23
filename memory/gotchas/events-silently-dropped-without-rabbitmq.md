---
title: Domain events silently dropped when RabbitMQ is not configured
type: gotcha
status: verified
sources:
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## Symptom

Business code calls `IEventBusService.PublishAsync(...)` and gets no
error. Consumers (ml-service, notification-service,
integration-gateway, OutboxEventProcessorWorker) never receive the
event. Logs show no failure on the publishing side.

## Cause

[DevHunt.CoreApi/Program.cs:222-237](DevHunt.CoreApi/Program.cs#L222-L237)
makes `IEventBusService` registration conditional:

```csharp
var rabbitMqConnectionString = Configuration["RabbitMQ:ConnectionString"];
var isEventBusEnabled = Configuration.GetValue("Features:EventBus:Enabled", true)
    && !string.IsNullOrWhiteSpace(rabbitMqConnectionString);

if (isEventBusEnabled) { /* real RabbitMQ + Outbox + worker */ }
else { builder.Services.AddSingleton<IEventBusService, NoOpEventBusService>(); }
```

When either `Features:EventBus:Enabled` is `false` or
`RabbitMQ:ConnectionString` is empty/whitespace, `NoOpEventBusService`
is registered. Calls to it succeed and return; nothing is sent
anywhere, nothing is written to the outbox.

## When this fires

- Local dev without a `RabbitMQ:ConnectionString` env var set.
- Tests that don't override `Features:EventBus:Enabled = true` and
  don't supply a connection string — they will run against the
  no-op silently.
- Production misconfiguration where the connection string secret
  is missing — Core API will start cleanly, the bus will be a
  no-op, and only downstream consumer silence will reveal it.

## How to spot it

- Inspect the registered `IEventBusService` implementation at
  startup (a debug log line at app start would help; none exists
  today).
- Compare `appsettings.{env}.json` and the env injected by Compose:
  `RabbitMQ__ConnectionString` is set in
  [docker-compose.yml:224](docker-compose.yml#L224) for the
  `core-api` service via the `RABBITMQ_DEFAULT_PASS` env. If that
  env is empty, Compose itself fails (`:?` substitution), so in
  Compose mode the path is fine — the trap is local `dotnet run`
  without a `.env` or with the flag forced off.

## What to do

- Don't add a "safety" exception path inside `NoOpEventBusService`.
  Its silence is by design (cheaper than nullable-event-bus
  patterns elsewhere in the code).
- For tests that rely on event publication, either inject a fake
  bus directly or set both `Features:EventBus:Enabled = true` and a
  `RabbitMQ:ConnectionString` pointing at a test broker.
- If a feature seems "not firing in production," check the
  feature flag and connection string before chasing the consumer.
