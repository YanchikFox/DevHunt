---
title: Projects & Teams — project CRUD, lifecycle, team, invitations, roles
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/ProjectsController.cs
  - DevHunt.CoreApi/Controllers/ProjectTeamController.cs
  - DevHunt.CoreApi/Controllers/InvitationsController.cs
  - DevHunt.CoreApi/Controllers/ProjectLifecycleController.cs
  - DevHunt.CoreApi/Controllers/ProjectSkillsController.cs
  - DevHunt.CoreApi/Controllers/ProjectSubscriptionsController.cs
  - DevHunt.CoreApi/Services/Projects/
  - frontend/src/app/[locale]/(public)/projects/
  - frontend/src/app/[locale]/dashboard/projects/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

The unit users actually rally around: a *Project* with an owner,
a tech-stack, open roles, subscribers, optional public slug, and
a team. Owners invite users; users send applications to projects.
Projects move through a lifecycle (`Draft → Active → Completed |
Archived | Cancelled` per `Models/ProjectStatus`), can be made
public via Showcase, and can be "boosted" by users for ranking.

## Surface

### HTTP — Core API

[DevHunt.CoreApi/Controllers/ProjectsController.cs](DevHunt.CoreApi/Controllers/ProjectsController.cs)
(`/api/projects`):

- `GET /` — list with filtering (delegated to
  `ProjectFilterService`, `TechStackMatcher`)
- `GET /{id}`, `GET /by-slug/{slug}`,
  `GET /{id}/permissions`
- `POST /`, `PUT /{id}`, `DELETE /{id}`
- `PATCH /{id}/status`, `/visibility`, `/settings`
- `POST /{id}/toggle-boost`,
  `POST /{id}/transfer-ownership`

[ProjectTeamController.cs](DevHunt.CoreApi/Controllers/ProjectTeamController.cs)
(`/api/projects/{projectId}/team`):

- `GET /`, `GET /members`, `POST /members`,
  `POST /members/leave`, `DELETE /members/{userId}`,
  `PATCH /{memberId}/permissions`
- `GET /roles`, `POST /roles`,
  `POST /roles/transfer-leadership`

[ProjectLifecycleController.cs](DevHunt.CoreApi/Controllers/ProjectLifecycleController.cs)
(under `/api/projects/{id}`):

- `POST /archive`, `/unarchive`, `/publish`, `/unpublish`,
  `/activate`, `/complete`, `/cancel` — each is a status
  transition with permission checks via
  `IProjectPermissionService`.

[ProjectSkillsController.cs](DevHunt.CoreApi/Controllers/ProjectSkillsController.cs)
(`/api/projects/{projectId}/skills`):

- `GET /`, `POST /`, `PUT /{projectSkillId}`,
  `DELETE /{projectSkillId}` — manages `ProjectTechStack`
  rows (junction with `Skill`).

[ProjectSubscriptionsController.cs](DevHunt.CoreApi/Controllers/ProjectSubscriptionsController.cs)
(`/api/projects/{projectId}/subscriptions`):

- `POST /`, `DELETE /` — subscribe/unsubscribe to project news
  feed (writes `ProjectSubscription`).

[InvitationsController.cs](DevHunt.CoreApi/Controllers/InvitationsController.cs)
(`/api/invitations`):

- `POST /send`, `GET /incoming`, `GET /sent`,
  `POST /respond`, `DELETE /{invitationId}` — both
  *invitations* (owner → user) and *applications* (user →
  project) live in the `Invitations` table, distinguished by
  `Invitation.Type` (added by migration `AddInvitationType`).

### UI routes

- Public —
  [frontend/src/app/[locale]/(public)/projects/](frontend/src/app/[locale]/(public)/projects/)
- Dashboard project workspace —
  [frontend/src/app/[locale]/dashboard/projects/[id]/](frontend/src/app/[locale]/dashboard/projects/[id]/)
  with `_components/` and `_hooks/` colocated; the kanban surface
  lives here too (see `tasks-kanban` domain).
- Team management —
  [frontend/src/app/[locale]/dashboard/teams/](frontend/src/app/[locale]/dashboard/teams/)
- Invitations inbox —
  [frontend/src/app/[locale]/dashboard/invitations/](frontend/src/app/[locale]/dashboard/invitations/)

### SignalR

No project-specific hub in this domain. Real-time presence and
project chat live in `chat-and-channels`.

## Entities involved

- `Project`, `ProjectBoost` (composite key, both Cascade)
- `TeamMember` (junction Project ↔ User)
- `ProjectRole` (open roles + recruitment ad)
- `ProjectTechStack` (junction Project ↔ Skill)
- `ProjectSubscription`
- `Invitation` (with `Type` discriminating invitation vs
  application)
- `OpenRoleEntry` — a value object inside
  [Project.cs:9](DevHunt.Infrastructure/Project.cs#L9), wrapped
  by `Project.OpenRoles`. Not a DbSet; treat as embedded data.

Authoritative locations: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — every endpoint
  above; `ProjectServicesFacade`, `ProjectPermissionService`,
  `ProjectFilterService`, `TechStackMatcher` carry the logic.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  the entities and slug-uniqueness filtered index.
- [systems/notification-service.md](../systems/notification-service.md) —
  consumes invitation/team-change events for email; delivery is
  best-effort and conditional on RabbitMQ being wired.
- [systems/frontend.md](../systems/frontend.md) — UI routes
  above.

## Known traps

- [gotchas/hand-rolled-json-text-columns.md](../gotchas/hand-rolled-json-text-columns.md) —
  `ProjectRole.RequiredSkillsJson` is plain text + manual JSON,
  not jsonb. EF can't filter inside it.
- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  invitation/team-change emails ride the bus; if it's a no-op,
  the user gets nothing.
- **`Slug` is a filtered unique index** — unique only when
  non-null. A project without a slug can co-exist with any
  number of others without slug; collision only fires when both
  rows have the same non-null slug. Migration source:
  `AddProjectSlugAndBoosts` (2026-04-21).
- **`Invitations.Type` distinguishes flows.** Same table, two
  user journeys. Filter explicitly when querying — the absence
  of `Type` in a `where` clause means you get both.

## What I should NOT assume

- **Lifecycle endpoints are not free-form mutators.** Each one
  enforces a status transition; calling `complete` on a `Draft`
  project will fail in the service layer, not pass through.
  The valid transitions live in `ProjectLifecycleController` /
  `ProjectStatus.cs` — read both before recommending a new
  transition.
- **Boosting is a toggle, not a count.** `POST /toggle-boost`
  flips the row's existence (composite key in `ProjectBoosts`
  on `(ProjectId, UserId)`). One user can boost a project at
  most once. Ranking that uses boost count just counts rows.
- **Team membership and project ownership are separate
  concepts.** `Project.OwnerId` is a column on `Projects`;
  `TeamMembers` is a side table. The owner is not necessarily a
  TeamMember row. Transferring ownership is a distinct
  operation from adding/removing team members.
- **Public visibility uses two layers.** `Project.Visibility`
  controls "is this visible to anonymous visitors", while
  `ShowcaseProject` (separate aggregate, separate domain) carries
  the curated public-portfolio variant. Don't conflate.
- **`ProjectFilterService` and `TechStackMatcher` do non-trivial
  matching** — listed for awareness; behavior not summarized
  here. Open the files when filtering recommendations need
  reasoning.
