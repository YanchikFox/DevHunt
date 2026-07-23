---
title: integration-gateway — Express OAuth + webhooks + sync proxy
type: system
status: verified
sources:
  - integration-gateway/src/index.js
  - integration-gateway/src/telemetry.js
  - integration-gateway/src/metrics.js
  - integration-gateway/package.json
  - integration-gateway/Dockerfile
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Node.js 20+ ESM service (Express 5) that brokers GitHub / GitLab
integrations: OAuth flows, webhook lifecycle, repository sync, and
fire-and-forget code-analysis triggers.

Compose service `integration-gateway`, port
`PORT_INTEGRATION_GATEWAY` (default 5002).

## Entry-point

[integration-gateway/src/index.js](integration-gateway/src/index.js)

`telemetry.js` is the first import (line 8).

## Boot wiring

- **Production secret validation** (lines 31–58): refuses to start
  unless `GITHUB_WEBHOOK_SECRET`, `GITLAB_WEBHOOK_SECRET`, and
  `JWT_SECRET` are each ≥32 chars. This is hard — the process
  throws synchronously.
- **CORS** (lines 64–80): same env-driven pattern as the
  notification service; production refuses unset / `*`.
- **Body limits** (lines 92–94): `10mb` (vs notification's 5mb)
  to accommodate webhook payloads.
- **Middleware order** (lines 96–115): `requestContext` →
  `traceContext` (propagates `traceparent`) → metrics →
  `rateLimit` → `throttle` → logging.
- **Routers** mounted at lines 117–119:
  - `/api/oauth` — `oauthRouter` from `routes/oauth.js`,
    bearer-authenticated.
  - `/api/webhooks` — `webhookRouter` from
    `routes/webhooks.js`, **no bearer auth** (relies on signature
    verification).

## Endpoints (top-level, defined in index.js itself)

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET | `/health` | none | line 492 |
| GET | `/` | none | endpoint map (line 513) |
| GET | `/metrics` | none | Prometheus |
| GET | `/metrics/throttle` | none | throttle stats |
| POST | `/api/sync` | bearer | line 122; fetches integration config from Core API if not provided; for `github` triggers code analysis fire-and-forget |
| POST | `/api/webhooks` | bearer | line 223; creates GitHub or GitLab webhook; URL is validated against SSRF via `validateUrl` |
| DELETE | `/api/webhooks/:integrationId/:serviceType/:webhookId` | bearer | line 379 |

Webhook ingress endpoints (`POST /api/webhooks/github`,
`/api/webhooks/gitlab`, `verify-signature`) live in
`routes/webhooks.js`; not opened in Step 2.

## Outbound dependencies

- **Core API** at `CORE_API_URL` (default
  `http://core-api:8080`) — fetches integration config, updates it
  with new webhook secret/id, deletes config on webhook removal.
  Forwards the user's `Authorization` header.
- **GitHub API** and **GitLab API** for OAuth and webhook
  CRUD; provider details in `services/webhookService.js`.
- **code-analyzer** at `CODE_ANALYZER_URL` (default
  `http://code-analyzer:8090`) with `ANALYZER_API_SECRET`,
  via `services/webhookHandler.js#triggerCodeAnalysis`.
- **RabbitMQ** consumer (`startEventConsumer` from
  `services/eventConsumer.js`) when `RABBITMQ_ENABLED !== "false"`
  (line 540), queue
  `RABBITMQ__INTEGRATIONQUEUE` (default `devhunt.integrations`).
- **OpenObserve** for traces and logs.

## What I should NOT assume

- **Two webhook handlers reference an undeclared `axios`**
  (lines 274 and 460) instead of the imported `axiosClient` (line
  10). The other call sites in the same file use `axiosClient`
  correctly. If those code paths execute, Node will throw
  `axios is not defined`. Did not run the code in Step 2 to
  confirm reachability — flagged here so future-me investigates
  before assuming the webhook create/delete paths are healthy.
- **`/api/webhooks` (without auth on the router) is different
  from the bearer-protected `POST /api/webhooks` defined inline
  at line 223.** Both are mounted at the same path prefix; Express
  resolves the inline route first if its method/path matches,
  otherwise the router handles it. Don't conflate the two when
  reasoning about auth.
- **The fire-and-forget code analysis trigger swallows errors**
  (lines 199–201) — failures in code analysis are logged at
  `error` but never propagated back to the sync caller. A "sync
  succeeded" response says nothing about analysis status.
- **JWT_SECRET-strict-validation only happens in production.** In
  development the env var can be empty and the service will
  start. Don't rely on local boot success as a security check.
