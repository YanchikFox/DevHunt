---
sidebar_position: 2
title: System Overview
description: Source-derived overview of the DevHunt runtime system and request boundaries.
sidebar_label: System Overview
---

# System Overview

> _If any detail here contradicts the code, trust the code — not this page._

DevHunt is built around a clear runtime split: the browser reaches Nginx, Nginx routes to the frontend or backend APIs, and the backend services share PostgreSQL, Redis, RabbitMQ, and object storage.

## Entry Points

| Entry point          | Runtime path                                                  | What happens                                                                                                |
| -------------------- | ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| Browser page request | `/` through Nginx to `frontend:3000`                          | Next.js renders the application and applies locale/auth middleware for protected UI routes.                 |
| Browser REST request | `/api/proxy-core/*`, `/api/proxy-auth/*`, or gateway `/api/*` | Requests reach Core API or Auth Service with JWT/cookie/CSRF handling downstream.                           |
| SignalR connection   | `/chatHub`, `/notificationHub`                                | Nginx forwards WebSocket upgrades to Core API hubs.                                                         |
| Auth API             | `/api/auth/*`                                                 | Auth Service handles credential, OAuth, refresh-token, TOTP, and account lifecycle endpoints.               |
| Core API             | `/api/*`                                                      | Core API handles project, task, chat, AI, admin, moderation, showcase, support, and related business APIs.  |
| Service callbacks    | gateway/internal service URLs                                 | Integration Gateway and notification-service expose HTTP endpoints used by Core API and external providers. |

## Service Responsibilities

### Frontend

The frontend is a Next.js 16 App Router application with React 19. It uses NextAuth v5 beta and `next-intl` middleware. Protected UI route prefixes are `/dashboard` and `/admin`; `/api/*` is excluded from the edge middleware matcher, so route handlers and backend services must enforce their own auth.

In Docker/proxy mode, browser calls are rewritten through:

- `/api/proxy-core/:path*` to Core API.
- `/api/proxy-auth/:path*` to Auth Service.
- localized `/chatHub/*` and `/notificationHub/*` to Core API hubs.

ML traffic uses a frontend route handler under `/api/proxy-ml`, not the static rewrite table.

### API Gateway

The Nginx gateway terminates TLS and routes:

- `/` to `frontend:3000`.
- `/api/auth/` to `auth-service:8080`.
- `/api/` to `core-api:8080`.
- `/chatHub` and `/notificationHub` to `core-api:8080` with WebSocket upgrade handling.

It strips `X-User-Id` on every upstream route so clients cannot spoof identity headers. CSP is present only as a commented template in the gateway config, so do not claim gateway-level CSP enforcement unless the config changes.

### Core API

Core API is the main business service. It registers controllers, SignalR hubs, EF Core, object storage, Redis cache/backplane, AI planning services, LLM provider services, project/task/chat services, and the conditional outbox/event bus chain.

The Core API pipeline order matters:

1. response compression and CORS
2. correlation id and log enrichment
3. metrics and upload validation
4. security headers and CSRF token generation
5. authentication and authorization
6. IP rate limiting
7. active-user DB check
8. maintenance-mode check
9. health, metrics, hubs, and controllers

`LogEnrichmentMiddleware` runs before authentication, so its user id is always anonymous unless later code adds a more specific scope.

### Auth Service

Auth Service owns JWT issuance, refresh tokens, OAuth provider registration, registration/login flows, TOTP, email-driven account lifecycle, and its own CSRF token endpoint. It shares the database model with Core API but has a separate startup path and does not run migrations on boot.

### ML Service

The FastAPI ML service handles AI generation, recommendations, and project passport synthesis. It talks to PostgreSQL through asyncpg, Redis for AI cache, RabbitMQ as a consumer, and LLM providers through configured Groq/Gemini clients.

Embeddings routes are optional: if `fastembed` is unavailable, the router is skipped at startup with a warning.

### Integration Gateway

The integration-gateway is an Express 5 service for GitHub/GitLab OAuth, webhook lifecycle, sync, and code-analysis triggers. Webhook ingress relies on signature validation rather than bearer auth on the router path.

There is a known implementation hazard in two inline webhook handlers: they reference `axios` while the imported client is `axiosClient`. Check those paths before assuming webhook create/delete is healthy.

### Notification Service

The notification-service is an Express 5 process for email/SMS/push dispatch and OpenObserve alert webhooks. It has no database connection. Persistence for in-app notifications belongs to Core API.

Core API persists in-app notification state through controller routes such as `POST /api/notifications/mark-read/{id:guid}` and `POST /api/notifications/mark-all-read`. The separate notification-service still exposes compatibility routes such as `PUT /api/notifications/:id/read` that return success without persistent storage changes, and bulk notification recipient addresses are currently placeholder `@devhunt.local` values.

## Shared Data Plane

PostgreSQL is the primary source of persistent application data. Core API and Auth Service use EF Core through `DevHunt.Infrastructure`; ML Service uses asyncpg directly.

Redis backs distributed cache behavior, rate limit counters, and the SignalR backplane when enabled. RabbitMQ backs cross-service asynchronous processing when the event bus is configured. SeaweedFS provides S3-compatible object storage for uploaded files and images.

## What This Overview Does Not Promise

This system has components that look horizontally scalable, but several paths are only safe as single-instance deployments until the scale-out items are fixed. Read [High-Level Architecture](./high-level-architecture) before adding replicas.
