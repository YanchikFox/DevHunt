---
title: Chat & Channels — DMs, group chats, project channels, reactions, pins
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/ChatController.cs
  - DevHunt.CoreApi/Controllers/ProjectChannelsController.cs
  - DevHunt.CoreApi/Controllers/ChannelMembersController.cs
  - DevHunt.CoreApi/Controllers/AdminChatController.cs
  - DevHunt.CoreApi/Hubs/ChatHub.cs
  - DevHunt.CoreApi/Services/Chat/ChatService.cs
  - DevHunt.CoreApi/Services/Chat/ProjectChannelService.cs
  - DevHunt.CoreApi/Services/Chat/ChannelMemberService.cs
  - DevHunt.Infrastructure/DevHuntDbContext.cs
  - frontend/src/app/[locale]/dashboard/chats/
  - frontend/src/app/[locale]/dashboard/messages/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Real-time messaging with three flavors sharing one
`Conversations` table: **direct (1:1)**, **group**, and
**project channel** (Discord/Slack-style, per-project slug,
roles, ban list). Channels carry custom roles via
`ChannelRoleDefinition`. Messages support edit, delete,
reactions, pins, and replies. Live updates flow through
SignalR `/chatHub`. Profanity is filtered at the write path
through MVC action filters.

## Surface

### HTTP — `/api/chat` (DM / group / cross-flavor)

[ChatController.cs](DevHunt.CoreApi/Controllers/ChatController.cs):

- `GET /conversations`
- `POST /conversations/direct/{otherUserId}` — get-or-create DM
- `POST /conversations/project/{projectId}` — open project chat
- `POST /conversations/group` — create group
- `GET /conversations/{conversationId}/messages`
- `POST /conversations/{conversationId}/messages`
- `PUT /messages/{messageId}` — edit
- `DELETE /messages/{messageId}`
- `POST /messages/{messageId}/reactions`
- `PUT /messages/{messageId}/pin`
- `POST /conversations/{conversationId}/participants/{userId}` —
  add (group only)
- `DELETE /conversations/{conversationId}/participants/{userId}`
- `POST /conversations/{conversationId}/leave`
- `PUT /conversations/{conversationId}/mute`
- `GET /conversations/{conversationId}/participants`

### HTTP — project channels

[ProjectChannelsController.cs](DevHunt.CoreApi/Controllers/ProjectChannelsController.cs)
(`/api/projects/{projectId}/channels`):

- `GET /`, `POST /`, `PATCH /{channelId}`,
  `DELETE /{channelId}`, `POST /{channelId}/join`,
  `POST /{channelId}/leave`

[ChannelMembersController.cs](DevHunt.CoreApi/Controllers/ChannelMembersController.cs)
(`/api/projects/{projectId}/channels/{channelId}/members`):

- `GET /`, `GET /banned`, `GET /candidates`
- Role definitions: `GET /roles`, `POST /roles`,
  `PUT /roles/{roleId}`, `DELETE /roles/{roleId}`
- Membership: `POST /`, `PATCH /{targetUserId}`,
  `DELETE /{targetUserId}`,
  `POST /{targetUserId}/ban`, `POST /{targetUserId}/unban`

### HTTP — admin cleanup

[AdminChatController.cs](DevHunt.CoreApi/Controllers/AdminChatController.cs)
(`/api/admin/chat`):

- `GET /stats`, `GET /conversations`,
  `GET /conversations/{id}/messages`,
  `DELETE /conversations/{id}`,
  `DELETE /conversations/{id}/messages/{messageId}`,
  `POST /cleanup-orphaned`

### SignalR — `/chatHub`

Hub method on the client → invokes
[ChatHub.cs](DevHunt.CoreApi/Hubs/ChatHub.cs):

- `JoinConversation(Guid)` (line 125),
  `LeaveConversation(Guid)` (line 157)
- `JoinProject(Guid)` (line 181),
  `LeaveProject(Guid)` (line 218)
- `SendMessage(Guid conversationId, string content)` (line 243)
- `MarkAsRead(Guid conversationId)` (line 279)
- `Typing(Guid conversationId, bool isTyping)` (line 311)

Server pushes (sent to clients):

- `Error` — fault notice (Caller only)
- `UserJoined` / `UserLeft` — conversation group events
- `ReceiveMessage`, `MessageEdited`, `MessageDeleted` — pushed
  by `ChatService` from the HTTP write path (lines 537, 604,
  639) **and** the `SendMessage` hub method
