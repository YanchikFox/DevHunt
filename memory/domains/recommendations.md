---
title: Recommendations — ML-driven user/project matching
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/RecommendationsController.cs
  - DevHunt.CoreApi/Services/MLServiceClient.cs
  - DevHunt.Infrastructure/Recommendation.cs
  - ml-service/routers/recommendations.py
  - ml-service/main.py
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Per-user list of recommended projects (or per-project list of
recommended people, in the inverse view). Computed by
ml-service using a scoring algorithm that combines skill match,
difficulty, project rating, team size, and "featured" status —
seen at the top of
[ml-service/routers/recommendations.py](ml-service/routers/recommendations.py).
Persisted in the Core API DB as `Recommendation` rows; users
can mark them viewed, actioned (= the user clicked through), or
dismissed. Refresh is on demand.

## Surface

### HTTP — Core API (`/api/recommendations`)

[RecommendationsController.cs](DevHunt.CoreApi/Controllers/RecommendationsController.cs):

- `GET /me` — current user's recommendations, paged
- `GET /project/{projectId}` — inverse view: people recommended
  for a project
- `PUT /{recommendationId}/view` — mark as viewed
- `PUT /{recommendationId}/action` — mark as acted-on
- `DELETE /{recommendationId}` — dismiss
- `POST /refresh-all` — privileged refresh of every user
- `POST /refresh/{userId}` — refresh one user

### HTTP — ml-service (`/api/recommendations/*`)

The compute side — called by Core API via
[MLServiceClient.cs](DevHunt.CoreApi/Services/MLServiceClient.cs):

- `GET /api/recommendations/user/{userId}?limit={n}` —
  generate recommendations for a user (line 123).
- `POST /api/recommendations/refresh` — bulk refresh trigger
  (line 150).

Inside ml-service, the router exposes additional endpoints
defined in `routers/recommendations.py`; their full surface was
not enumerated in this pass.

### UI

Not enumerated as a separate route — recommendations are likely
surfaced inside the activity-feed home page or a dashboard
widget. UI placement was not verified.

### SignalR

None. Recommendations refresh is HTTP-only.

## Entities involved

- `Recommendation` — FK to `Project` and `User` (target);
  status fields (`Viewed`, `Actioned`, `Dismissed` — exact set
  not read, see entity file). Stores rationale in
  **plain-text `ReasoningJson`** (hand-rolled JSON, not jsonb).

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — controller +
  `MLServiceClient`.
- [systems/ml-service.md](../systems/ml-service.md) — compute,
  scoring algorithm, queries against the same PostgreSQL
  database via `asyncpg` (separate connection from EF Core).
- [systems/infrastructure.md](../systems/infrastructure.md) —
  the `Recommendation` table.
- [systems/frontend.md](../systems/frontend.md) — UI consumer
  (placement unverified).

## Known traps

- [gotchas/hand-rolled-json-text-columns.md](../gotchas/hand-rolled-json-text-columns.md) —
  `Recommendation.ReasoningJson` is plain `text` + manual
  `System.Text.Json`. Filtering by anything inside the
  reasoning structure is not server-side queryable via EF.
- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  ml-service consumes the `devhunt.ml.recommendations` queue
  on the bus (per
  [systems/ml-service.md](../systems/ml-service.md)). Without
  RabbitMQ, event-driven refresh paths silently drop. The
  HTTP-driven `POST /refresh*` endpoints continue to work.

## What I should NOT assume

- **Two services share one PostgreSQL database.** ml-service
  reads project / user / skill data via `asyncpg`, not the EF
  Core schema. Schema changes that rename columns or alter
  types **must** be coordinated with ml-service queries —
  there is no shared model class enforcing the contract. The
  ml-service queries live in
  [ml-service/routers/recommendations.py](ml-service/routers/recommendations.py)
  and (per its docstring) embed SQL.
- **Scoring weights are code, not config.** The functions
  `calc_skill_match_factor`, `calc_difficulty_factor`,
  `calc_rating_factor`, `calc_team_size_factor`,
  `calc_featured_factor` in
  `ml-service/routers/recommendations.py` (visible at line ~30+)
  define the algorithm. Tuning means a code change + redeploy
  of ml-service.
- **Recommendations are not real-time.** Refresh is explicit
  via `POST /refresh-all` or `POST /refresh/{userId}` on Core
  API, or a queue consumer trigger inside ml-service. A new
  user does not see recommendations until a refresh runs for
  them.
- **`POST /refresh-all` is admin/privileged.** It iterates over
  every user — don't expose to any other role.
- **The compute path is independent from the AI-planning
  domain.** Both touch `ml-service`'s `/api/ai/*` and
  `/api/recommendations/*` respectively, but they're different
  features with different routers. Don't conflate.
- **`Recommendation` rows are user-scoped persistent state**,
  not per-request results. Refreshing replaces existing rows
  rather than appending — the exact merge semantics
  (delete-and-recreate vs upsert) were not read in this pass.
