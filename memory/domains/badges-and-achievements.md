---
title: Badges & Achievements — definitions, in-process triggers, user awards
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/BadgesController.cs
  - DevHunt.CoreApi/Services/Badges/AchievementTriggerService.cs
  - DevHunt.CoreApi/Services/Badges/IAchievementTriggerService.cs
  - DevHunt.CoreApi/Services/Badges/BadgesService.cs
  - DevHunt.CoreApi/Services/Badges/AchievementTrigger.cs
  - DevHunt.Infrastructure/Achievement.cs
  - DevHunt.Infrastructure/UserAchievement.cs
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Admin defines achievements with a code, an icon, and award rules.
The platform fires triggers from action endpoints (a review is
posted, a task is completed, a user follows another, …); the
trigger service evaluates rules and may grant a `UserAchievement`
row to the affected user. Users see their own badge collection
on the profile.

## Surface

### HTTP — `/api/badges`

[BadgesController.cs](DevHunt.CoreApi/Controllers/BadgesController.cs):

- Public reads: `GET /`, `GET /{id}`, `GET /me`,
  `GET /user/{userId}`, `GET /stats/{userId}`
- Manual award (admin): `POST /award/{userId}/{achievementCode}`
- Progress patch (admin): `PUT /progress/{userId}/{achievementCode}`
- Definition CRUD (admin): `POST /`, `PUT /{id}`, `DELETE /{id}`

### In-process trigger surface

`AchievementTrigger` enum values feed
`IAchievementTriggerService.TriggerAchievementCheckAsync` (called
via an `IServiceProvider` extension method). Concrete trigger
sites observed at this commit:

- **Reviews** —
  [ReviewsController.cs:186, 189](DevHunt.CoreApi/Controllers/ReviewsController.cs#L186) →
  `ReviewCreated` for both reviewer and reviewed user.
- **Projects** —
  [ProjectsController.cs:448](DevHunt.CoreApi/Controllers/ProjectsController.cs#L448) →
  `ProjectCreated` for the owner.
- **Tasks** —
  [TasksController.cs:797](DevHunt.CoreApi/Controllers/TasksController.cs#L797) →
  `TaskCompleted` for the assigned-to user.
- **Follows** —
  [UsersController.cs:223-224](DevHunt.CoreApi/Controllers/UsersController.cs#L223-L224) →
  `UserFollowed` fired for both follower and followed.
- **Support** —
  [AdminSupportController.cs:258](DevHunt.CoreApi/Controllers/AdminSupportController.cs#L258) →
  `TicketResolved` for the resolving admin.
- **Admin actions** —
  [AdminController.cs:159, 203, 246](DevHunt.CoreApi/Controllers/AdminController.cs#L159) →
  `UserBlocked`, `AdminVerifiedUser`, `RoleChanged`.

If an action does not appear in this list, **no trigger fires
when it happens**. Adding a new trigger requires adding a call
site, not just an enum value.

### UI

- Profile-level badge view —
  [frontend/src/components/profile/UserBadges.tsx](frontend/src/components/profile/UserBadges.tsx)
  (rendered under `dashboard/profile/`).
- Admin definition tooling —
  [frontend/src/components/admin/BadgeManagement.tsx](frontend/src/components/admin/BadgeManagement.tsx)
  and `BadgeFormDialog.tsx` (under
  `(protected)/admin/`).

### SignalR

None. Awarding a badge does not push a real-time event in this
domain. If a notification is desired, it must go through the
`notifications` domain (event → DB row → `/notificationHub`
push). Whether that wiring exists for badge awards was not
verified in this pass.

## Entities involved

- `Achievement` — definitions (admin-managed); collection
  back-ref `UserAchievements`.
- `UserAchievement` — junction `User ↔ Achievement` with
  award/progress fields. Mapping in
  [Configuration/EntityConfigurations/AchievementConfiguration.cs](DevHunt.Infrastructure/Configuration/EntityConfigurations/AchievementConfiguration.cs).

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — controllers,
  services, trigger fan-out.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  the two entities and the unique-index migration
  (`AddBadgesUniqueIndexesAndModerationTracking`).
- [systems/frontend.md](../systems/frontend.md) — badge UI in
  profile + admin sections.

## Known traps

None new in this domain. The general
[gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md)
applies *if* a path adds bus-published events; today triggers
are synchronous in-process, so the bus is not on the critical
path.

## What I should NOT assume

- **Triggers are synchronous and in-process.** They are not
  RabbitMQ events. A failure inside `AchievementTriggerService`
  does **not** roll back the originating action — a review still
  posts even if its trigger throws. Conversely, awards land in
  the same DB transaction scope as the controller's request, so
  they survive only if the controller succeeds.
- **There is no central registry of trigger call sites.** If a
  new code path should award a badge, you must add the call
  manually. Forgetting to wire a trigger is the most likely way
  this domain regresses.
- **`POST /award/...` is a privileged manual override** — it
  bypasses rule evaluation and grants the achievement directly.
  Don't expose it to non-admin users.
- **Progress is admin-patched, not auto-incremented per
  trigger** at this commit. The `PUT /progress/...` endpoint is
  separate; the trigger flow either grants-or-not, it does not
  bump partial counters. Verify in
  `AchievementTriggerService.cs` if you need exact semantics.
- **`UserAchievement` is the source of truth for awarded
  state.** Don't filter by `Achievement.IsAwarded` or similar —
  awards are rows in the junction table.