- Reaction / pin updates — `ChatService.cs:690, 724` (event
  names not enumerated in this pass)
- `MessageRead` — `OthersInGroup` only (line 298)
- `UserTyping` — `OthersInGroup` only (line 347)

Group naming: `conversation:{id}` for per-conversation rooms.
Project-level join/leave hub methods exist (lines 181/218) but
the per-project group naming was not read in this pass — open
ChatHub.cs around those lines before relying on the format.

### UI routes

- DMs / groups —
  [frontend/src/app/[locale]/dashboard/messages/](frontend/src/app/[locale]/dashboard/messages/)
- Project channels surface —
  [frontend/src/app/[locale]/dashboard/chats/](frontend/src/app/[locale]/dashboard/chats/)
  (with `_components/` colocated). The split between `messages/`
  and `chats/` was not deeply explored; the URL semantics here
  are inferred from naming and may be reversed.

## Entities involved

- `Conversation` (direct / group / channel — distinguished by
  `Type` enum; `Type=2` is channel per the filtered unique slug
  index in `OnModelCreating`)
- `ConversationParticipant` (with banned-by + custom role
  pointer)
- `ChannelRoleDefinition` (table `Channel_Role_Definitions`,
  Cascade delete with conversation)
- `Message` (jsonb `AiMetadataJson`)
- `MessageReaction` (unique on `MessageId, UserId, Emoji`)

Plus `AiMessageDetails` (1:1 with Message) is co-located but is
the AI-planning domain's responsibility.

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — controllers,
  services, hub.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  schema and constraints (filtered unique slug, jsonb,
  `Channel_Role_Definitions` table).
- [systems/api-gateway.md](../systems/api-gateway.md) — only the
  gateway forwards `Upgrade: websocket` to `/chatHub`. `/api/`
  routes intentionally do not (Step-2 finding).
- [systems/frontend.md](../systems/frontend.md) — UI plus the
  rewrites for `/{locale}/chatHub/*` → core-api.
- [systems/notification-service.md](../systems/notification-service.md) —
  message events can fan out to email/push when the bus is
  wired and a recipient is offline (out-of-band; not deeply
  verified).

## Known traps

- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  message events publish via `IEventBusService`; without
  RabbitMQ, downstream consumers (notifications, ml-service
  consumers) hear nothing.
- [gotchas/encryption-key-rotation-cross-domain.md](../gotchas/encryption-key-rotation-cross-domain.md) —
  `ChatService` and `ChatHub` depend on `IEncryptionService`
  for message content; rotating the platform encryption key
  makes historical messages unreadable.
- **`Conversations.Slug` is filtered-unique only for `Type=2`
  channels** — the SQL filter
  `"Type" = 2 AND "Slug" IS NOT NULL AND "ProjectId" IS NOT NULL`
  lives at [DevHuntDbContext.cs:193](DevHunt.Infrastructure/DevHuntDbContext.cs#L193).
  Direct/group conversations can collide on slug freely (they
  shouldn't have one anyway).
- **Profanity filter is in-process and embedded.** The word
  list lives at `Filters/profanity_words.json` (embedded
  resource of CoreApi). Hot-updating words requires a redeploy.

## What I should NOT assume

- **Message edits overwrite.** No `MessageEditHistory` table
  exists at this commit. The `ReceiveMessage` →
  `MessageEdited` UI swap is the only record of an edit unless
  audit logging captures it (not verified).
- **`Type` discriminator values.** Step-2 evidence only confirms
  `Type=2` is a channel. The exact mapping of 0/1 (direct,
  group?) was not read; open
  [Models/Conversation.cs](DevHunt.Infrastructure/Models/Conversation.cs)
  before encoding the literal in new code.
- **Admin delete is hard delete.** `AdminChatController.cs:213,
  245` are `DELETE` verbs and the cascade rules in DbContext
  treat reactions/AI details as Cascade. Once gone, gone — there
  is no soft-delete-with-restore here, unlike tasks-kanban.
- **`POST /conversations/project/{projectId}` is "open the
  default channel," not "create a new channel."** Channel
  creation is `POST /api/projects/{projectId}/channels`.
- **`MarkAsRead` does not update a per-message read receipt.**
  Pushes a `MessageRead` event with the user id and timestamp
  to the conversation group; persistent state, if any, was not
  read in this pass.
- **`/chatHub` group format.** Confirmed only `conversation:{id}`.
  The project-level group format used by `JoinProject` is
  unverified. Don't fan out to a guessed group name.
