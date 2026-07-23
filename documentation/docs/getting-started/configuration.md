---
sidebar_position: 6
title: Configuration
sidebar_label: Configuration
---

# Configuration Reference

How DevHunt services load configuration, the priority order of sources, and where secrets come from in each environment.

> _If any detail here contradicts the code, trust the code — not this page._

## Configuration source stack

Both `core-api` and `auth-service` load configuration from these sources in order — the last one to set a key wins:

```
1. appsettings.json               — baseline, committed, no secrets
2. appsettings.{Environment}.json — environment overlay (Development, Staging)
3. Environment variables          — Docker Compose / Kubernetes secrets
4. Key Vault (Production only)    — Azure Key Vault, refreshed every 5 minutes
   or User Secrets (non-Production) — .NET User Secrets, scoped to assembly
```

`EnvLoader.Load()` runs before `CreateBuilder` in both services. It finds the nearest `.env` file by walking up from the binary's directory and injects values into the process environment — so `.env` values land in source #3. Docker-injected env vars always win because `EnvLoader` never overwrites a key that is already set.

## Environment variable naming

ASP.NET Core config key paths use `:` as the separator. Environment variable names replace `:` with `__` (double underscore):

| Config key                            | Env var name                           |
| ------------------------------------- | -------------------------------------- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `Jwt:Key`                             | `Jwt__Key`                             |
| `RabbitMQ:ConnectionString`           | `RabbitMQ__ConnectionString`           |
| `Features:Redis:Enabled`              | `Features__Redis__Enabled`             |
| `Encryption:Key`                      | `Encryption__Key`                      |
| `OpenObserve:User`                    | `OpenObserve__User`                    |

The `docker-compose.yml` uses mixed conventions in its `environment:` blocks — check the actual service definition for the exact key name used.

## Required secrets

These four variables have fail-fast syntax in `docker-compose.yml` (`${VAR:?message}`) — Docker Compose aborts if they are unset:

| Variable                | Used by                           | Constraint                                                  |
| ----------------------- | --------------------------------- | ----------------------------------------------------------- |
| `POSTGRES_PASSWORD`     | `db`, `db-migrator`, all services | any non-empty value                                         |
| `JWT_KEY`               | `auth-service`, `core-api`        | ≥32 characters; in Production may not contain weak patterns |
| `RABBITMQ_DEFAULT_PASS` | `message-broker`, `core-api`      | any non-empty value                                         |
| `ENCRYPTION_KEY`        | `core-api`                        | exactly 32 characters                                       |

For local development, `docker-compose.override.yml.example` provides safe defaults for all four. Copy it to get started:

```bash
cp docker-compose.override.yml.example docker-compose.override.yml
```

## Optional / feature-gated configuration

### RabbitMQ / EventBus

Core API registers a real `RabbitMQEventBusService` only when both conditions are true:

- `Features:EventBus:Enabled` = `true` (default: `true`)
- `RabbitMQ:ConnectionString` is non-empty

Without a connection string the service falls back to `NoOpEventBusService` — domain events are silently dropped. This is the expected dev behaviour when `message-broker` is not running.

### Redis

Redis is used for SignalR backplane, rate limiting, and caching. When `ConnectionStrings:RedisConnection` is empty, Core API falls back to in-memory rate limiting and Auth Service uses in-memory rate limit counters. This is suitable for development only.

### OAuth providers

Google and GitHub OAuth are registered only when both `ClientId` and `ClientSecret` are configured:

```
Authentication:Google:ClientId + Authentication:Google:ClientSecret
Authentication:GitHub:ClientId + Authentication:GitHub:ClientSecret
```

Omitting these disables the respective OAuth flow; password auth and TOTP still work.

### OpenObserve (tracing and logs)

`OpenObserve:OtlpTracesEndpoint`, `OpenObserve:User`, and `OpenObserve:Password` configure OTLP tracing. In Production the service refuses to start if the OpenObserve password is missing, shorter than 32 characters, or matches weak patterns (`ChangeMe123!`, `default`, `changeme`).

## Production-specific behaviour

In Production (`ASPNETCORE_ENVIRONMENT=Production`):

- **Azure Key Vault** replaces .NET User Secrets as the secrets source. Activated when `KeyVault:Uri` is set; uses `DefaultAzureCredential` (Managed Identity, or `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` / `AZURE_TENANT_ID` env vars). Key Vault is unavailable? The service logs a warning and falls through to env vars.
- **JWT key strength** is validated at startup — weak keys cause startup failure.
- **OpenObserve password** is validated at startup — weak passwords cause startup failure.
- **Cookies** are always `Secure` in Production (requests arrive over HTTPS).

## `.env` file (local tooling only)

`.env` is for local development and tooling scripts (e.g., `dotnet ef` CLI). It is gitignored. Docker Compose does not load it — secrets reach containers through `docker-compose.override.yml`.

`EnvLoader` applies `ApplyLocalDatabaseOverrides()` when the binary is not running inside a Docker container: it rewrites the database connection strings to use `localhost` instead of the internal service hostname, so `dotnet ef` and other local tools can reach the containerised PostgreSQL.

## `appsettings.json` structure

Baseline (no secrets, committed):

```
ConnectionStrings:
  DefaultConnection     — primary read-write DB connection
  ReadOnlyConnection    — read-only DB replica (core-api only)
  RedisConnection       — Redis for caching + SignalR + rate limits

Jwt:
  Key                   — HMAC secret (set via env var in all envs)
  Issuer
  Audience

Features:
  Redis:Enabled         — defaults to true except in Development
  EventBus:Enabled      — defaults to true

RabbitMQ:
  ConnectionString      — amqp://... (set via env var)

Encryption:
  Key                   — 32-char symmetric key (set via env var)
  IV                    — 16-char IV (set via env var)
```

Use `appsettings.Development.json` to override defaults for local runs (e.g., disable Redis, point at a local SMTP catcher).
