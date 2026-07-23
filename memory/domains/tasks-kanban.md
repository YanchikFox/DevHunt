---
title: Tasks & Kanban — board, columns, links, attachments, GitHub sync
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/TasksController.cs
  - DevHunt.CoreApi/Controllers/TaskColumnsController.cs
  - DevHunt.CoreApi/Controllers/TaskLinksController.cs
  - DevHunt.CoreApi/Controllers/TaskAttachmentsController.cs
  - DevHunt.CoreApi/Controllers/TaskBoardSettingsController.cs
  - DevHunt.CoreApi/Controllers/GitHubTaskSyncController.cs
  - DevHunt.CoreApi/Services/Tasks/TaskAuthorizationService.cs
  - frontend/src/app/[locale]/dashboard/projects/[id]/_components/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

A per-project Kanban board: tasks move across user-defined
columns, link to each other (blocks, relates, duplicates …),
carry attachments (uploaded blobs or pointers to existing
`ProjectFile` rows), and can optionally bi-sync with a GitHub
repository's Issues. The domain is one of the larger surfaces in
Core API — `TasksController.cs` alone is 40K.

## Surface

### HTTP — primary (`/api/projects/{projectId:guid}/...`)

[TasksController.cs](DevHunt.CoreApi/Controllers/TasksController.cs)
(`tasks`):

- `GET /` — board listing
- `POST /` — create task
- `PUT /{taskId}` — full update
- `DELETE /{taskId}` — soft delete (see "What I should NOT
  assume")
- `POST /{taskId}/restore` — undo soft delete
- `POST /reorder` — bulk reorder within / across columns

[TaskColumnsController.cs](DevHunt.CoreApi/Controllers/TaskColumnsController.cs)
(`columns`):

- `GET /`, `POST /`, `PUT /{columnId}`, `DELETE /{columnId}`,
  `POST /reorder`

[TaskLinksController.cs](DevHunt.CoreApi/Controllers/TaskLinksController.cs)
(`tasks/{taskId}/links`):

- `GET /`, `POST /`, `DELETE /{linkId}`

[TaskAttachmentsController.cs](DevHunt.CoreApi/Controllers/TaskAttachmentsController.cs)
(`tasks/{taskId}/attachments`):

- `GET /`, `POST /` (upload blob), `POST /link` (link existing
  `ProjectFile`), `GET /{attachmentId}`,
  `DELETE /{attachmentId}`

[TaskBoardSettingsController.cs](DevHunt.CoreApi/Controllers/TaskBoardSettingsController.cs)
(`board-settings`):

- `GET /`, `PUT /` — board-level preferences (default column,
  WIP limits, etc. — fields per `TaskBoardSettings` entity)

### HTTP — internal GitHub sync (`/api/internal/github-tasks/*`)

[GitHubTaskSyncController.cs](DevHunt.CoreApi/Controllers/GitHubTaskSyncController.cs)
is invoked **by the integration-gateway**, not by browsers:

- `PATCH /projects/{projectId}/tasks/{taskId}/link` — bind a
  `TaskItem` to a GitHub issue (`TaskItem.GitHubIssueId`)
- `POST /projects/{projectId}/tasks` — create a task from a
  webhook event
- `PATCH .../{taskId}/complete` and `/reopen`,
  `PATCH .../{taskId}/content`, `PATCH .../{taskId}/labels`
- `GET /projects/{projectId}/tasks/by-issue/{gitHubIssueId}` —
  reverse lookup

Auth on this controller goes through
`IInternalServiceAuthenticator` (singleton from
[Program.cs:130](DevHunt.CoreApi/Program.cs#L130)) — it is **not**
the user JWT path. Treat it as a service-to-service contract.

### UI routes

The kanban surface lives entirely under
[frontend/src/app/[locale]/dashboard/projects/[id]/](frontend/src/app/[locale]/dashboard/projects/[id]/)
with logic colocated in `_components/` and `_hooks/`. There is
**no** standalone `/tasks/` route — the board is part of the
project workspace.

The domain uses `@dnd-kit/core` + `@dnd-kit/sortable` (per
`frontend/package.json`) for drag-and-drop, hitting
`POST /reorder` on drop.

### SignalR

No dedicated hub methods for tasks at this commit. Real-time
updates of the board across collaborators ride
`/notificationHub` notifications when wired (per
`NotificationHelperService`); this domain does not own that
plumbing.

## Entities involved

- `TaskItem` (DbSet name `Tasks` — note divergence)
- `TaskColumn`, `TaskLink` (Source/Target self-relation),
  `TaskAttachment`, `TaskBoardSettings`
- Cross-domain refs: `Project`, `User` (CreatedBy, AssignedTo),
  `ProjectFile` (linked attachments)

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — every endpoint
  above; `TaskAuthorizationService` enforces who can mutate what.
- [systems/integration-gateway.md](../systems/integration-gateway.md) —
  receives GitHub webhooks, then calls
  `/api/internal/github-tasks/*` on Core API. Bare `axios` typo
  caveat applies (see gotcha).
- [systems/frontend.md](../systems/frontend.md) — kanban UI.
- [systems/infrastructure.md](../systems/infrastructure.md) — the
  five Task* entities live here; created by migration
  `AddTaskBoardEnhancements` (2026-01-05).

## Known traps

- [gotchas/integration-gateway-bare-axios-references.md](../gotchas/integration-gateway-bare-axios-references.md) —
  affects the webhook-to-task path indirectly. If
  webhook-creation/deletion fail in the gateway, tasks won't
  link to a freshly registered repository's issues.
- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  task-changed events feed notifications; with the bus disabled,
  collaborators don't see real-time refreshes.
- **`/api/internal/github-tasks/*` is not in OpenAPI for clients.**
  It is a private contract between Core API and the gateway. If
  a frontend bug appears that "calls" these, that's a bug — they
  should not be invoked from the browser.

## What I should NOT assume

- **`DELETE /{taskId}` is a soft delete.** The `restore`
  endpoint at `TasksController.cs:495` confirms it. The exact
  flag column on `TaskItem` was not read in this pass — open
  [TaskItem.cs](DevHunt.Infrastructure/TaskItem.cs) before
  reasoning about how filtered queries exclude deleted rows.
  Update this note when verified.
- **Reorder is a bulk operation, not per-item.** `POST /reorder`
  receives a target ordering and rewrites positions. Don't
  emulate it with N single-task PUTs — racing reorders will
  fight each other.
- **Attachments come in two flavors.** `POST /` uploads a new
  blob (presumably to SeaweedFS via `S3ObjectStorageService`);
  `POST /link` reuses an existing `ProjectFile`. They map to
  the same `TaskAttachment` row but the lifetime story differs:
  blob attachments are owned by the task, linked attachments
  share a file with the project.
- **GitHub sync is one-directional from GitHub to DevHunt at this
  layer.** The gateway pushes webhook-derived state into Core
  API via `/api/internal/github-tasks/*`. Whether DevHunt edits
  push back to GitHub is *not* covered by these endpoints —
  open `IntegrationsController.cs` (integrations-oauth domain,
  later batch) before claiming bidirectional sync.
- **`TaskLink` Source/Target are both `TaskItem` FKs in the same
  project.** Cross-project links are not modeled. If a feature
  request implies them, a schema change is needed.
- **Board settings are 1:1 with Project.** A new project does not
  necessarily have a row — `GET /board-settings` may return
  defaults synthesized in code rather than a row. Verify by
  reading `TaskBoardSettingsController.cs` before assuming the
  `TaskBoardSettings` table is always populated.
