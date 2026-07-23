---
title: Code Analysis — static analysis runs, results storage, semantic search
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/CodeAnalysisController.cs
  - DevHunt.Infrastructure/CodeAnalysisResult.cs
  - DevHunt.Infrastructure/Models/CodeAnalysisEmbedding.cs
  - DevHunt.Infrastructure/DevHuntDbContext.cs
  - DevHunt.Analyzer/devhunt_analyzer/api.py
  - integration-gateway/src/services/webhookHandler.js
  - frontend/src/components/features/CodeQualityInsights.tsx
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Static analysis on a project's git repository. The Python
`code-analyzer` deployable does the actual scanning (tree-sitter
+ Semgrep + custom rules); Core API stores results, exposes
them per project, and supports semantic search over issues via
pgvector embeddings. Triggered post-sync from
`integration-gateway` after a successful repository sync; can
also be invoked on demand.

## Surface

### HTTP — Core API (TWO controllers, same file)

Both classes live in
[CodeAnalysisController.cs](DevHunt.CoreApi/Controllers/CodeAnalysisController.cs):

**Internal — `/api/internal/code-analysis`** (line 19). Auth via
`IInternalServiceAuthenticator`, not user JWT:

- `POST /results` — code-analyzer (or the gateway acting as
  proxy) posts an analysis run's JSON output here for storage.
  Single endpoint; this is the inbound surface from outside.

**User-facing — `/api/projects/{projectId}/code-analysis`**
(line 230):

- `GET /latest` — most recent result for the project
- `GET /history` — paged
- `GET /results/{resultId}` — full payload of one run
- `GET /summary` — aggregated metrics
- `GET /issues` — flat list, supports filtering / pagination
- `GET /semantic-search` — pgvector kNN over `CodeAnalysisEmbeddings`
- `POST /generate-embeddings` — backfill / refresh embeddings
- `GET /config` — exclude-patterns and similar
- `PATCH /config/exclude-patterns`
- `POST /dismiss` | `POST /undismiss` — per-issue dismissal

### UI

The user surface lives inside the project workspace, not at a
top-level path. Component:
[frontend/src/components/features/CodeQualityInsights.tsx](frontend/src/components/features/CodeQualityInsights.tsx),
rendered from
[frontend/src/app/[locale]/dashboard/projects/[id]/_components/](frontend/src/app/[locale]/dashboard/projects/[id]/_components/).

### SignalR

None. Long-running analyses don't stream progress through a
hub; the UI polls `/latest` / `/history`.

## Entities involved

- `CodeAnalysisResult` — one row per analysis run; FKs to
  `Project` and (optionally) `Integration`. Composite indexes
  on `(ProjectId, CreatedAt DESC)` and
  `(IntegrationId, Branch)` set in
  [DevHuntDbContext.cs:136-143](DevHunt.Infrastructure/DevHuntDbContext.cs#L136-L143).
- `CodeAnalysisEmbedding` — vectorized issue embeddings; FK to
  `CodeAnalysisResult` and `Project`. Powered by **pgvector**,
  available because the `db` image is `pgvector/pgvector:pg16`
  (see `docker-compose.yml`).

Migrations: `AddCodeAnalysisResults` (2026-03-05),
`AddCodeAnalysisEmbeddings` (2026-03-10).

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/code-analyzer.md](../systems/code-analyzer.md) — the
  Python service that runs `tree-sitter` + Semgrep, exposes
  `POST /analyze`. Authenticated by `ANALYZER_API_SECRET`.
- [systems/integration-gateway.md](../systems/integration-gateway.md) —
  triggers analyzer fire-and-forget after a successful
  repository sync (see
  [integration-gateway/src/services/webhookHandler.js](integration-gateway/src/services/webhookHandler.js#L)
  `triggerCodeAnalysis`). Errors swallowed.
- [systems/core-api.md](../systems/core-api.md) — storage,
  user-facing API, embedding generation orchestration.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  schema and pgvector indexes.
- [systems/frontend.md](../systems/frontend.md) — Code Quality
  Insights component.

## Known traps

- [gotchas/integration-gateway-bare-axios-references.md](../gotchas/integration-gateway-bare-axios-references.md) —
  if the webhook create/delete paths in the gateway throw at
  runtime, the post-sync analysis trigger may never wire up
  for newly registered repos.
- **Trigger failures are silent** — the gateway calls
  `triggerCodeAnalysis(...).catch(err => logger.error(...))`
  ([integration-gateway/src/index.js:199](integration-gateway/src/index.js#L199)).
  A "sync succeeded" response says nothing about whether
  analysis actually started.
- **pgvector is image-dependent.** If the database image is
  swapped to plain `postgres:16`, semantic search and embedding
  generation will fail with `extension "vector" does not exist`.

## What I should NOT assume

- **The internal controller is unauthenticated for users —
  authenticated for services.** Users cannot reach
  `/api/internal/code-analysis/results`; nginx routes it through
  `/api/`, but the action checks
  `IInternalServiceAuthenticator` (singleton from
  [Program.cs:130](DevHunt.CoreApi/Program.cs#L130)). Don't
  attempt to call it from a browser.
- **Embeddings are not generated automatically.** The
  `POST /generate-embeddings` endpoint must be invoked
  explicitly. A `CodeAnalysisResult` can exist without
  corresponding `CodeAnalysisEmbedding` rows. Semantic search
  silently returns empty for unindexed runs.
- **Dismissals are per issue, not per result.** Calling
  `POST /dismiss` removes one finding from the user's view; it
  does not invalidate the whole run. The dismiss persistence
  layer (column on what entity?) was not read in this pass.
- **`Branch` indexing implies branch-scoped runs.** The
  composite index on `(IntegrationId, Branch)` suggests the
  same integration can have results for multiple branches.
  Querying "the latest" without specifying a branch may collide
  or pick by recency only — verify before designing UI around
  it.
- **The analyzer runs in its own container with no DB access.**
  It does NOT write directly to PostgreSQL — all results land
  via `POST /api/internal/code-analysis/results`. Don't expect
  partial progress rows to appear before the run completes.
