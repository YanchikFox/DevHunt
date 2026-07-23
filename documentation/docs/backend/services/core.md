---
sidebar_position: 1
title: Core API
description: Source-derived service reference for DevHunt.CoreApi.
sidebar_label: Core API
---

# Core API

> _If any detail here contradicts the code, trust the code — not this page._

`DevHunt.CoreApi` is the main ASP.NET Core / .NET 10 service. It owns most business domains: projects, tasks, chat, AI planning, showcase, badges, recommendations, moderation, support, admin, files, integrations, feature flags, and SignalR hubs.

## Runtime

| Detail          | Value                                     |
| --------------- | ----------------------------------------- |
| Compose service | `core-api`                                |
| Container port  | `INTERNAL_PORT_CORE_API`, default `8080`  |
| Host port       | `PORT_CORE_API`, default `7002`           |
| Health          | `GET /health`                             |
| Metrics         | `GET /metrics` with custom IP/token guard |
| SignalR         | `/chatHub`, `/notificationHub`            |

## Major Registrations

Core API wires:

- EF Core `DevHuntDbContext`;
- Redis-backed cache and SignalR backplane when enabled;
- JWT authentication and authorization policies;
- CSRF token and validation filters;
- rate limiting;
- object storage through S3/SeaweedFS;
- LLM and AI planning services;
- domain services for users, projects, tasks, integrations, chat, badges, and recommendations;
- conditional outbox/event bus.

## Request Pipeline

The important order is:

1. response compression and CORS
2. correlation id
3. log enrichment
4. metrics
5. upload validation
6. security headers
7. CSRF token generation
8. authentication
9. authorization
10. IP rate limiting
11. active-user check
12. maintenance mode
13. health, metrics, hubs, controllers

Rate limiting intentionally runs before the active-user DB check.

## Data Access

Most controllers inject `DevHuntDbContext` directly and hit `DefaultConnection`. `ReadOnlyConnection` exists in configuration, but `CreateReadContext()` is not used at the verified commit.

Core API is the host that wires Npgsql `EnableDynamicJson()`.

## Eventing

When RabbitMQ is configured and eventing is enabled, `IEventBusService` writes to `OutboxEvents`; a hosted worker publishes to RabbitMQ. Without that configuration, `IEventBusService` is `NoOpEventBusService`.

Await event publication after durable state changes. Fire-and-forget breaks the reliability model.

## Startup Degradations

- `LlmModelSeeder` failure is logged; service continues.
- object-storage bucket initialization failure is logged; service continues and uploads may fail later.
- Redis absence can downgrade cache/rate-limit/backplane behavior.
- RabbitMQ absence can downgrade eventing to no-op.
