---
title: Showcase — public portfolio entries per project, likes, comments, featuring
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/ShowcaseController.cs
  - DevHunt.CoreApi/Controllers/AdminContentController.cs
  - DevHunt.Infrastructure/ShowcaseProject.cs
  - DevHunt.Infrastructure/Models/ShowcaseComment.cs
  - frontend/src/app/[locale]/(public)/showcase/
  - frontend/src/app/[locale]/dashboard/showcase-preview/
  - frontend/src/components/showcase/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

A curated public portfolio entry derived from a private Project.
The owner crafts a `ShowcaseProject` (separate row from
`Project` itself), publishes screenshots and metrics, and admins
can "feature" the best entries onto a hero list. Visitors —
including unauthenticated — can browse, like/unlike, and
comment. Comments are threaded via self-FK on `ShowcaseComment`.

## Surface

### HTTP — `/api`

The route attribute on
[ShowcaseController.cs:23](DevHunt.CoreApi/Controllers/ShowcaseController.cs#L23)
is just `api/`, not `api/showcase` — the resource segment is
written into each method's template.

Owner / public reads:

- `GET /projects/{projectId}/showcase` — entry for one project
- `GET /showcase` — listing (paged + filtered)

Owner write path:

- `POST /projects/{projectId}/showcase` — create
- `PUT /projects/{projectId}/showcase` — update
- `DELETE /projects/{projectId}/showcase` — remove showcase
  (does **not** delete the underlying `Project`)

Engagement:

- `POST /projects/{projectId}/showcase/like` |
  `POST /projects/{projectId}/showcase/unlike`
- `GET /projects/{projectId}/showcase/comments`
- `POST /projects/{projectId}/showcase/comments`
- `PUT /projects/{projectId}/showcase/comments/{commentId}`
- `DELETE /projects/{projectId}/showcase/comments/{commentId}`

Admin curation:

- `POST /projects/{projectId}/showcase/feature`
- `POST /projects/{projectId}/showcase/unfeature`

Plus, parallel path on
[AdminContentController.cs](DevHunt.CoreApi/Controllers/AdminContentController.cs):

- `GET /api/admin/content/showcase`
- `DELETE /api/admin/content/showcase/{id}` — hard delete from
  the moderation surface (see `moderation` domain for context).

### UI routes

- Public list / detail —
  [frontend/src/app/[locale]/(public)/showcase/](frontend/src/app/[locale]/(public)/showcase/)
  with `[projectId]/` for the detail page.
- Owner preview before publishing —
  [frontend/src/app/[locale]/dashboard/showcase-preview/](frontend/src/app/[locale]/dashboard/showcase-preview/)
- Components —
  [frontend/src/components/showcase/](frontend/src/components/showcase/)

### SignalR

None.

## Entities involved

- `ShowcaseProject` — one per `Project` (FK to `Project`).
  **Two hand-rolled JSON columns** (`ScreenshotsJson` and
  `MetricsJson`) — see traps.
- `ShowcaseComment` — threaded comments via
  `ParentCommentId` self-FK; `→ Replies` collection.

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — REST.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  schema; created by migration
  `ShowcaseProjectsImplementation` and extended by
  `AddShowcaseCommentsProjectFilesDocs`.
- [systems/frontend.md](../systems/frontend.md) — public list,
  detail, preview, component library.

## Known traps

- [gotchas/hand-rolled-json-text-columns.md](../gotchas/hand-rolled-json-text-columns.md) —
  `ShowcaseProject.ScreenshotsJson` and `MetricsJson` are
  plain `text` with manual `System.Text.Json`. EF cannot filter
  inside them; LINQ predicates against array elements fall back
  to client-side evaluation.

## What I should NOT assume

- **`ShowcaseProject` is not a `Project`.** Two distinct rows in
  two distinct tables. `DELETE /projects/{id}/showcase` removes
  the showcase row only — the `Projects` row keeps existing.
  Conversely, deleting a `Project` cascades to its
  `ShowcaseProject` (per the `→ ShowcaseProjects` collection in
  Project.cs); but the user-visible flow for "remove from
  showcase" is `DELETE /showcase`, not project deletion.
- **`feature` / `unfeature` is admin-only.** The endpoints are
  on the same controller as user-facing ones, so eyeballing the
  routes is misleading; auth checks live inside the action
  bodies.
- **Likes are not a count column.** The like/unlike endpoints
  toggle a row in a likes collection (table not enumerated in
  this pass — open the controller for the exact entity if
  needed). Don't try to update a `LikeCount` field.
- **Comment moderation has two doors.** Users can edit/delete
  their own comments via this domain; admins can hard-delete via
  `moderation` (`/api/admin/content/comments/{id}`). The two
  paths do not coordinate state — a user-deleted comment still
  shows up in the admin queue's history view (not verified, but
  expected from the polymorphic `ModerationReport` design).
- **Visitor visibility depends on `Project.Visibility` AND
  `ShowcaseProject` existence.** A project may be public yet
  have no showcase entry. A showcase entry on a non-public
  project is allowed at the schema level — the controller is
  responsible for blocking that combination, and that logic was
  not read in this pass.
