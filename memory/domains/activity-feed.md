---
title: Activity Feed — user activity timeline, project news posts, follow-driven feed
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/ActivitiesController.cs
  - DevHunt.CoreApi/Controllers/FeedController.cs
  - DevHunt.CoreApi/Controllers/ProjectNewsController.cs
  - DevHunt.CoreApi/Services/UserActivityService.cs
  - DevHunt.CoreApi/Services/Projects/ProjectNewsService.cs
  - DevHunt.Infrastructure/Models/ActivityRecord.cs
  - DevHunt.Infrastructure/Models/ProjectNewsPost.cs
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Three related but distinct surfaces:

1. **Activity timeline** — `ActivityRecord` rows describing what
   a user did or what happened to a user (project created,
   review left, follow event…). Consumed for both per-user
   activity views and aggregate feeds.
2. **Project news** — long-form posts published on a project
   page (`ProjectNewsPost`), with likes and threaded comments.
3. **Personalized feed** — assembles a single timeline for the
   current user from the above plus subscriptions.

The user follow graph (`UserFollow`) and project subscriptions
(`ProjectSubscription`) drive what shows up in (3); both
entities were created by other domains but are read here.

## Surface

### HTTP — Core API

[ActivitiesController.cs](DevHunt.CoreApi/Controllers/ActivitiesController.cs)
(`/api/activities`):

- `GET /` — paged activity records, scope/filter via query
  params (filter shape not enumerated; open the controller).

[FeedController.cs](DevHunt.CoreApi/Controllers/FeedController.cs)
(`/api/feed`):

- `GET /` — assembled feed for the current user.

[ProjectNewsController.cs](DevHunt.CoreApi/Controllers/ProjectNewsController.cs)
(`/api/projects/{projectId}/news`):

- News CRUD: `GET /`, `GET /{newsId}`, `POST /`,
  `PUT /{newsId}`, `DELETE /{newsId}`
- Likes: `POST /{newsId}/like`, `GET /{newsId}/like`
- Comments: `GET /{newsId}/comments`, `POST /{newsId}/comments`,
  `PUT /{newsId}/comments/{commentId}`,
  `DELETE /{newsId}/comments/{commentId}`

Cross-domain reads (covered elsewhere but used here):

- `GET /api/users/{userId}/activities` — per-user view
  (auth-and-identity surface).
- `POST/DELETE /api/projects/{projectId}/subscriptions` —
  toggle (projects-and-teams surface).
- `POST/DELETE /api/users/{userId}/follow` —
  (auth-and-identity).

### UI

No dedicated `/feed` or `/activity` route in
[frontend/src/app/[locale]/dashboard/](frontend/src/app/[locale]/dashboard/).
Components observed:

- [frontend/src/components/news/](frontend/src/components/news/) —
  news rendering.
- [frontend/src/components/profile/activity-card/](frontend/src/components/profile/activity-card/) —
  per-user activity display.

The aggregated feed is presumably surfaced from the home /
landing page
([frontend/src/app/[locale]/(public)/](frontend/src/app/[locale]/(public)/)
and `home-page-client.tsx`); the exact placement was not
verified in this pass.

### SignalR

None at this commit. Updates rely on poll / refresh.

## Entities involved

- `ActivityRecord` (table `Activity_Records` — note underscored
  name) — `ActorId` non-null, `ProjectId` **nullable** since
  migration `MakeActivityProjectOptional` (2025-11-24),
  `TargetUserId` nullable.
- `ProjectNewsPost` — FK to `Project` and `User` (Author);
  collections `Likes` and `Comments`.
- `NewsPostLike` — `NewsPostId` + `UserId`.
- `NewsPostComment` — `NewsPostId` + `AuthorId`.
- Read-only here, owned by other domains: `UserFollow`,
  `ProjectSubscription`.

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — controllers
  and services (`UserActivityService`, `ProjectNewsService`).
- [systems/infrastructure.md](../systems/infrastructure.md) —
  schema. Indexes added by migration
  `OptimizeFeedIndexes` (2025-11-25) targeting feed query
  performance.
- [systems/frontend.md](../systems/frontend.md) — news +
  activity components.

## Known traps

- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  whether activity/news creation publishes events for
  fan-out (feed precomputation, notification fan-out) is not
  fully traced; if it does, those publishes are no-ops without
  the bus. The feed itself is computed on-read from DB rows, so
  the user-visible feed remains functional even with the bus
  disabled.

## What I should NOT assume

- **`ActivityRecord.ProjectId` is nullable.** A non-trivial
  fraction of activity rows have no project context (e.g., user
  follows). Filters that JOIN through `Projects` will silently
  drop those rows — use a LEFT JOIN or pre-filter.
- **Migration `MakeActivityProjectOptional` also bundled
  unrelated `UpdateData` for two specific user IDs**
  (per [data/migration-timeline.md](../data/migration-timeline.md)).
  Don't paraphrase the migration as "schema-only" when reasoning
  about its content.
- **The feed is not real-time.** No SignalR push, no precomputed
  feed table at this commit. Each `GET /api/feed` recomputes
  from `Activity_Records` + subscriptions/follows. Performance
  depends on `OptimizeFeedIndexes`; queries that bypass those
  indexes scale poorly.
- **News likes and showcase likes are different entities.**
  `NewsPostLike` is on `ProjectNewsPost`; the
  `showcase` domain has its own like surface. Don't share UI
  state between them by accident.
- **News comments are flat at the entity level.** Unlike
  `ShowcaseComment`, `NewsPostComment` has no `ParentCommentId`
  self-FK at this commit. If threading is requested, it's a
  schema change.
- **The follow graph is a separate domain's data**, but feed
  queries read it directly. Don't add follow-related write logic
  here — that's auth-and-identity.
