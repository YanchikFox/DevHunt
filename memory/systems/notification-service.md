---
title: notification-service — Express notifications + RabbitMQ consumer
type: system
status: verified
sources:
  - notification-service/src/index.js
  - notification-service/src/telemetry.js
  - notification-service/src/metrics.js
  - notification-service/package.json
  - notification-service/Dockerfile
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Node.js 20+ ESM service (Express 5) that delivers user
notifications across email/SMS/push, plus accepts OpenObserve alert
webhooks and forwards critical alerts to admins by email. Has
both a synchronous HTTP API (called by Core API) and an async
RabbitMQ consumer.

Compose service `notification-service`, port `PORT_NOTIFICATION`
(default 5003).

## Entry-point

[notification-service/src/index.js](notification-service/src/index.js)

`telemetry.js` is imported as the first statement (line 8) so
OpenTelemetry instrumentation is in place before any other module
loads.

## Boot wiring

- **CORS** (lines 33–60): origins from `CORS_ALLOWED_ORIGINS`
  (comma-split). In production: throws if unset, throws on `*`.
- **Body limits** (lines 63–64): `5mb` for JSON and urlencoded.
- **Middleware order** (lines 67–83): `requestContext` (request id
  + child logger) → `metricsMiddleware` → `rateLimit` → `throttle`
  (custom backpressure, exposes `/metrics/throttle`) → request
  logging.
- **RabbitMQ consumer** (lines 415–419): started inside
  `app.listen` callback when `RABBITMQ_ENABLED !== "false"`
  (default on); failure is logged but not fatal.
- **Graceful shutdown** on `SIGTERM` (lines 423–429).

## Endpoints

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET    | `/health` | none | static healthy response (line 86) |
| GET    | `/`       | none | service map |
| GET    | `/metrics` | none | Prometheus text |
| GET    | `/metrics/throttle` | none | throttle internals |
| POST   | `/api/notifications` | bearer | dispatches to email/sms/push by `Type` (line 104) |
| POST   | `/api/notifications/bulk` | bearer | parallel email send via `Promise.allSettled` (line 247) |
| PUT    | `/api/notifications/:id/read` | bearer | (currently a no-op response, line 296) |
| PUT    | `/api/notifications/user/:userId/read-all` | bearer + ownership/admin | line 311 |
| POST   | `/api/alerts/webhook` | **none** | OpenObserve webhook → admin email on `critical`/`high`; HTML escaped (line 338) |

The `Type` switch at line 144 normalizes case and supports `email`,
`sms`, `push`. Anything else → 400.

## Outbound dependencies

- **SMTP** (mailpit in compose) via `services/emailService.js`.
- **SMS** provider via `services/smsService.js` (not read in Step 2).
- **Push** provider via `services/pushService.js` (not read in Step 2).
- **RabbitMQ** for async consumption
  (`services/rabbitmqConsumer.js`); queue name from
  `RABBITMQ__NOTIFICATIONQUEUE` (default `devhunt.notifications`).
- **OpenObserve** for traces (`telemetry.js`) and logs.

## What I should NOT assume

- **The `/api/alerts/webhook` endpoint has no authentication.**
  The inline comment (line 337) says "internal network only" — in
  practice it's reachable through any network where the container
  port is exposed. Treat the docker network as the only trust
  boundary; do NOT expose this port via nginx without auth in
  front.
- **Bulk send uses `${userId}@devhunt.local` as the recipient
  (line 250).** The comment admits this is a placeholder ("в
  реальности нужно получать email из БД"). The bulk path is real
  code shipped to production but does not actually send to the
  user's email — keep this in mind before recommending it for
  any real fan-out.
- **`/api/notifications/:id/read` does nothing persistent**
  (lines 291–308). The handler returns success without touching a
  database. If the user expects "marked as read" to survive a
  refresh, this service alone won't deliver it.
- **`type` parameter spelling matters** in the error path at
  line 189 — it references the original (possibly undefined)
  `type` variable rather than `finalType`/`normalizedType`.
  Future-me: double-check before quoting that error string in
  client tests.
- **No DB connection here.** This service is stateless except for
  the throttle metrics buffer. Persistence belongs to Core API.
