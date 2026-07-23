---
sidebar_position: 4
title: Setup
sidebar_label: Setup
---

# Local Development Setup

How to run individual services outside Docker — useful when you are actively developing one service and want fast iteration without rebuilding images.

> _If any detail here contradicts the code, trust the code — not this page._

For running the full stack without modifications, use the Docker path in [Quickstart](./quickstart) instead.

## Required tools

| Tool              | Version | Used by                                             |
| ----------------- | ------- | --------------------------------------------------- |
| .NET SDK          | 10.0    | core-api, auth-service, db-migrator                 |
| Node.js           | ≥20     | frontend, documentation portal                      |
| Python            | ≥3.11   | ml-service                                          |
| Docker Compose v2 | any     | infrastructure services (db, Redis, RabbitMQ, etc.) |

Install each tool from its official download page. For .NET 10 use [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download). For Node.js 20+ use [https://nodejs.org](https://nodejs.org) or a version manager like `nvm`.

## Start infrastructure services

Even when running application services on the host, you still need PostgreSQL, Redis, RabbitMQ, and SeaweedFS. The easiest path is to bring up just the infrastructure containers:

```bash
docker compose up -d db cache-service message-broker object-storage
```

These run on their default internal ports; the application services reach them via `localhost` on host-mapped ports.

## Run database migrations

```bash
cd DevHunt.DatabaseMigrator
dotnet run
```

`EnvLoader` walks up from the binary's directory looking for a `.env` file. If no `.env` is present, set the connection string directly:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=devhunt_db;Username=postgres;Password=devhunt_local_dev_password" dotnet run
```

The migrator retries up to 10 times. Once it exits 0, the schema is ready.

## Run Core API

```bash
cd DevHunt.CoreApi
dotnet run
```

Default host port: **7002**. Health check: http://localhost:7002/health

Core API reads config in this order (last wins):

1. `appsettings.json`
2. `appsettings.Development.json`
3. Environment variables (or `.env` values loaded by `EnvLoader`)
4. .NET User Secrets (non-Production only)

Minimum env vars for a working dev instance:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=devhunt_db;Username=postgres;Password=devhunt_local_dev_password"
Jwt__Key="devhunt_local_dev_jwt_key_min_32_characters_long"
Encryption__Key="devhunt_local_encryption_key_32_chars!"
```

RabbitMQ and Redis are optional — the service starts in degraded mode without them (events go to `NoOpEventBusService`, rate limiting falls back to in-memory).

## Run Auth Service

```bash
cd DevHunt.AuthService
dotnet run
```

Default host port: **7001**. Health check: http://localhost:7001/health

Minimum env vars:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=devhunt_db;Username=postgres;Password=devhunt_local_dev_password"
Jwt__Key="devhunt_local_dev_jwt_key_min_32_characters_long"
```

Use the same `Jwt__Key` value as Core API so tokens issued by auth-service are accepted by core-api.

## Run the frontend

```bash
cd frontend
npm install
npm run dev
```

Default port: **3000**.

The frontend proxies API calls through Next.js rewrites. Active when `DOCKER_ENV=true` or `NEXT_PUBLIC_USE_PROXY=true`. In local dev without Docker you typically set:

```bash
NEXT_PUBLIC_USE_PROXY=true npm run dev
```

This routes `/api/proxy-core/*` → `http://localhost:7002/api/*` and `/api/proxy-auth/*` → `http://localhost:7001/api/*`.

## Run ml-service

```bash
cd ml-service
pip install -r requirements.txt
DATABASE_URL="postgresql://postgres:devhunt_local_dev_password@localhost:5432/devhunt_db" uvicorn main:app --host 0.0.0.0 --port 8000
```

`DATABASE_URL` must be set — `deps.py` checks for its presence at module import time and raises `ValueError` if absent.

## Environment variable naming

ASP.NET Core maps config key paths to env var names using `__` (double underscore) as the separator:

```
ConnectionStrings:DefaultConnection  →  CONNECTIONSTRINGS__DEFAULTCONNECTION
                                     or ConnectionStrings__DefaultConnection
Jwt:Key                              →  Jwt__Key
RabbitMQ:ConnectionString            →  RabbitMQ__ConnectionString
Features:Redis:Enabled               →  Features__Redis__Enabled
Encryption:Key                       →  Encryption__Key
```

Both forms work; the `docker-compose.yml` uses mixed conventions — check the `environment:` block of each service for exact names.
