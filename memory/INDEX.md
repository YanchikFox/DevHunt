# Memory Index

This file is loaded first every session. It is an index, not content.
For each entry, one line: relative path + a short hook. Anything longer
belongs in the entry itself. Read the rules in repo-root `CLAUDE.md`
before adding to this index.

## Confirmed deployables (Step 1, commit `cc43ab9`)

Confirmed by cross-referencing `DevHunt.slnx`, `Directory.Packages.props`,
and root `docker-compose.yml`. Each item below is a top-level component
that will get its own `systems/*.md` in Step 2.

Built from this repo (have `Dockerfile` referenced by compose):

- `auth-service` → builds from `DevHunt.AuthService/Dockerfile` (in solution)
- `core-api` → builds from `DevHunt.CoreApi/Dockerfile` (in solution)
- `db-migrator` → builds from `DevHunt.DatabaseMigrator/Dockerfile` (NOT in solution; runs once and exits)
- `code-analyzer` → builds from `DevHunt.Analyzer/Dockerfile` (NOT in solution; Python by directory layout)
- `frontend` → builds from `frontend/Dockerfile` (Next.js)
- `ml-service` → builds from `ml-service/Dockerfile` (Python)
- `notification-service` → builds from `notification-service/Dockerfile` (Node.js)
- `integration-gateway` → builds from `integration-gateway/Dockerfile` (Node.js)
- `documentation` → builds from `documentation/Dockerfile` via `docker-compose.docs.yml` (Docusaurus portal)
- `api-gateway` → builds from `nginx/Dockerfile` (Nginx config baked into image)
- `backup-service` → uses `postgres:16-alpine` image with a custom command (no Dockerfile in repo)
- `openobserve-init` → uses `alpine:3.19` with an init script from `monitoring/openobserve/`

Library, in solution, NOT a standalone deployable:

