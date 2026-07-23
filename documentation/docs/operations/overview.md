---
sidebar_position: 1
title: Operations Overview
description: Source-derived operational map for DevHunt services, dependencies, health checks, observability, and scale-out risks.
sidebar_label: Operations Overview
---

# Operations Overview

> _If any detail here contradicts the code, trust the code — not this page._

DevHunt operations revolve around keeping the data plane healthy first, then application services, then edge/frontend routing.

## Service Groups

| Group          | Services                                                                     |
| -------------- | ---------------------------------------------------------------------------- |
| Data plane     | `db`, `cache-service`, `message-broker`, `object-storage`                    |
| Init jobs      | `db-migrator`, `openobserve-init`                                            |
| Core app       | `auth-service`, `core-api`, `frontend`, `api-gateway`                        |
| Supporting app | `ml-service`, `notification-service`, `integration-gateway`, `code-analyzer` |
| Observability  | `openobserve` plus service-level logs, traces, metrics                       |
| Maintenance    | `backup-service`                                                             |

Auth Service and Core API depend on the migrator completing successfully. Core API also depends on Redis, RabbitMQ, and object storage for full behavior, though some failures degrade later rather than crash startup.

## Health Endpoints

| Service              | Health path   |
| -------------------- | ------------- |
| Auth Service         | `GET /health` |
| Core API             | `GET /health` |
| ML Service           | `GET /health` |
| Notification Service | `GET /health` |
| Integration Gateway  | `GET /health` |
| Code Analyzer        | `GET /health` |

Nginx health uses `nginx -t`. Frontend health checks the local Next.js HTTP server.

## Observability

Core API uses three signal paths:

- Serilog logs to console and optionally OpenObserve HTTP sink.
- OpenTelemetry traces to OpenObserve OTLP endpoint.
- Prometheus metrics exposed at `/metrics`.

Auth Service, ML Service, notification-service, and integration-gateway also export logs/traces to OpenObserve according to their runtime wiring.

Metrics access is guarded by private/loopback IP prefixes or `X-Metrics-Token`. Treat proxy `RemoteIpAddress` behavior as security-sensitive.

## Operational Dependencies

- PostgreSQL must be healthy before migrator and APIs can start.
- Redis backs distributed cache, SignalR backplane, and rate-limit counters when enabled.
- RabbitMQ is required for real event publishing; otherwise Core API event bus can become no-op.
- SeaweedFS backs uploads and image access; Core API bucket initialization failure is non-fatal.
- OpenObserve missing credentials can disable sinks depending on service/environment.

## Scale-Out Readiness

The current Core API is single-instance-safe, not fully multi-replica-safe.

Before running multiple Core API replicas, address:

- Redis backplane and Redis-backed rate limit counters.
- Outbox row locking or atomic status transition.
- process-local AI cancel registry.
- per-process maintenance-mode cache.
- per-process feature-flag cache invalidation.

These failures are mostly silent correctness bugs rather than crashes.

## Runbook Starting Points

- If both APIs are down, inspect `db` and `db-migrator` first.
- If chat or notifications are intermittent under replicas, inspect Redis/backplane before hub code.
- If expected emails or async side effects are missing, inspect RabbitMQ config and `OutboxEvents`.
- If uploads fail while Core API is healthy, inspect object storage and bucket initialization logs.
- If metrics are unexpectedly public, inspect proxy addressing and `METRICS_TOKEN`.
