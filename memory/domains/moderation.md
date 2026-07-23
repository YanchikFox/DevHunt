---
title: Moderation — user reports, decisions queue, profanity filter, admin curation
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/ModerationController.cs
  - DevHunt.CoreApi/Controllers/AdminContentController.cs
  - DevHunt.CoreApi/Services/Moderation/ModerationService.cs
  - DevHunt.CoreApi/Services/Moderation/ProfanityFilterService.cs
  - DevHunt.CoreApi/Filters/
  - DevHunt.CoreApi/Program.cs
  - DevHunt.Infrastructure/ModerationReport.cs
  - DevHunt.Infrastructure/DevHuntDbContext.cs
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Two ways content gets moderated on DevHunt: **reactive** (a user
files a report against any entity, an admin works the queue and
issues a decision), and **proactive** (a profanity filter runs at
write-time on user-submitted DTOs and either rejects or censors).
Both share `ModerationReport` for state. Adjacent to but separate
from this domain is the admin-content surface, which lets staff
edit/delete content without going through a report at all.

## Surface

### HTTP — user-facing reports + admin queue

[ModerationController.cs](DevHunt.CoreApi/Controllers/ModerationController.cs)
(`/api/moderation`):

- `POST /report` — user files a report against a target. Target
  is polymorphic (`TargetType` + `TargetId`).
- `GET /queue` — admin queue (paged, status-filtered).
- `POST /decision` — admin records a moderation decision (status
  transition + optional moderator note); writes
  `ProcessedByUserId`.

### HTTP — admin direct curation

[AdminContentController.cs](DevHunt.CoreApi/Controllers/AdminContentController.cs)
(`/api/admin/content`) — surgical edits without a report:

- `GET /stats`
- Projects: `GET /projects`, `GET /projects/{id}`,
  `PUT /projects/{id}`, `DELETE /projects/{id}`
- News: `GET /news`, `DELETE /news/{id}`
- Comments: `GET /comments`, `DELETE /comments/{id}` (covers
  `ShowcaseComment`, `NewsPostComment`, `FeedbackComment` —
  exact set requires reading the controller, not done in this
  pass)
- Showcase: `GET /showcase`, `DELETE /showcase/{id}`

### Profanity filtering (in-process, write-path)

Wired in
[Program.cs:124-127](DevHunt.CoreApi/Program.cs#L124-L127):

- `IProfanityFilterService` (singleton) →
  `ProfanityFilterService` — exposes
  `CheckField`, `CheckDto`, `CensorText`, `CensorDto`.
- `ProfanityFilter` (scoped) — MVC action filter applied to
  controllers that need rejection on profane input.
- `ProfanityCensorFilter` (scoped) — MVC action filter that
  applies censorship instead of rejection.

The word list is an embedded resource:
[DevHunt.CoreApi/Filters/profanity_words.json](DevHunt.CoreApi/Filters/profanity_words.json)
(embedded via `<EmbeddedResource>` in
[DevHunt.CoreApi.csproj](DevHunt.CoreApi/DevHunt.CoreApi.csproj)).
Hot-updating the list requires a redeploy.

### UI routes

There is no top-level `/moderation` route in
[frontend/src/app/[locale]/](frontend/src/app/[locale]/). The
queue and admin content tooling live under
[frontend/src/app/[locale]/(protected)/admin/](frontend/src/app/[locale]/(protected)/admin/) —
the protected route group enforces the `admin` prefix gate from
[middleware.ts:14](frontend/src/middleware.ts#L14).

### SignalR

No moderation-specific hub. Decisions can publish events that
trigger notifications (covered by `notifications` domain), but
the moderation workflow itself is request/response.

## Entities involved

- `ModerationReport`, located at
  [DevHunt.Infrastructure/ModerationReport.cs:6](DevHunt.Infrastructure/ModerationReport.cs#L6).
  Polymorphic FK pair (`TargetType` + `TargetId`); supporting
  composite index `(TargetType, TargetId, Status)` set in
  [DevHuntDbContext.cs:132-134](DevHunt.Infrastructure/DevHuntDbContext.cs#L132-L134).
  Reporter is a real FK; `ProcessedByUserId` is nullable
  (queue rows haven't been actioned yet).

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — controllers,
  services, MVC filters.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  schema, composite index.
- [systems/frontend.md](../systems/frontend.md) — admin UI
  under `(protected)/admin`.

This domain does **not** cross the integration-gateway or
ml-service.

## Known traps

- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  decision events that should trigger notifications to the
  reporter / target are no-op'd when the bus is disabled.
- **Polymorphic target has no DB-level integrity.**
  `TargetType` and `TargetId` are raw columns — there is no
  cascade delete from a deleted Project or User to the reports
  about it. Stale reports outlive their targets.

## What I should NOT assume

- **The admin-content surface and the moderation queue are
  separate workflows.** A staff member deleting a project via
  `DELETE /api/admin/content/projects/{id}` does **not**
  resolve open `ModerationReport` rows about that project. They
  must be processed via `POST /api/moderation/decision`.
- **Profanity filters operate on whole DTOs.** `CheckDto` and
  `CensorDto` walk the object via reflection; they don't act on
  individual property names you might assume. If a new field is
  added to a DTO that should be filtered, no extra wiring is
  needed — but if a field should be exempt, it must be marked
  appropriately (attribute mechanism not read in this pass).
- **The word list is shipped, not learned.** No ML, no
  embeddings, no on-the-fly tuning at this commit. Adding a
  word means a code change.
- **`Status` values on `ModerationReport`** were not enumerated
  in this pass. Open the entity file before hardcoding a
  literal in new queue logic.
- **Reports are not anonymous.** `ReporterId` is non-null and
  required (per the FK grep at Step 3). Don't expose this id to
  the reportee — that's a privacy boundary the API code must
  enforce; the schema does not.
