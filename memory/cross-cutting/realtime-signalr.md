---
title: Real-time / SignalR — hubs, groups, presence, Redis backplane
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Hubs/ChatHub.cs
  - DevHunt.CoreApi/Hubs/NotificationHub.cs
  - DevHunt.CoreApi/Services/PresenceService.cs
  - DevHunt.CoreApi/Extensions/InfrastructureExtensions.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

Two SignalR hubs mounted on Core API, backed by an optional
Redis backplane and a lightweight distributed presence
tracker. All WebSocket connections pass through the Nginx
gateway (which forwards `Upgrade: websocket` — see
[systems/api-gateway.md](../systems/api-gateway.md)).

## Components

| Class | Path | Role |
|---|---|---|
| `ChatHub` | Hubs/ChatHub.cs | real-time chat: messages, reactions, pins, typing |
| `NotificationHub` | Hubs/NotificationHub.cs | in-app notification delivery per user |
| `PresenceService` | Services/PresenceService.cs | online/offline tracker via distributed cache |
| `NotificationHubExtensions` | Hubs/NotificationHub.cs:97 | helper for pushing from controllers/services |
| `InfrastructureExtensions.AddSignalRWithRedis` | Extensions/InfrastructureExtensions.cs:62 | conditional Redis backplane wiring |

## Hubs

### `/chatHub` → `ChatHub`

`[Authorize]` — JWT required at connection time.
`IEncryptionService` injected for message content.
`IPresenceService` called with `source="chat"`.

**Group naming (confirmed):**
- Per-conversation: `conversation:{conversationId}` (line 148)
- Per-project: group format not read in this pass — open
  ChatHub.cs around lines 181/210 before relying on the name.

**Client → server methods (hub invocations):**

| Method | Line | Effect |
|---|---|---|
| `JoinConversation(Guid)` | 125 | AddToGroup conversation:{id} |
| `LeaveConversation(Guid)` | 157 | RemoveFromGroup |
| `JoinProject(Guid)` | 181 | AddToGroup project-level group |
| `LeaveProject(Guid)` | 218 | RemoveFromGroup |
| `SendMessage(Guid, string)` | 243 | saves + pushes ReceiveMessage |
| `MarkAsRead(Guid)` | 279 | pushes MessageRead to OthersInGroup |
| `Typing(Guid, bool)` | 311 | pushes UserTyping to OthersInGroup |

**Server → client events (also fired from HTTP write path via `ChatService`):**
`UserJoined`, `UserLeft`, `ReceiveMessage`, `MessageEdited`,
`MessageDeleted`, reaction/pin events (names not enumerated —
see ChatService.cs:690, 724), `MessageRead`, `UserTyping`,
`Error` (Caller only).

### `/notificationHub` → `NotificationHub`

`[Authorize]` — JWT required.
No `DevHuntDbContext` injection — user-active status is NOT
re-checked on connection (comment at line 58 notes this explicitly).
`IPresenceService` called with `source="notify"`.

**Group naming (confirmed):**
- Personal: `user:{userId}` (line 62)

**Server → client events:**
- `Notification` — pushed via `NotificationHubExtensions.SendNotificationToUserAsync`
  which targets `Clients.Group("user:{userId}")`

No client → server methods defined (notification-only hub;
clients only listen).

## SignalR configuration (Program.cs:114-121)

```csharp
options.EnableDetailedErrors = IsDevelopment()  // false in prod
options.MaximumReceiveMessageSize = 8192         // 8 KB
```

## Redis backplane (InfrastructureExtensions.cs:64-87)

```
if Features:Redis:Enabled AND RedisConnection set:
    AddStackExchangeRedis(redisConnection)
    ChannelPrefix = "DevHunt:SignalR"
```

Without the backplane, `Groups.AddToGroupAsync` is in-process
only. A message broadcast to `conversation:{id}` on replica A
does not reach clients connected to replica B.

## Presence

`PresenceService` backed by `IDistributedCache` (Redis in prod,
in-memory in dev).

- Key format: `presence:{userId}:{source}`
  — source is `"chat"` or `"notify"` (hardcoded array in Sources)
- Sliding TTL: **2 minutes** per key
- Online = any source key exists
- All cache operations are fail-safe (exception → LogWarning,
  `IsOnline` returns `false`)

## Known traps

- [gotchas/signalr-fanout-fails-without-redis-backplane.md](../gotchas/signalr-fanout-fails-without-redis-backplane.md) —
  without Redis backplane, `Groups.SendAsync` reaches only
  clients on the same replica; delivery fails silently across
  replicas for both chat and notifications.
- **Presence TTL is 2 minutes sliding.** A connected but idle
  client (no SignalR traffic for 2 min) will have its presence
  key expire and appear offline even while the WebSocket is
  still open. Presence is refreshed only on hub connect/disconnect;
  there is no heartbeat keepalive write.
- **`NotificationHub` does not check `user.IsActive`.** The
  class comment (line 58) explicitly notes this — `[Authorize]`
  validates the JWT, but a blocked user whose token has not yet
  expired can connect to `/notificationHub` and receive events.
  `ChatHub` has the same limitation (no active-check on
  connection, only on HTTP requests via `UserActiveCheckMiddleware`).
- **Message size limit is 8 KB.** Any single SignalR frame
  larger than `MaximumReceiveMessageSize` will be rejected.
  Chat message content that can be large (e.g. AI-assisted
  replies) must stay under this limit after JSON serialization.

## What I should NOT assume

- **Project-level group naming in ChatHub is unverified.**
  `JoinProject` / `LeaveProject` use a group name that was not
  read in this pass. Don't hard-code a guessed format.
- **`NotificationHubExtensions` is the only server-push helper
  for notifications.** Controllers that need to push
  notifications inject `IHubContext<NotificationHub>` and call
  the extension methods — this is the canonical pattern. The
  hub itself has no inbound methods.
- **Redis backplane prefix is `DevHunt:SignalR`, not `DevHunt:`.**
  Redis keys for SignalR groups will be under this prefix.
  If filtering Redis keys for debugging, use this prefix.
