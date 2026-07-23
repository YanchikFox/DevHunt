---
title: Notifications — inbox, real-time push, async fan-out to email/SMS/push
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/NotificationsController.cs
  - DevHunt.CoreApi/Hubs/NotificationHub.cs
  - DevHunt.CoreApi/Services/NotificationHelperService.cs
  - DevHunt.CoreApi/Services/NotificationServiceClient.cs
  - notification-service/src/index.js
  - frontend/src/app/[locale]/dashboard/notifications/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Two parallel paths sharing the word "notification":

1. **In-app inbox** — `Notifications` rows in Core API's
   PostgreSQL, listed and managed by the user, plus a SignalR
   real-time push to the active session.
2. **Out-of-band delivery** — email / SMS / push, handled by
   the separate **notification-service** Node.js deployable,
   driven by RabbitMQ events emitted from Core API.

The two paths are independent: a notification can land in the DB
without an email going out, and an alert webhook can deliver
email without writing a row in `Notifications`. Don't conflate.

## Surface

### HTTP — Core API

[NotificationsController.cs](DevHunt.CoreApi/Controllers/NotificationsController.cs)
(`/api/notifications`):

- `GET /` — paged list for the current user
- `GET /unread-count`
- `POST /mark-read/{id}`, `POST /mark-all-read`
- `POST /` — create (admin / privileged path; auth check inside
  controller, not enumerated here)
- `DELETE /{id}`, `DELETE /` — delete one / clear all

### HTTP — notification-service (out-of-band)

Documented in [systems/notification-service.md](../systems/notification-service.md).
Relevant inbound routes called by Core API (or other internal
callers) on the notification-service deployable:

- `POST /api/notifications` — single-recipient fan-out
- `POST /api/notifications/bulk` — **stubbed recipient**, see
  trap below
- `PUT  /api/notifications/:id/read`,
  `PUT /api/notifications/user/:userId/read-all` —
  no-op responses today
- `POST /api/alerts/webhook` — OpenObserve alert ingress; **no
  auth**, internal network only

### SignalR — `/notificationHub`

[NotificationHub.cs](DevHunt.CoreApi/Hubs/NotificationHub.cs):

- On connect (line 40), the hub joins the caller into a per-user
  group named `user:{userId}`.
- On disconnect (line 70), the hub releases membership.
- No client-callable methods at this commit — the hub is
  push-only.

Server pushes use static helpers:

- `NotificationHubExtensions.SendNotificationToUserAsync`
  (line 105) — pushes one event named `Notification` to
  `Group("user:{userId}")`.
- `NotificationHubExtensions.SendNotificationToUsersAsync`
  (line 119) — fan-out the same event to multiple users in
  parallel (`Task.WhenAll`).

The Group naming `user:{userId}` is the contract — don't push to
a different format.

### UI routes

- Inbox —
  [frontend/src/app/[locale]/dashboard/notifications/](frontend/src/app/[locale]/dashboard/notifications/)
- The frontend client subscribes to `/notificationHub` via
  `@microsoft/signalr` (per `frontend/package.json`); routing
  rewrites in [next.config.mjs](frontend/next.config.mjs) and
  the nginx gateway both honor it.

## Entities involved

- `Notification` — single table; `UserId` (recipient),
  polymorphic `RelatedEntityId`, `Type` and `Title`/`Content`
  string fields. No FK enforcement on `RelatedEntityId`.

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — REST + hub
  push; `NotificationHelperService` is the in-process glue,
  `NotificationServiceClient` calls out to the deployable.
- [systems/notification-service.md](../systems/notification-service.md) —
  the deliverer; consumes RabbitMQ
  (`RABBITMQ__NOTIFICATIONQUEUE`, default
  `devhunt.notifications`).
- [systems/infrastructure.md](../systems/infrastructure.md) — the
  `Notifications` table.
- [systems/api-gateway.md](../systems/api-gateway.md) — forwards
  `/notificationHub` with WebSocket Upgrade and a 24-hour read
  timeout (per Step-2).
- [systems/frontend.md](../systems/frontend.md) — inbox UI and
  the SignalR client.

## Known traps

- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  with the bus disabled, the **out-of-band** path is dead. The
  in-app inbox + SignalR push still works (they don't go through
  RabbitMQ).
- [gotchas/notification-bulk-userid-localhost-placeholder.md](../gotchas/notification-bulk-userid-localhost-placeholder.md) —
  the `POST /api/notifications/bulk` endpoint on the
  notification-service ships with a `${userId}@devhunt.local`
  recipient stub. Intentional, do not auto-fix.
- **The OpenObserve webhook (`POST /api/alerts/webhook`)
  bypasses auth.** It's reachable from anything inside the
  Docker network. Don't expose its port through the gateway
  without adding auth in front.

## What I should NOT assume

- **Two parallel "read" states.** A notification has a read
  state in Core API's DB (mutated by
  `POST /api/notifications/mark-read/{id}`). The notification-
  service has its own no-op endpoints with a similar shape
  (`PUT /api/notifications/:id/read`) that today return success
  without persistence. Don't expect cross-service consistency.
- **`POST /api/notifications` (Core API) is not the public
  send-a-notification API.** It is permission-gated — outside
  callers should publish events that trigger
  `NotificationHelperService` to write the row. The direct POST
  is for admin tooling.
- **Real-time push is per-connection, not per-user.** A user
  with two open tabs sees two `Notification` events. If
  client-side code assumes idempotency, that's a bug there, not
  a duplication on the server.
- **No retry on the push.** If the user is offline, the SignalR
  push silently misses; the row is still in the DB and will be
  visible on next inbox load. Out-of-band email retry, if any,
  is the notification-service's job — not the hub's.
- **Group membership lifetime equals connection lifetime.**
  Reconnects (SignalR auto-reconnect) re-add the user to
  `user:{userId}` via `OnConnectedAsync`; manual cleanup is
  unnecessary.
