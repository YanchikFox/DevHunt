---
title: ml-service — FastAPI AI / recommendations service
type: system
status: verified
sources:
  - ml-service/main.py
  - ml-service/requirements.txt
  - ml-service/pyproject.toml
  - ml-service/Dockerfile
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review:
---

## What it is

Python (FastAPI) service handling AI generation, recommendation
engine, and project-passport synthesis. Talks to LLM providers
(Groq / Gemini per compose env) on behalf of Core API.

Compose service `ml-service`, port `PORT_ML` (default 8000).

Source-of-truth comment at `main.py:1–13` notes the file was
refactored from 823 → ~190 lines and points to the routers/services
that hold the actual logic.

## Entry-point

[ml-service/main.py](ml-service/main.py) — the `app` FastAPI
instance is at line 138. `if __name__ == "__main__"` guard at
line 305 runs `uvicorn.run(app, host=HOST, port=PORT)`.

## Boot wiring

- **Tracing + logging** (`configure_tracing()`, lines 77–131): OTLP
  HTTP exporters to OpenObserve for both traces and logs. In
  production the function refuses to start tracing if
  `OPENOBSERVE_ROOT_USER` / `OPENOBSERVE_ROOT_PASSWORD` are
  unset (lines 91–96). Instruments asyncpg, httpx, FastAPI,
  Python logging.
- **CORS** (lines 149–165): origins from
  `CORS_ALLOWED_ORIGINS` (comma-split). In production a wildcard
  raises immediately (line 152–156). Dev default is `*`.
- **Routers** (lines 168–175): `ai`, `recommendations`,
  `passport`. The `embeddings` router is conditional — its
  import is wrapped in a try/except (lines 59–63), and if
  `fastembed` is not installed the router is silently skipped
  with a warning at startup.
- **Per-request metric middleware** (lines 182–192): increments
  `REQUEST_COUNTER` and observes `REQUEST_LATENCY` — Prometheus
  registry is shared with `metrics.py`.

## Lifecycle

- `@app.on_event("startup")` (lines 202–232): connects Redis from
  `REDIS_URL` (default `redis://cache-service:6379/0`); calls
  `ai_cache.init(redis_client)`. Then starts the RabbitMQ event
  consumer (`consumers.event_consumer.start_event_consumer`) as an
  asyncio task, wiring its `on_connection_state` and `on_event`
  callbacks to the `RABBIT_CONNECTION_STATE` gauge and
  `EVENT_MESSAGES_TOTAL` counter.
- `@app.on_event("shutdown")` (lines 235–252): stops the consumer,
  closes the asyncpg pool (if it was lazily created via
  `deps.get_db_pool`), and closes the Redis client.

## Endpoints

- `GET /health` (lines 273–286) — runs `SELECT 1` against the
  asyncpg pool dependency; returns `healthy` only when Postgres
  responds.
- `GET /metrics` — Prometheus text exposition (lines 266–270).
- `GET /` — returns a static map of public endpoints (line 289).
- Plus router-mounted routes: `/api/ai`,
  `/api/recommendations/generate`, `/api/recommendations/refresh`,
  passport routes (`routers/passport.py`), and embeddings if
  enabled.

## Outbound dependencies

- **PostgreSQL** via `deps.get_db_pool` (asyncpg, `DATABASE_URL`
  env).
- **Redis** for `ai_cache` (`REDIS_URL`).
- **RabbitMQ** for event consumption (`RABBITMQ_URL`,
  `ML_QUEUE` default `devhunt.ml.recommendations`,
  `RABBITMQ_EXCHANGE_NAME` default `devhunt.events`).
- **LLM providers** — Groq (`GROQ_API_KEY`), Gemini
  (`GEMINI_API_KEY`); selected via `AI_PROVIDER` (default `groq`).
  Provider clients live in `clients/`.
- **OpenObserve** for traces + logs.

## What I should NOT assume

- **`@app.on_event("startup"|"shutdown")` is the deprecated API.**
  FastAPI/Starlette have moved to `lifespan` handlers; the file
  still uses the old hooks. If you migrate, the cleanup chain
  must remain identical.
- **`fastembed` is optional.** Embedding endpoints disappear
  silently when the lib is missing — there's no import-time error.
  If embeddings "don't work in prod," check Dockerfile `pip` first.
- **Redis failures are non-fatal.** `ai_cache` init logs a
  warning and `redis_client` is set to `None` (lines 213–216);
  callers must handle that. Don't add hard `assert redis_client`
  later.
- **Db pool isn't created at startup.** It's lazily acquired
  through `deps.get_db_pool` per request, then closed on
  shutdown if `app.state.db_pool` was ever set.
- **In production, `OPENOBSERVE_ROOT_*` are required for tracing
  but not for the service to boot.** The function returns the
  provider unmodified and logs a warning instead of raising
  (lines 91–96). Tracing will be silently disabled.
