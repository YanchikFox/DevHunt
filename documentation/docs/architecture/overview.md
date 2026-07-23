---
sidebar_position: 1
title: Architecture Overview
description: Entry point for DevHunt service architecture, generated diagrams, and source-of-truth rules.
sidebar_label: Architecture Overview
---

# Architecture Overview

> _If any detail here contradicts the code, trust the code — not this page._

DevHunt is a polyglot developer-collaboration platform composed of a Next.js frontend, two .NET APIs, three supporting service processes, Nginx, PostgreSQL, Redis, RabbitMQ, and SeaweedFS object storage.

This section is intentionally thin. It should orient you, then send you either to generated diagrams or to source-derived reference pages. Do not use archived docs, README files, or old API markdown as architecture sources.

## Current Deployable Shape

| Area                   | Deployable                                                | Runtime                                | Primary role                                                            |
| ---------------------- | --------------------------------------------------------- | -------------------------------------- | ----------------------------------------------------------------------- |
| Edge                   | `api-gateway`                                             | Nginx                                  | TLS termination, route fan-out, SignalR WebSocket upgrade               |
| Web                    | `frontend`                                                | Next.js 16 / React 19                  | User interface, edge auth/i18n middleware, browser API proxy            |
| Core backend           | `core-api`                                                | ASP.NET Core / .NET 10                 | Business logic, SignalR hubs, EF Core data access, outbox writer        |
| Identity               | `auth-service`                                            | ASP.NET Core / .NET 10                 | Login, OAuth, JWT, refresh tokens, TOTP, account lifecycle              |
| AI and recommendations | `ml-service`                                              | FastAPI                                | Recommendations, AI generation, passport synthesis                      |
| External integrations  | `integration-gateway`                                     | Express 5                              | GitHub/GitLab OAuth, webhooks, sync proxy, analyzer trigger             |
| Notifications          | `notification-service`                                    | Express 5                              | Email/SMS/push dispatch and OpenObserve alert webhook                   |
| Data and infra         | `db`, `cache-service`, `message-broker`, `object-storage` | PostgreSQL, Redis, RabbitMQ, SeaweedFS | Persistent data, distributed cache/backplane/rate limits, events, blobs |

## Read This Section In Order

1. [System Overview](./system-overview) - what runs, which service owns which responsibility, and where requests enter.
2. [High-Level Architecture](./high-level-architecture) - service relationships, synchronous calls, eventing, storage, and scale-out constraints.
3. [Chat System](./chat-system) - SignalR hubs, presence, group routing, Redis backplane dependency.
4. [Security](./security) - edge headers, JWT/CSRF, rate limiting, metrics guards, and known security-sensitive gaps.
5. [Glossary](../reference/glossary) - code-derived terms for services, entities, and platform components.

## Generated Diagrams

The diagrams below are generated from source and should be linked instead of duplicated in handwritten pages:

- [Service topology](./generated/service-topology)
- [Database ERD](./generated/database-erd)
- [Event flow](./generated/event-flow)
- [Frontend routes](./generated/frontend-routes)

If a diagram looks wrong, fix the generator or the source model it reads. Do not hand-edit generated files.

## Architectural Rules Of Thumb

- Core API owns most business logic: projects, tasks, chat, AI planning, showcase, badges, recommendations, moderation, support, and admin surfaces.
- Auth Service owns credentials and token lifecycle, but it shares `DevHuntDbContext` with Core API through `DevHunt.Infrastructure`.
- The frontend should call backend services through `/api/proxy-*` paths or route handlers. Browser code should not depend on internal container hostnames.
- RabbitMQ-backed eventing is conditional. If `RabbitMQ:ConnectionString` is missing or `Features:EventBus:Enabled=false`, `IEventBusService` becomes `NoOpEventBusService`; no outbox rows are written.
- Redis is not just a cache. It enables SignalR fan-out, distributed rate-limit counters, and distributed cache behavior. Without it, several paths still run but become single-instance assumptions.

## Known Architecture Risks

The platform works in single-instance Docker Compose mode. Multi-replica deployment has specific correctness risks: outbox double-publish, SignalR fan-out loss without Redis backplane, in-memory maintenance mode cache, local feature-flag invalidation, process-local AI cancel registry, and per-instance rate-limit counters without Redis.

Treat those as design constraints before scaling horizontally.
