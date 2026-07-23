---
sidebar_position: 2
title: How To Run The Backend
description: Source-derived backend run paths for Docker Compose and local .NET services.
sidebar_label: How To Run The Backend
---

# How To Run The Backend

> _If any detail here contradicts the code, trust the code — not this page._

There are two supported run shapes: full Docker Compose, or local .NET services against Compose-managed infrastructure.

Do not skip the migrator. Core API and Auth Service no longer run migrations during startup.

## Full Compose Path

The backend-relevant services in Compose are:

- `db`
- `cache-service`
- `message-broker`
- `object-storage`
- `db-migrator`
- `auth-service`
- `core-api`
- `ml-service`
- `notification-service`
- `integration-gateway`
- `code-analyzer`

Bring up the backend through Compose when you want container-to-container hostnames, Redis, RabbitMQ, object storage, and the migrator wiring to match the runtime topology:

```bash
docker compose up -d db cache-service message-broker object-storage db-migrator auth-service core-api
```

Add supporting services when the feature path needs them:

```bash
docker compose up -d ml-service notification-service integration-gateway code-analyzer
```


## Local .NET Path

For local backend debugging, run infrastructure through Compose and web services through `dotnet`:

```bash
docker compose up -d db cache-service message-broker object-storage db-migrator
dotnet run --project DevHunt.AuthService/DevHunt.AuthService.csproj
dotnet run --project DevHunt.CoreApi/DevHunt.CoreApi.csproj
```

`EnvLoader.Load()` runs before each .NET host builds configuration. For local tools outside containers, it can rewrite DB host variables from service-name hostnames to `localhost` when its conditions match.

## Required Secrets

Compose requires these values before the key backend services can start:

| Variable                | Used by                                                  |
| ----------------------- | -------------------------------------------------------- |
| `POSTGRES_PASSWORD`     | PostgreSQL, migrator, Core API, Auth Service, ML Service |
| `RABBITMQ_DEFAULT_PASS` | RabbitMQ and event consumers                             |
| `JWT_KEY`               | Core API and Auth Service                                |
| `ENCRYPTION_KEY`        | Core API encryption-dependent domains                    |
| `ENCRYPTION_IV`         | Core API encryption-dependent domains                    |

Core API also reads object-storage credentials and service base URLs from Compose. Auth Service reads OAuth, email, Redis, JWT, and OpenObserve settings.

## Health Checks

Use service health endpoints after startup:

```bash
curl -fsS http://localhost:7001/health
curl -fsS http://localhost:7002/health
```

Default host ports are `7001` for Auth Service and `7002` for Core API. Inside containers both listen on internal port `8080` unless overridden.

## What To Watch

- If `db-migrator` exits non-zero, Compose keeps Auth Service and Core API from starting.
- If RabbitMQ is absent or its connection string is empty, Core API event publishing degrades to no-op.
- If Redis is absent, Core API falls back to in-memory distributed cache and in-memory rate-limit counters.
- Object-storage bucket initialization failure is non-fatal; Core API can start while uploads fail later.
