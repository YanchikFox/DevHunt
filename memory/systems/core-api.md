---
title: DevHunt.CoreApi — main application API
type: system
status: verified
sources:
  - DevHunt.CoreApi/Program.cs
  - DevHunt.CoreApi/DevHunt.CoreApi.csproj
  - DevHunt.CoreApi/Dockerfile
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

ASP.NET Core (`Microsoft.NET.Sdk.Web`, **net10.0**) Web API holding
nearly all business logic: projects, tasks, chat, AI planning,
showcase, badges, recommendations, moderation, support, admin, etc.
Shares `DevHuntDbContext` with Auth Service via
`DevHunt.Infrastructure`.

Compose service `core-api`, container port `INTERNAL_PORT_CORE_API`
(default 8080), host port `PORT_CORE_API` (default 7002).

## Entry-point

[DevHunt.CoreApi/Program.cs:51](DevHunt.CoreApi/Program.cs#L51) —
`var builder = WebApplication.CreateBuilder(args);`

`EnvLoader.Load()` is called first (line 50) — same helper as Auth
Service.

## Boot wiring (only what isn't obvious)

Many subsystems are hidden behind extension methods in
`DevHunt.CoreApi/Extensions/` (e.g., `AddSecretsConfiguration`,
`AddOpenTelemetryTracing`, `AddDistributedRateLimiting`,
`AddDatabaseContext`, `AddJwtAuthentication`,
`AddAuthorizationPolicies`, `AddSignalRWithRedis`,
`AddRedisCache`, `AddResilientHttpClients`,
`AddDefaultLlmProviders`). These wrap the equivalent of what Auth
Service does inline.

- **SignalR** (lines 114–121): `AddSignalR` with
  `MaximumReceiveMessageSize = 8192` and `EnableDetailedErrors`
  only in Development. Backed by Redis via `AddSignalRWithRedis`.
- **AI / LLM stack** (lines 144–186): a long chain of registrations
  for AI planning (`AiPlanningService`, `AiPlanValidator`,
  `AiPlanApplier`, `AiStrategyRouter`, `V1AiPlannerStrategy`,
  `AiPlanDraftService`), BYOK / multi-provider LLM
  (`LlmProviderRegistry`, `UserApiKeyService`,
  `LlmModelRegistryService`, `LlmModelSyncService`,
  `LlmChatService`, `LlmModelSeeder`), and AI tool dispatch
  (`AiToolDispatcher`, `AiToolExecutionService`, plus 11 concrete
  `IAiTool` implementations: `CreateTaskTool`, `UpdateTaskTool`,
  `MoveTaskTool`, `DeleteTaskTool`, three "Multiple"-variant
  task tools, `UpdateProjectTool`, `RecordDecisionTool`,
  `ReadDocumentTool`).
- **Domain services** are registered in clusters: User
  (`UserProfileService`, `UserFollowService`,
  `UserActivityService`, `UserSearchService`, plus
  `UserServicesFacade`); Project (`TechStackMatcher`,
  `ProjectFilterService`, `ProjectPermissionService`,
  `ProjectServicesFacade`, `ProjectTeamService`,
  `ProjectNewsService`, `ProjectIssuesService`); Task
  (`TaskAuthorizationService`); Integration
  (`IntegrationAuthorizationService`, `OAuthCallbackHandler`);
  Profile (`ProfileServicesFacade`); Chat (`ChatService`,
  `ProjectChannelService`, `ChannelMemberService`); Badges
  (`BadgesService`, `AchievementTriggerService`).
- **Outbox / EventBus** (lines 222–237): the `IEventBusService`
  registration is **conditional**. When
  `Features:EventBus:Enabled` is true (default true) **and**
  `RabbitMQ:ConnectionString` is non-empty, a real
  `RabbitMQEventBusService` is registered as a singleton, an
  `OutboxEventBusDecorator` is registered as `IEventBusService`
  (so business code writes to the outbox), and an
  `OutboxEventProcessorWorker` `IHostedService` drains the outbox
  to the bus. Otherwise `NoOpEventBusService` is registered and
  no events are published.
- **Hosted services**: `AiMessageDetailsPruneWorker` (always,
  line 163), `UnverifiedAccountCleanupWorker` (always, line 239),
  `OutboxEventProcessorWorker` (only when EventBus enabled).
- **Antiforgery** (lines 247–260): cookie `CSRF-TOKEN`
  (`SameSite=Lax` — note: weaker than Auth Service's `Strict`),
  header `X-CSRF-TOKEN`. The comment block at lines 247–251 says
  user-identity binding is intentionally suppressed to avoid token
  invalidation on login/logout; security relies on the
  double-submit cookie pattern + JWT auth.
- **Form size limits** (lines 263–274): 10 MB multipart, 4 MB per
  field, 1000 fields max. `IISServerOptions.MaxRequestBodySize` is
  also 10 MB.
- **Health checks** (lines 277–286): `postgresql` always; `redis`
  only when `Features:Redis:Enabled` (default
  `!IsDevelopment()`) and `RedisConnection` non-empty.
- **Response compression** (lines 289–307): Brotli + Gzip,
  `CompressionLevel.Optimal`, enabled for HTTPS.
- **JSON** (lines 318–322): camelCase property and dictionary key
  policies.

## App pipeline (lines 353–396)

`UseResponseCompression` → CORS (Dev or Production policy) →
`UseCorrelationId` → `LogEnrichmentMiddleware` →
`MetricsMiddleware` → `FileUploadValidationMiddleware` →
`SecurityHeadersMiddleware` → `UseCsrfToken` → `UseAuthentication`
→ `UseAuthorization` → `IpRateLimitMiddleware` →
`UserActiveCheckMiddleware` → `MaintenanceModeMiddleware`.

**Important ordering quirk**: `IpRateLimitMiddleware` runs **after**
authentication but **before** `UserActiveCheckMiddleware`. The
inline comment (lines 386–388) explains this is intentional — rate
limiting must precede the active-user check so brute-forcing
deactivated accounts is throttled.

## Endpoints (top-level)

- `GET /health` — anonymous (line 399).
- `GET /api/csrf-token` — anonymous (lines 402–406).
- `GET /api/feature-flags` — `RequireAuthorization` (lines 409–413).
- `GET /metrics` — same gating as Auth Service (private IPs or
  `X-Metrics-Token`).
- SignalR: `/chatHub` (`ChatHub`), `/notificationHub`
  (`NotificationHub`) (lines 446–447).
- All `[ApiController]`s under `DevHunt.CoreApi/Controllers/`
  (≈50 controllers — not enumerated here, will be summarized in
  `domains/*.md` on Step 4).

## Post-Build startup actions

- `LlmModelSeeder.SeedAsync` runs once after `app.Build()`
  (lines 452–461). Wrapped in try/catch — if seeding fails, the
  service continues with whatever rows already exist.
- `IObjectStorageService.InitializeBucketsAsync` runs when
  `ObjectStorage:InitializeBucketsOnStartup` is true (default)
  (lines 466–481). Failure logs a warning and the service starts
  in degraded mode (uploads may fail).

## Outbound dependencies

- **PostgreSQL** — both `DefaultConnection` (RW) and
  `ReadOnlyConnection` (RO). The `ReadWriteDbContextFactory`
  singleton (line 216) selects between them per request.
- **Redis** — caching + SignalR backplane + rate limiting.
- **RabbitMQ** — `devhunt.events` exchange (compose env), via
  `RabbitMQEventBusService`.
- **SeaweedFS / S3** — via `S3ObjectStorageService` using AWSSDK.S3.
- **ml-service** (`MLService:BaseUrl` →
  `http://ml-service:8000`) for AI/recommendations.
- **integration-gateway** (`IntegrationGateway:BaseUrl` →
  `http://integration-gateway:5002`).
- **notification-service** (`NotificationService:BaseUrl` →
  `http://notification-service:5003`).
- **OpenObserve** for OTLP traces and Serilog logs.

## What I should NOT assume

- **No migrations on startup** — same as Auth Service, the
  comment at lines 338–339 explicitly defers to
  `DevHunt.DatabaseMigrator`.
- **EventBus is not always present.** Code that calls
  `IEventBusService` may end up against `NoOpEventBusService`
  silently. Tests and dev environments without RabbitMQ run in
  this mode by default unless `Features:EventBus:Enabled` is
  forced and a connection string is provided.
- **CORS allows `PATCH`** in Production policy (line 101) — Auth
  Service does not. Don't conflate the two policies.
- **Antiforgery `SameSite=Lax`** here vs `Strict` in Auth
  Service — cross-site GETs hitting Core API will carry the
  cookie, which is intentional but worth knowing.
- **Object storage init can fail silently.** The `InitializeBuckets`
  call is wrapped; the API will start in degraded mode and only
  upload-related endpoints will fail.
