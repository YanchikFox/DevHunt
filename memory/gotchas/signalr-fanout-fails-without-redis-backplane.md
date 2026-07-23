---
title: SignalR cross-replica fan-out silently fails without Redis backplane
type: gotcha
status: verified
sources:
  - DevHunt.CoreApi/Extensions/InfrastructureExtensions.cs
  - DevHunt.CoreApi/Hubs/ChatHub.cs
  - DevHunt.CoreApi/Hubs/NotificationHub.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## The trap

`AddSignalRWithRedis` (InfrastructureExtensions.cs:62) wires
a Redis backplane conditionally:

```
if Features:Redis:Enabled AND RedisConnection set → backplane ON
else                                              → backplane OFF
```

Without the backplane, `Groups.AddToGroupAsync` is in-process:
group membership lives in the memory of the instance that
handled the client's WebSocket connection. When the server
calls `Clients.Group("conversation:{id}").SendAsync(...)`, only
clients whose WebSocket landed on **that same replica** receive
the message. Clients on other replicas are silently skipped.

No error is thrown. No log entry marks the dropped delivery.
From the sender's perspective the call succeeds; from the
recipient's perspective the message never arrives.

## Affected surfaces

- **Chat**: `ChatHub` — `conversation:{id}` group; any replica
  that handles `SendMessage` or an HTTP write via `ChatService`
  fans out only to its local clients.
- **Notifications**: `NotificationHub` — `user:{userId}` group;
  `NotificationHubExtensions.SendNotificationToUserAsync` hits
  `IHubContext<NotificationHub>.Clients.Group(...)`, which
  likewise is in-process only without the backplane.

## Effect under scale-out

A user connected to replica B misses all messages sent from
replica A, even when both are correctly authenticated. The bug
manifests as intermittent message loss correlated with which
replica the connection lands on — hard to reproduce locally
but consistent under round-robin load balancing.

## When this fires

Whenever `Features:Redis:Enabled` is false or `RedisConnection`
is absent and Core API has more than one replica. The default
`appsettings.Development.json` disables Redis, so a development
deployment with multiple instances would reproduce this.

## See also

[cross-cutting/scale-out-readiness.md](../cross-cutting/scale-out-readiness.md) —
architectural overview of all single-instance-safe / scale-out-unsafe
patterns in DevHunt.
