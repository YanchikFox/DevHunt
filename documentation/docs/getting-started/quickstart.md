---
sidebar_position: 1
title: Quickstart
sidebar_label: Quickstart
---

# Quickstart

Get a fully running local stack — Docker only, no host-side runtimes required.

> _If any detail here contradicts the code, trust the code — not this page._

## Prerequisites

- Docker Engine and Docker Compose v2 — verify with `docker compose version`
- Git

No .NET SDK, Node.js, or Python is required to run the stack inside Docker.

## 1. Clone the repo

```bash
git clone https://github.com/YanchikFox/DevHunt.git
cd DevHunt
```

## 2. Create the local secrets file

`docker-compose.yml` uses fail-fast syntax (`${VAR:?...}`) for four variables — `POSTGRES_PASSWORD`, `JWT_KEY`, `RABBITMQ_DEFAULT_PASS`, and `ENCRYPTION_KEY`. Without them Docker Compose aborts before starting any container.

The override file ships dev-only defaults for all required secrets:

```bash
cp docker-compose.override.yml.example docker-compose.override.yml
```

`docker-compose.override.yml` is gitignored. You can edit the values in that file freely; just never use these defaults outside a local dev machine.

## 3. Start the stack

```bash
docker compose up -d
```

The first run builds or pulls all images. Allow 3–5 minutes for .NET and Python image layers.

## 4. Wait for database migrations

`db-migrator` runs EF Core migrations and exits. `auth-service` and `core-api` depend on it via `service_completed_successfully` and will not start until it exits 0.

Stream its output until it finishes:

```bash
docker compose logs -f db-migrator
```

Success looks like the container reaching `Exited (0)`. If PostgreSQL is slow on first boot, the migrator retries automatically — up to 10 attempts with exponential backoff (2 s, 4 s, 8 s …).

## 5. Verify

```bash
docker compose ps
```

`db-migrator` should be `Exited (0)`. `auth-service`, `core-api`, and `frontend` should be `Up`.

Hit the health endpoints:

| Service      | URL                          | Expected   |
| ------------ | ---------------------------- | ---------- |
| Auth Service | http://localhost:7001/health | `200 OK`   |
| Core API     | http://localhost:7002/health | `200 OK`   |
| Frontend     | http://localhost:3000        | Login page |

## Stop the stack

```bash
docker compose down          # stop, keep volumes
docker compose down -v       # stop and delete all data volumes
```

## Troubleshooting

**`auth-service` or `core-api` stuck in `Waiting`** — `db-migrator` exited non-zero. Run `docker compose logs db-migrator` to see the error. Re-run `docker compose up -d` once the database is healthy.

**Port already in use** — Set `PORT_CORE_API`, `PORT_AUTH`, or `PORT_FRONTEND` in `docker-compose.override.yml` to free ports.

**Image build fails** — Try `DOCKER_BUILDKIT=1 docker compose up -d --build`.
