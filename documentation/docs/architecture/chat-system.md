---
sidebar_position: 4
title: Chat System
description: SignalR chat, notifications, presence, group routing, and Redis backplane behavior.
sidebar_label: Chat System
---

# Chat System

> _If any detail here contradicts the code, trust the code — not this page._

DevHunt real-time features live in Core API. There are two SignalR hubs: one for chat and one for in-app notification delivery. Both require JWT authorization at connection time and both are reached through the Nginx gateway.

## Hubs

| Hub               | Path               | Purpose                                               | Inbound methods                    |
| ----------------- | ------------------ | ----------------------------------------------------- | ---------------------------------- |
| `ChatHub`         | `/chatHub`         | real-time chat messages, typing, reads, joins, leaves | yes                                |
| `NotificationHub` | `/notificationHub` | per-user notification push                            | no public client-to-server methods |

Core API configures SignalR with:

- `MaximumReceiveMessageSize = 8192`.
- detailed errors only in Development.
- optional Redis backplane with channel prefix `DevHunt:SignalR`.

## ChatHub Flow

`ChatHub` supports:

- `JoinConversation(Guid)` and `LeaveConversation(Guid)`.
- `JoinProject(Guid)` and `LeaveProject(Guid)`.
- `SendMessage(Guid, string)`.
- `MarkAsRead(Guid)`.
- `Typing(Guid, bool)`.

The confirmed conversation group name is `conversation:{conversationId}`. Project-level group naming exists in the hub, but the memory entry intentionally does not hard-code it; check `ChatHub.cs` before relying on the exact string.

Server-to-client events include joins/leaves, message receipt, message edit/delete, reactions, pin changes, reads, typing, and caller-only errors.

## NotificationHub Flow

`NotificationHub` adds each connection to personal group `user:{userId}`. Server-side code sends notifications through `NotificationHubExtensions.SendNotificationToUserAsync`, targeting that personal group.

The hub does not expose inbound client actions. Clients connect and listen.

## Presence

Presence is tracked by `PresenceService` through `IDistributedCache`.

| Detail           | Value                                               |
| ---------------- | --------------------------------------------------- |
| Key format       | `presence:{userId}:{source}`                        |
| Sources          | `chat`, `notify`                                    |
| TTL              | 2 minutes sliding                                   |
| Online check     | user is online if any source key exists             |
| Failure behavior | cache errors log warnings; `IsOnline` returns false |

Presence is refreshed on hub connect/disconnect paths, not by a separate heartbeat write. An idle connected user can appear offline after the TTL expires.

## Redis Backplane Requirement

With Redis configured and enabled, SignalR uses the Redis backplane and broadcasts can reach clients connected to different Core API replicas.

Without the backplane, SignalR group membership is process-local:

- A user connected to replica A can join `conversation:{id}` on replica A.
- A message broadcast from replica B to the same group does not reach that user.
- The failure is silent from the application user's point of view.

This matters for both chat and notification delivery.

## Auth And Active-User Caveat

Both hubs use `[Authorize]`, so the JWT must validate at connection time. The standard HTTP `UserActiveCheckMiddleware` is not part of the hub connection path in the same way it is for controller requests.

`NotificationHub` explicitly does not re-check `user.IsActive` on connection, and the memory entry records the same limitation for `ChatHub`. A blocked user with an unexpired token can still connect until the token expires or hub-specific checks are added.

## Message Size Caveat

The SignalR receive limit is 8 KB. Large chat payloads, especially AI-assisted content serialized through JSON, must stay below that limit or the frame will be rejected.
