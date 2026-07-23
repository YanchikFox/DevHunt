---
sidebar_position: 3
title: Incident Response
description: Source-derived incident triage guide for DevHunt runtime failures.
sidebar_label: Incident Response
---

# Incident Response

> _If any detail here contradicts the code, trust the code — not this page._

This runbook is a triage order, not a replacement for service-specific logs.

## 1. Classify The Blast Radius

| Symptom                             | First subsystem                                              |
| ----------------------------------- | ------------------------------------------------------------ |
| both APIs down                      | PostgreSQL and `db-migrator`                                 |
| only login/auth broken              | Auth Service, JWT config, OAuth config, SMTP for email flows |
| project/task/profile APIs broken    | Core API, PostgreSQL, CSRF/auth/rate limits                  |
| chat/notifications intermittent     | Core API SignalR, Redis backplane                            |
| async side effects missing          | RabbitMQ, `OutboxEvents`, event consumers                    |
| uploads broken                      | SeaweedFS/object storage and Core API bucket init            |
| frontend pages render but APIs fail | frontend rewrites/proxy mode, backend health                 |

## 2. Check Health

Check container health and HTTP health endpoints:

```bash
docker compose ps
curl -fsS http://localhost:7001/health
curl -fsS http://localhost:7002/health
curl -fsS http://localhost:8000/health
```

Only use the ML health check when ML service is part of the incident path.

## 3. Check The Data Plane

PostgreSQL is the first dependency for Auth Service, Core API, migrator, and ML Service. Redis affects cache, SignalR, and rate limiting. RabbitMQ affects event-driven side effects. SeaweedFS affects files and images.

If a service is "healthy" but behavior is missing, inspect degraded dependencies. Several startup paths log warnings and continue.

## 4. Check Events

When side effects are missing:

- confirm Core API has a RabbitMQ connection string;
- confirm `Features:EventBus:Enabled` is true;
- inspect `OutboxEvents`;
- inspect consumer service logs.

No RabbitMQ can mean no-op event publishing, not a crash.

## 5. Check Observability

Core API sends logs/traces/metrics to OpenObserve when configured. Metrics endpoint access is custom-guarded; verify `METRICS_TOKEN` and proxy source IP behavior before exposing it.

Remember the Core API log enrichment user id can be anonymous even for authenticated requests because the middleware runs before authentication.

## 6. Scale-Out Specific Checks

If the incident appears only under multiple replicas, check:

- Redis backplane for SignalR;
- Redis-backed rate-limit counters;
- outbox duplicate publish risk;
- process-local AI cancel registry;
- per-process maintenance and feature-flag caches.

These issues are silent correctness failures more often than crashes.