- `DevHunt.Infrastructure` — referenced by `auth-service`, `core-api`, `db-migrator` (will still get `systems/infrastructure.md` because it's the data-layer entry-point)

External infra (image-only, no code in this repo, but the platform
depends on them — not getting `systems/*.md`, will be referenced from
`cross-cutting/` entries):

- `db` → `pgvector/pgvector:pg16` (PostgreSQL with pgvector extension)
- `cache-service` → `redis:7-alpine`
- `message-broker` → `rabbitmq:3-management-alpine`
- `object-storage` → `chrislusf/seaweedfs:latest` (S3-compatible)
- `openobserve` → `public.ecr.aws/zinclabs/openobserve:latest` (logs/metrics/traces)

Top-level project directories present in the repo but NOT wired into
`docker-compose.yml` and NOT in `DevHunt.slnx` — purpose unclear from
the three Step-1 sources alone:

- `DevHunt.DatabaseSeeder/`
- `CtFixer/`

See `unverified/orphaned-projects.md`.

## Entries

### systems/
- [auth-service.md](systems/auth-service.md) — JWT/OAuth/TOTP API; in-solution; net10.0; CSRF SameSite=Strict
- [core-api.md](systems/core-api.md) — main API (~50 controllers); SignalR + Outbox + RW/RO Postgres; net10.0
- [infrastructure.md](systems/infrastructure.md) — shared EF Core library; owns DbContext + 45 entities + migrations
- [database-migrator.md](systems/database-migrator.md) — one-shot job; 10× retries with exp backoff; gates Auth/Core boot
- [code-analyzer.md](systems/code-analyzer.md) — Python stdlib HTTP server (NOT FastAPI); tree-sitter + Semgrep
- [frontend.md](systems/frontend.md) — Next.js 16 App Router; rewrites browser → core-api/auth-service via `/api/proxy-*`
- [documentation.md](systems/documentation.md) — Docker-first Docusaurus portal; fail-fast Core/Auth/ML OpenAPI, TypeDoc, DocFX, diagrams, Swagger UI
- [ml-service.md](systems/ml-service.md) — FastAPI; Groq/Gemini; asyncpg; embeddings router optional
- [notification-service.md](systems/notification-service.md) — Express 5; email/SMS/push + OpenObserve alert webhook
- [integration-gateway.md](systems/integration-gateway.md) — Express 5; GitHub/GitLab OAuth + webhooks; production hard secret check
- [api-gateway.md](systems/api-gateway.md) — Nginx TLS termination; H2C-smuggling-safe WebSocket; CSP commented out

(`backup-service` runs an inline `pg_dump` loop as the compose `command:` — no Dockerfile, no application code. `openobserve-init` is alpine + `monitoring/openobserve/init-alerts.sh` that configures OpenObserve alerts on first boot and exits. Neither warrants a `systems/` or `cross-cutting/` entry; both are referenced contextually from `cross-cutting/observability.md`.)

### domains/
- [auth-and-identity.md](domains/auth-and-identity.md) — registration/login/OAuth/2FA/profile/follows/BYOK; AuthService owns credentials, Core API owns the rest
- [projects-and-teams.md](domains/projects-and-teams.md) — project CRUD + lifecycle + team + invitations/applications + boost + slug
- [tasks-kanban.md](domains/tasks-kanban.md) — kanban board, columns, links, attachments, soft-delete restore, GitHub Issues bi-sync via gateway
- [ai-planning.md](domains/ai-planning.md) — chat assist + plan generation + tool-calling; multi-provider BYOK LLM; runs in core-api, NOT ml-service
- [chat-and-channels.md](domains/chat-and-channels.md) — DMs/group/project channels share `Conversations` table; `/chatHub` real-time; profanity filter at write
- [notifications.md](domains/notifications.md) — two parallel paths (in-app inbox via `/notificationHub` vs out-of-band email/SMS via notification-service)
- [moderation.md](domains/moderation.md) — user reports + admin decisions queue + write-path profanity filter; admin curation is a separate workflow
- [badges-and-achievements.md](domains/badges-and-achievements.md) — admin-defined achievements + in-process triggers from ~8 controller call sites; NOT bus-driven
- [showcase.md](domains/showcase.md) — public portfolio entries on top of `Project`; like/comment/feature; has hand-rolled JSON columns
- [code-analysis.md](domains/code-analysis.md) — analyzer service POSTs results back; per-project query, dismissals, pgvector semantic search
- [integrations-oauth.md](domains/integrations-oauth.md) — GitHub/GitLab linking; OAuth split between gateway and Core API; tokens encrypted at rest
- [file-storage.md](domains/file-storage.md) — project files, documents, task attachments, avatars; all blobs via SeaweedFS S3
- [activity-feed.md](domains/activity-feed.md) — `ActivityRecord` timeline + `ProjectNewsPost` + on-read feed; no SignalR push, no precompute
- [recommendations.md](domains/recommendations.md) — ml-service scoring algorithm + Core API persistence; two services share one Postgres via asyncpg

### cross-cutting/
- [github-actions.md](cross-cutting/github-actions.md) — 16 workflows: авто-PR (semgrep/trivy/arch/pr-checks/hadolint/iac), вручную (ci/sonar/depscan/mutation/perf/contract/visual), CD (cd-production tag-based, cd-staging manual)
- [eventing-and-outbox.md](cross-cutting/eventing-and-outbox.md) — transactional outbox → RabbitMQ topic exchange; NoOp when bus disabled; Processing status never written
- [persistence-and-ef-core.md](cross-cutting/persistence-and-ef-core.md) — single DevHuntDbContext (DefaultConnection); EnableDynamicJson wired only here; CreateReadContext() never called
- [middleware-pipeline.md](cross-cutting/middleware-pipeline.md) — 14-step ordered pipeline; LogEnrichment before Auth (UserId always anonymous); UserActiveCheck = per-request DB read
- [rate-limiting-and-csrf.md](cross-cutting/rate-limiting-and-csrf.md) — AspNetCoreRateLimit IP keying; two CSRF cookie names (CSRF-TOKEN vs XSRF-REQUEST-TOKEN); dev allows without token
- [realtime-signalr.md](cross-cutting/realtime-signalr.md) — ChatHub + NotificationHub; Redis backplane optional; presence TTL 2 min sliding; no active-check on hub connect
- [scale-out-readiness.md](cross-cutting/scale-out-readiness.md) — 6 silent scale-out failure modes; all work single-instance; Redis fixes 3 of 6; 3 require code changes; 3 candidate singletons audited safe
- [observability.md](cross-cutting/observability.md) — Serilog+OpenTelemetry+Prometheus all export to OpenObserve; LogEnrichment UserId always anonymous; metrics path normalization incomplete
- [caching.md](cross-cutting/caching.md) — ICacheService/IDistributedCache (Redis or in-memory); IMemoryCache is separate per-process; RemoveByPattern no-ops without Redis
- [config-and-secrets.md](cross-cutting/config-and-secrets.md) — EnvLoader → env vars → appsettings → Azure KeyVault (Production only); fail-safe on KeyVault; __ separator convention
- [feature-flags.md](cross-cutting/feature-flags.md) — DB-backed FeatureFlag; IMemoryCache 30s TTL; unknown flag = enabled by default; Invalidate local-only

### data/
- [entity-catalog.md](data/entity-catalog.md) — every DbSet → file:line + 1–2 key relations; entity classes split across 3 locations
- [migration-timeline.md](data/migration-timeline.md) — table of ~50 EF migrations, oldest first; flags name-vs-content mismatches

### decisions/
*(empty — written when a real decision is made)*

### gotchas/
- [events-silently-dropped-without-rabbitmq.md](gotchas/events-silently-dropped-without-rabbitmq.md) — `IEventBusService` falls through to NoOp when RabbitMQ unconfigured
- [integration-gateway-bare-axios-references.md](gotchas/integration-gateway-bare-axios-references.md) — `unverified` — bare `axios` at lines 274 & 460; only `axiosClient` is imported
- [notification-bulk-userid-localhost-placeholder.md](gotchas/notification-bulk-userid-localhost-placeholder.md) — `/api/notifications/bulk` ships a `${userId}@devhunt.local` stub; intentional, do NOT auto-fix
- [hand-rolled-json-text-columns.md](gotchas/hand-rolled-json-text-columns.md) — 5 entity props store JSON in plain `text` via `System.Text.Json`; not jsonb, not query-shaped
- [ai-plan-cancel-process-local.md](gotchas/ai-plan-cancel-process-local.md) — `IAiInFlightRegistry` is in-process singleton; cancel silently no-ops under scale-out
- [encryption-key-rotation-cross-domain.md](gotchas/encryption-key-rotation-cross-domain.md) — one `Encryption:Key`/`IV` pair touches 5 domains (integrations / BYOK / chat / admin); rotation breaks all at once
- [outbox-double-publish-on-scale-out.md](gotchas/outbox-double-publish-on-scale-out.md) — `OutboxEventStatus.Processing` never written; no row-lock; two worker replicas can double-publish same event
- [readonly-connection-registered-but-unused.md](gotchas/readonly-connection-registered-but-unused.md) — `CreateReadContext()` never called; `ReadOnlyConnection` dead; all reads hit primary
- [maintenance-mode-cache-not-distributed.md](gotchas/maintenance-mode-cache-not-distributed.md) — IMemoryCache per-process; toggle lag up to 30s per replica under scale-out
- [signalr-fanout-fails-without-redis-backplane.md](gotchas/signalr-fanout-fails-without-redis-backplane.md) — without Redis backplane cross-replica message delivery silently drops
- [rate-limit-counters-per-instance-without-redis.md](gotchas/rate-limit-counters-per-instance-without-redis.md) — in-memory counters per replica; effective limit = N × config without Redis
- [feature-flag-cache-not-distributed.md](gotchas/feature-flag-cache-not-distributed.md) — flag toggle via SuperAdmin clears only local replica cache; 30s lag on others
- [ml-service-import-blocks-on-db-env-var.md](gotchas/ml-service-import-blocks-on-db-env-var.md) — deps.py raises ValueError at module import if DATABASE_URL absent; gen-mlapi.js must pass dummy value

### playbooks/
- [finalization-checks.md](playbooks/finalization-checks.md) — 5-step script suite: broken links, frontmatter, backlinks, INDEX completeness, stale-suspected detection
- [coverage-report.md](playbooks/coverage-report.md) — monthly: broken source refs, uncovered files by dir, hot files (>5 mentions), coverage %

### unverified/
- [orphaned-projects.md](unverified/orphaned-projects.md) — `DevHunt.DatabaseSeeder` and `CtFixer` exist on disk but are absent from solution and compose; need user to confirm purpose
