# SRS_MAP — инвентаризационная карта DevHunt (reverse-engineered)

Карта построена ТОЛЬКО по исходному коду (контроллеры, роуты, модели, миграции, конфиги,
docker-compose, csproj/package.json/lock-файлы) и заметкам в `/memory`. README, `/docs`
и прочие `*.md` игнорировались. Формат ссылок: `путь:строка`.

Снимок репозитория: ветка `main`, коммит `36e132c`.

---

## 1. Сервисы (deployment topology из docker-compose.yml)

| Сервис               | Технология               | Порт (host)                    | Build / Image                                   | Роль                                           |
| -------------------- | ------------------------ | ------------------------------ | ----------------------------------------------- | ---------------------------------------------- |
| db                   | PostgreSQL 16 + pgvector | 5432                           | `pgvector/pgvector:pg16` (`docker-compose.yml`) | единая БД платформы                            |
| cache-service        | Redis 7                  | 6379                           | `redis:7-alpine`                                | кэш, rate-limit счётчики, SignalR backplane    |
| message-broker       | RabbitMQ 3               | 5672/15672                     | `rabbitmq:3-management-alpine`                  | topic-exchange `devhunt.events`                |
| object-storage       | SeaweedFS                | 8333 (S3)                      | `chrislusf/seaweedfs:latest`                    | S3-хранилище файлов/аватаров                   |
| db-migrator          | .NET 10                  | — (one-shot)                   | `DevHunt.DatabaseMigrator/Dockerfile`           | применяет EF-миграции до старта API            |
| auth-service         | .NET 10 (ASP.NET Core)   | 7001→8080                      | `DevHunt.AuthService/Dockerfile`                | регистрация, JWT, OAuth, TOTP                  |
| core-api             | .NET 10 (ASP.NET Core)   | 7002→8080                      | `DevHunt.CoreApi/Dockerfile`                    | основной REST API + SignalR                    |
| ml-service           | Python 3.11 / FastAPI    | 8000                           | `ml-service/Dockerfile`                         | рекомендации, LLM-генерация, эмбеддинги        |
| notification-service | Node 20 / Express 5      | 5003                           | `notification-service/Dockerfile`               | email/SMS/push, алерты OpenObserve             |
| integration-gateway  | Node 20 / Express 5      | 5002                           | `integration-gateway/Dockerfile`                | GitHub/GitLab OAuth, вебхуки, синхронизация    |
| code-analyzer        | Python 3.13              | 8090 (localhost)               | `DevHunt.Analyzer/Dockerfile`                   | статический анализ репозиториев (~481 правило) |
| frontend             | Next.js 16 / React 19    | 3000                           | `frontend/Dockerfile`                           | SPA/SSR клиент, BFF-прокси                     |
| api-gateway          | Nginx                    | 80/443                         | `nginx/Dockerfile`                              | TLS, rate-limit на границе, маршрутизация      |
| openobserve          | OpenObserve              | 5080                           | `public.ecr.aws/zinclabs/openobserve:latest`    | логи/метрики/трейсы                            |
| backup-service       | postgres:16-alpine       | —                              | inline command                                  | ежедневный `pg_dump`, ретенция 7 дней          |
| openobserve-init     | alpine:3.19              | — (one-shot)                   | `monitoring/openobserve/init-alerts.sh`         | сидинг алертов                                 |
| documentation        | Docusaurus               | 9000 (docker-compose.docs.yml) | `documentation/Dockerfile`                      | портал документации (отдельный compose)        |

Не задеплоено (нет в compose и в `DevHunt.slnx`): `DevHunt.DatabaseSeeder/` (ручной сидер demo-данных,
`DevHunt.DatabaseSeeder/Program.cs`), см. `memory/unverified/orphaned-projects.md`.
`DevHunt.CoreApi.Tests`, `DevHunt.AuthService.Tests`, `tests/` — тестовые проекты.
`packages/throttle` — общая Node-библиотека backpressure (используют notification-service и integration-gateway).

---

## 2. auth-service — эндпоинты

Базовый роут `api/auth` (`DevHunt.AuthService/Controllers/AuthController.cs:27`).

| Метод | Путь                          | Auth                 | Назначение                                            | Строка                |
| ----- | ----------------------------- | -------------------- | ----------------------------------------------------- | --------------------- |
| POST  | /api/auth/register            | anon                 | регистрация + email-верификация                       | AuthController.cs:62  |
| POST  | /api/auth/login               | anon                 | логин (пароль + опц. TOTP/recovery), httpOnly cookies | AuthController.cs:122 |
| POST  | /api/auth/refresh             | anon (refresh token) | ротация refresh-токена                                | AuthController.cs:206 |
| GET   | /api/auth/me                  | JWT                  | профиль текущего пользователя                         | AuthController.cs:237 |
| POST  | /api/auth/logout              | anon                 | ревокация refresh, очистка cookies                    | AuthController.cs:277 |
| POST  | /api/auth/verify-email        | anon                 | проверка 6-значного кода                              | AuthController.cs:296 |
| POST  | /api/auth/resend-verification | anon                 | повторная отправка кода                               | AuthController.cs:333 |
| POST  | /api/auth/forgot-password     | anon                 | запрос сброса пароля                                  | AuthController.cs:375 |
| POST  | /api/auth/reset-password      | anon                 | сброс пароля по токену                                | AuthController.cs:416 |
| GET   | /api/auth/check-username      | anon                 | проверка доступности username                         | AuthController.cs:472 |
| GET   | /api/auth/login/{provider}    | anon                 | старт OAuth (github/google)                           | AuthController.cs:487 |
| GET   | /api/auth/callback/{provider} | anon                 | OAuth callback → one-time exchange code               | AuthController.cs:506 |
| POST  | /api/auth/oauth/exchange      | anon                 | обмен one-time кода на токены                         | AuthController.cs:560 |
| POST  | /api/auth/totp/setup          | JWT                  | старт настройки TOTP                                  | AuthController.cs:660 |
| POST  | /api/auth/totp/verify-setup   | JWT                  | включение 2FA + recovery-коды                         | AuthController.cs:684 |
| POST  | /api/auth/totp/disable        | JWT                  | отключение 2FA                                        | AuthController.cs:719 |
| GET   | /api/auth/totp/status         | JWT                  | статус 2FA                                            | AuthController.cs:759 |
| GET   | /api/auth/csrf-token          | anon                 | выдача antiforgery-токена                             | Program.cs:384        |
| GET   | /health                       | anon                 | health check                                          | Program.cs:391        |
| GET   | /metrics                      | token/loopback       | Prometheus                                            | Program.cs:395        |

Таблицы: `Users`, `RefreshTokens` (через общий `DevHuntDbContext`).
Зависимости: PostgreSQL, Redis (rate limiting, OAuth exchange codes), SMTP (MailKit), OpenObserve (OTLP).

---

## 3. core-api — эндпоинты по контроллерам

Все файлы в `DevHunt.CoreApi/Controllers/`. Auth = класс-уровень, если не указано иное.
Роли admin/curator/superadmin для админ-эндпоинтов проверяются по БД, а не только по JWT-claims
(`AdminController.cs:118-127`).

### 3.1 Auth-域: профиль, пользователи, ключи

**ProfileController** — `api/profile`, `[Authorize]` (ProfileController.cs:34):
GET /me:145 · GET /{id} (anon):193 · PUT /me:246 · POST /deactivate:399 · POST /activate:429 ·
POST /avatar:474 · DELETE /avatar:504 · GET /privacy:565 · PUT /privacy:579.
Хранилище аватаров: SeaweedFS S3. События: `profile.updated`, `avatar.uploaded/deleted`.

**UsersController** — `api/users` (UsersController.cs:26):
GET /online-status (anon):75 · GET / и /search (anon):148 · GET /{id} (anon):165 ·
POST /{userId}/follow:203 · DELETE /{userId}/follow:256 · GET /suggested:292 ·
GET /{userId}/followers (anon):308 · GET /{userId}/following (anon):323 ·
GET /me/settings:383 · PUT /me/settings:407 · GET /{userId}/activities (anon):494 · GET /{userId}/stats (anon):535.

**UserApiKeysController** — `api/me/api-keys`, `[Authorize]` (UserApiKeysController.cs:14):
GET /:37 · POST /:56 · DELETE /{id}:99. BYOK-ключи LLM, AES-256-GCM шифрование.

### 3.2 Проекты и команды

**ProjectsController** — `api/projects` (ProjectsController.cs:37):
GET / (anon):304 · GET /{id} (anon, кэш 15 мин):366 · GET /{id}/permissions:410 · GET /by-slug/{slug} (anon):428 ·
POST /{id}/toggle-boost:459 · POST /:509 · PUT /{id}:566 · PATCH /{id}/status:601 · PATCH /{id}/visibility:664 ·
PATCH /{id}/settings:691 · DELETE /{id}:722 · POST /{id}/transfer-ownership:774.
Лимит: 20 проектов на пользователя. События `project.*`.

**ProjectLifecycleController** — `api/projects` (ProjectLifecycleController.cs:21):
POST /{id}/archive:54 · /unarchive:73 · /publish:94 · /unpublish:113 · /activate (≥2 участников):132 ·
/complete:167 · /cancel:186. События `project.published/activated/completed/...`.

**ProjectTeamController** — `api/projects/{projectId}/team`, `[Authorize]` (ProjectTeamController.cs:15):
GET / и /members (anon):65 · PATCH /{memberId}/permissions:79 · GET /roles (anon):93 · POST /members:104 ·
POST /members/leave:115 · DELETE /members/{userId}:125 · POST /roles:135 · POST /roles/transfer-leadership:146.

**InvitationsController** — `api/invitations`, `[Authorize]` (InvitationsController.cs:60):
POST /send:129 · GET /incoming:320 · GET /sent:359 · POST /respond:397 · DELETE /{invitationId}:510.
Два потока: invite и request. События `invitation.*`.

**ProjectSkillsController** — `api/projects/{projectId}/skills` (ProjectSkillsController.cs:16):
GET / (anon):40 · POST / (owner):89 · PUT /{projectSkillId} (owner):148 · DELETE /{projectSkillId} (owner):197.

**ProjectSubscriptionsController** — `api/projects/{projectId}/subscriptions`, `[Authorize]` (ProjectSubscriptionsController.cs:18):
POST /:42 · DELETE /:84.

**ProjectMetricsController** — `api/projects/{projectId}/metrics`, `[Authorize]` (ProjectMetricsController.cs:25):
GET /:50 · GET /velocity:85 · GET /contributions:135.

**ReviewsController** — `api/reviews`, `[Authorize]` (ReviewsController.cs:37):
GET /project/{projectId} (anon):96 · POST /:166 · PUT /{reviewId} (автор, 24 ч):237 · DELETE /{reviewId}:306.

### 3.3 Задачи (kanban)

**TasksController** — `api/projects/{projectId}/tasks`, `[Authorize]` (TasksController.cs:52):
GET /:227 · POST /:317 · PUT /{taskId}:396 · DELETE /{taskId} (soft):453 · POST /{taskId}/restore:506 · POST /reorder:537.

**TaskColumnsController** — `api/projects/{projectId}/columns`, `[Authorize]` (TaskColumnsController.cs:25):
GET /:126 · POST /:171 · PUT /{columnId}:261 · DELETE /{columnId}:322 · POST /reorder:427.

**TaskLinksController** — `api/projects/{projectId}/tasks/{taskId}/links`, `[Authorize]` (TaskLinksController.cs:28):
GET /:100 · POST / (типы blocks/depends_on/... + защита от циклов):163 · DELETE /{linkId}:278.

**TaskAttachmentsController** — `api/projects/{projectId}/tasks/{taskId}/attachments`, `[Authorize]` (TaskAttachmentsController.cs:23):
GET /:95 · POST / (до 50 МБ, S3):142 · POST /link:238 · GET /{attachmentId}:310 · DELETE /{attachmentId}:379.

**TaskBoardSettingsController** — `api/projects/{projectId}/board-settings`, `[Authorize]` (TaskBoardSettingsController.cs:25):
GET /:94 · PUT /:148.

**GitHubTaskSyncController** — `api/internal/github-tasks`, internal HMAC (GitHubTaskSyncController.cs:23):
PATCH .../tasks/{taskId}/link:63 · POST .../tasks:75 · PATCH .../complete:116 · PATCH .../reopen:127 ·
PATCH .../content:138 · PATCH .../labels:148 · GET .../tasks/by-issue/{gitHubIssueId}:157.
Вызывается только integration-gateway.

### 3.4 Чат и каналы

**ChatController** — `api/chat`, `[Authorize]` (ChatController.cs:31):
GET /conversations:86 · POST /conversations/direct/{otherUserId}:96 · POST /conversations/project/{projectId}:106 ·
GET /conversations/{id}/messages:126 · POST /conversations/{id}/messages:139 · PUT /messages/{id}:149 ·
DELETE /messages/{id}:159 · POST /messages/{id}/reactions:170 · PUT /messages/{id}/pin:180 ·
POST /conversations/group:191 · POST /conversations/{id}/participants/{userId}:201 ·
DELETE /conversations/{id}/participants/{userId}:211 · POST /conversations/{id}/leave:222 ·
PUT /conversations/{id}/mute:233 · GET /conversations/{id}/participants:243.

**ProjectChannelsController** — `api/projects/{projectId}/channels`, `[Authorize]` (ProjectChannelsController.cs:16):
GET /:57 · POST /:67 · PATCH /{channelId}:79 · DELETE /{channelId}:91 · POST /{channelId}/join:101 · POST /{channelId}/leave:111.

**ChannelMembersController** — `api/projects/{projectId}/channels/{channelId}/members`, `[Authorize]` (ChannelMembersController.cs:14):
GET /:51 · GET /banned:58 · GET /candidates:65 · GET /roles:72 · POST /roles:79 · PUT /roles/{roleId}:90 ·
DELETE /roles/{roleId}:102 · POST /:109 · PATCH /{targetUserId}:120 · DELETE /{targetUserId}:132 ·
POST /{targetUserId}/ban:140 · POST /{targetUserId}/unban:152.

**SignalR-хабы** (`DevHunt.CoreApi/Program.cs:471-472`): `/chatHub` (`Hubs/ChatHub.cs`, методы
JoinConversation/LeaveConversation/JoinProject/LeaveProject/SendMessage/MarkAsRead/Typing),
`/notificationHub` (`Hubs/NotificationHub.cs`). Оба `[Authorize]`, JWT из query `access_token`.

### 3.5 Лента, активность, новости

**ActivitiesController** — `api/activities` (anon, фильтры видимости) — GET /:80 (ActivitiesController.cs:22).
**FeedController** — `api/feed`, `[Authorize]` — GET /:99 (FeedController.cs:24), персональная лента.
**ProjectNewsController** — `api/projects/{projectId}/news` (ProjectNewsController.cs:24):
GET / (anon):84 · GET /{newsId} (anon):100 · POST /:112 · PUT /{newsId}:131 · DELETE /{newsId}:143 ·
POST /{newsId}/like:157 · GET /{newsId}/like (anon):215 · GET /{newsId}/comments (anon):264 ·
POST /{newsId}/comments:303 · PUT /{newsId}/comments/{commentId}:350 · DELETE /{newsId}/comments/{commentId}:385.

### 3.6 Витрина (showcase)

**ShowcaseController** — `api` (ShowcaseController.cs:21):
GET /projects/{projectId}/showcase (anon):55 · GET /showcase (anon):119 · POST /projects/{projectId}/showcase (owner):175 ·
PUT ... (owner/admin):245 · POST .../like:303 · POST .../unlike:365 · POST .../feature (roles admin,curator):414 ·
POST .../unfeature (roles admin,curator):457 · DELETE ...:491 · GET .../comments (anon):534 · POST .../comments:597 ·
PUT .../comments/{commentId}:668 · DELETE .../comments/{commentId}:719. События `showcase.*`.

### 3.7 Файлы и документы

**ProjectFilesController** — `api/projects` (ProjectFilesController.cs:24):
GET /{projectId}/files:256 · GET /{projectId}/gallery (anon):311 · POST /{projectId}/files (до 100 МБ, S3):362 ·
GET /{projectId}/files/{fileId} (anon+видимость):449 · DELETE /{projectId}/files/{fileId}:498.

**ProjectDocumentsController** — `api/projects` (ProjectDocumentsController.cs:22):
GET /{projectId}/docs:89 · GET /{projectId}/docs/{docId}:173 · POST /{projectId}/docs:224 ·
PUT /{projectId}/docs/{docId}:273 · DELETE ... (soft):324 · POST /{projectId}/docs/generate-passport (ML):372 ·
POST /{projectId}/docs/generate-diagram (ML):489.

**ProjectArtifactsController** — `api/projects/{projectId}/artifacts`, `[Authorize]`, помечен `[Obsolete]` (ProjectArtifactsController.cs:16):
GET /:44 · GET /{type}:74 · POST /generate (ML):105 · PUT /{type}:214.

### 3.8 AI / LLM

**AIController** — `api/ai`, `[Authorize]` + feature flag `ai_features` (AIController.cs:16-18):
POST /chat/conversations/{id}:61 · POST .../regenerate:121 · POST .../estimate:175 ·
POST .../cancel/{requestId}:212 · POST .../tools/execute:238 · POST /tech-stack (legacy ML):273.
BYOK LLM-чат со стримингом через SignalR; лимит 30 req/min/user (`Services/Ai/Llm/AiRateLimiter.cs:57`).

**AiPlansController** — `api/projects/{projectId}/ai/plans`, `[Authorize]` + `ai_features` (AiPlansController.cs:13-15):
POST /tech-stack:40 · POST /:59 · GET /{planId}:77 · POST /{planId}/apply:91 · POST /refine:106 ·
POST /tech-stack/refine:125 · POST /diagram:143.

**AiPlanDraftsController** — `api/ai/plans/draft`, `[Authorize]` + `ai_features` (AiPlanDraftsController.cs:17-19):
POST /:40 · POST /refine:62 · POST /tech-stack:84 (эфемерные черновики, без записи в БД).

**LlmController** — `api/llm`, `[Authorize]` + `ai_features` (LlmController.cs:13-15):
GET /providers:37 · GET /models:57 · POST /providers/{provider}/models/sync:86.

### 3.9 Анализ кода

**CodeAnalysisController** — `api/projects/{projectId}/code-analysis`, `[Authorize]` + активное членство (CodeAnalysisController.cs:197):
GET /latest:228 · GET /history:250 · GET /results/{resultId}:272 · GET /summary:413 · GET /issues:543 ·
GET /semantic-search (pgvector):639 · POST /generate-embeddings:707 · GET /config:1016 ·
PATCH /config/exclude-patterns:1041 · POST /dismiss:1066 · POST /undismiss:1112.

**CodeAnalysisInternalController** — `api/internal/code-analysis`, internal HMAC (CodeAnalysisController.cs:21):
POST /results:115 · GET /embedding-jobs/{jobId}:169. Вызывается integration-gateway.

### 3.10 Интеграции

**IntegrationsController** — `api/integrations`, `[Authorize]` (IntegrationsController.cs:31):
GET /project/{projectId}:90 · POST /project/{projectId}:111 · GET /{integrationId}:180 ·
GET /{integrationId}/token (internal HMAC):209 · GET /{integrationId}/github-repos:251 · PUT /{integrationId}:308 ·
PUT /{integrationId}/toggle:363 · POST /{integrationId}/sync:390 · DELETE /{integrationId}:450 ·
GET /oauth/{serviceType}/authorize:483 · GET /oauth/{serviceType}/callback (anon + state):529 ·
POST /webhook/{integrationId} (anon + подпись):560 · GET /by-repository (internal):610 ·
GET /{id}/decrypt-token (internal):660 · GET /project/{projectId}/for-internal (internal):691.
Токены шифруются AES-256-GCM; OAuth CSRF state в Redis.

### 3.11 Рекомендации, скиллы, бейджи

**RecommendationsController** — `api/recommendations`, `[Authorize]` (RecommendationsController.cs:30):
GET /me (refresh cooldown 5 мин):148 · GET /project/{projectId} (admin/curator):265 · PUT /{id}/view:329 ·
PUT /{id}/action:365 · DELETE /{id}:401 · POST /refresh-all (admin):438 · POST /refresh/{userId} (admin/curator):474.

**SkillsController** — `api/skills`, `[Authorize]` (SkillsController.cs:32):
GET / (anon):106 · GET /{id} (anon):157 · GET /search (anon):193 · GET /suggest (anon):225 · GET /categories (anon):337 ·
GET /resolve (anon):365 · POST / (policy AdminOrCurator):468 · PUT /{id}:527 · DELETE /{id}:588 · GET /my:639 ·
POST /my/{skillId}:717 · PUT /my/{skillId}:776 · DELETE /my/{skillId}:818 · GET /user/{userId} (anon):861 ·
PUT /{skillId}/verify/{userId} (AdminOrCurator):937.

**BadgesController** — `api/badges` (BadgesController.cs:15):
GET / (anon):87 · GET /{id} (anon):101 · GET /me:112 · GET /user/{userId} (anon):125 ·
POST /award/{userId}/{achievementCode} (admin):139 · PUT /progress/{userId}/{achievementCode}:162 ·
GET /stats/{userId} (anon):181 · POST / (admin):192 · PUT /{id} (admin):208 · DELETE /{id} (admin):221.

**MetadataController** — `api/metadata`, `[AllowAnonymous]` (MetadataController.cs:13):
GET /:34 · GET /skills/categories:55 · GET /badges/categories:72 · GET /projects/statuses:89 ·
GET /projects/difficulties:107 · GET /projects/visibilities:123.

### 3.12 Уведомления, модерация, комьюнити, поддержка

**NotificationsController** — `api/notifications`, `[Authorize]` (NotificationsController.cs:20):
GET /:72 · POST /mark-read/{id}:119 · POST /mark-all-read:134 · POST / (admin/curator, SignalR push):157 ·
GET /unread-count:184 · DELETE /{id}:196 · DELETE /:209.

**ModerationController** — `api/moderation`, `[Authorize]` (ModerationController.cs:16):
POST /report:37 · GET /queue (admin/curator по БД):55 · POST /decision (admin/curator по БД):68.

**CommunityController** — `api/community` (CommunityController.cs:19):
POST /feedback:139 · GET /feedback (anon):188 · GET /feedback/{id} (anon):240 · POST /feedback/{id}/vote:278 ·
DELETE /feedback/{id}/vote:333 · POST /feedback/{id}/comments:368 · PUT /feedback/{id}/status (admin/curator):406 ·
PUT /feedback/{id} (автор):459 · DELETE /feedback/{id}:502 · PUT /feedback/{id}/comments/{commentId} (автор, ≤1 ч):533 ·
DELETE /feedback/{id}/comments/{commentId}:564.

**SupportController** — `api/support`, `[Authorize]` (SupportController.cs:42):
POST /tickets:186 · GET /tickets:262 · GET /tickets/{ticketId}:322 · POST /tickets/{ticketId}/messages:389 ·
PUT /tickets/{ticketId}/close:493 · POST /tickets/{ticketId}/reopen:548 · GET /tickets/{ticketId}/history:656.

**ProjectIssuesController** — `api/projects` (ProjectIssuesController.cs:17):
POST /{projectId}/issues:69 · GET /{projectId}/issues:87 · POST .../{issueId}/cancel:98 · PUT .../{issueId}:110.

### 3.13 Администрирование

**AdminController** — `api/admin`, `[Authorize]` + роль по БД (AdminController.cs:19):
POST /users/block:163 · /users/unblock:199 · /users/verify:216 · /users/change-role (admin only):243 ·
POST /projects/action (hide/archive/feature/unfeature):286 · GET /stats:388 · GET /users:439 ·
GET /users/{id}/details:475 · GET /users/{id}/activity:501 · POST /users/suspend:521 · POST /users/unsuspend:542 ·
GET /users/{id}/notes:565 · POST /users/{id}/notes:586 · DELETE /users/notes/{noteId}:612 ·
POST /users/bulk-action (≤100):638 · GET /issues:710 · GET /issues/{issueId}:755 · POST /issues/{issueId}/assign:796 ·
POST /issues/{issueId}/resolve (admin):890 · PUT /issues/{issueId}/escalate:945 ·
PUT /users/{userId}/change-name:1013 · PUT /users/{userId}/edit-profile:1128 · GET /stats/extended:1158.

**AdminSupportController** — `api/admin/support` (AdminSupportController.cs:20):
GET /tickets:184 · POST /tickets/assign:228 · POST /tickets/resolve:272 · POST /tickets/{id}/reassign:315 ·
PUT /tickets/{id}/priority:369 · POST /tickets/{id}/escalate:420 · GET /tickets/all:528 · GET /stats:573.

**AdminChatController** — `api/admin/chat` (AdminChatController.cs:15):
GET /stats:68 · GET /conversations:101 · GET /conversations/{id}/messages:172 · DELETE /conversations/{id}:228 ·
DELETE /conversations/{id}/messages/{messageId}:260 · POST /cleanup-orphaned:284.

**AdminContentController** — `api/admin/content` (AdminContentController.cs:16):
GET /stats:60 · GET /projects:100 · GET /projects/{id}:146 · PUT /projects/{id}:176 · DELETE /projects/{id}:209 ·
GET /news:262 · DELETE /news/{id}:304 · GET /comments:341 · DELETE /comments/{id}:400 · GET /showcase:441 ·
DELETE /showcase/{id}:473.

**SuperAdminController** — `api/superadmin`, `[Authorize]` + `[IpWhitelist]` + подтверждение пароля (SuperAdminController.cs:19-21):
GET /audit-logs:178 · POST /hard-delete/user:206 · POST /hard-delete/project:256 · POST /promote-admin:293 ·
POST /demote-admin:332 · GET /system-info:360 · GET /admins:391 · POST /verify-password:422 · GET /settings:438 ·
PUT /settings:456 · GET /feature-flags:517 · PUT /feature-flags/{key}:548 · POST /maintenance:578 ·
GET /ip-whitelist:617 · POST /ip-whitelist:637 · POST /broadcast-notification:669 · DELETE /ip-whitelist/{ip}:717.

### 3.14 Системные эндпоинты core-api (Program.cs)

GET /health:427 · GET /api/csrf-token:430 · GET /api/feature-flags (JWT):439 · GET /metrics (token/loopback):449.

Внешние зависимости core-api: PostgreSQL, Redis (кэш/лимиты/backplane), RabbitMQ (outbox → `devhunt.events`),
SeaweedFS S3, ml-service (HTTP), integration-gateway (HTTP), notification-service (HTTP), OpenObserve (OTLP).

---

## 4. ml-service — эндпоинты

FastAPI, порт 8000. Auth: JWT HS256 (`security.py:19-21`) или сервисный `ML_SERVICE_TOKEN` (`security.py:74-77`).

| Метод  | Путь                                 | Назначение                        | Строка                         |
| ------ | ------------------------------------ | --------------------------------- | ------------------------------ |
| GET    | /                                    | сведения о сервисе                | main.py:291                    |
| GET    | /health                              | health check                      | main.py:275                    |
| GET    | /metrics                             | Prometheus (X-Metrics-Token)      | main.py:267                    |
| POST   | /api/recommendations/generate        | генерация рекомендаций            | routers/recommendations.py:382 |
| GET    | /api/recommendations/user/{user_id}  | рекомендации из кэша              | routers/recommendations.py:414 |
| POST   | /api/recommendations/refresh         | массовое обновление (202)         | routers/recommendations.py:482 |
| DELETE | /api/recommendations/cache/{user_id} | инвалидация кэша                  | routers/recommendations.py:507 |
| GET    | /api/ai/models                       | список LLM-моделей                | routers/ai.py:66               |
| POST   | /api/ai/generate-tech-stack          | генерация tech stack              | routers/ai.py:97               |
| POST   | /api/ai/generate-plan                | генерация плана                   | routers/ai.py:102              |
| POST   | /api/ai/refine-plan                  | уточнение плана                   | routers/ai.py:107              |
| POST   | /api/ai/refine-tech-stack            | уточнение tech stack              | routers/ai.py:113              |
| POST   | /api/ai/generate-diagram             | Mermaid-диаграмма                 | routers/ai.py:124              |
| POST   | /api/ai/chat                         | чат (Groq)                        | routers/ai.py:135              |
| POST   | /api/ai/chat-agent                   | агентный чат с tool-calling       | routers/ai.py:182              |
| GET    | /api/ai/tools                        | метаданные инструментов агента    | routers/ai.py:188              |
| POST   | /api/ai/tech-stack                   | legacy-варианты стека             | routers/ai.py:216              |
| POST   | /api/ai/roadmap                      | legacy roadmap                    | routers/ai.py:222              |
| POST   | /api/ai/tasks                        | legacy задачи по фазе             | routers/ai.py:233              |
| POST   | /api/ai/generate-passport            | паспорт проекта                   | routers/passport.py:190        |
| POST   | /api/embeddings/generate             | batch-эмбеддинги (fastembed 384d) | routers/embeddings.py:73       |
| POST   | /api/embeddings/query                | эмбеддинг запроса                 | routers/embeddings.py:97       |

Таблицы (чтение через asyncpg): `Users`, `UserSkills`, `Skills`, `Projects`, `ProjectTechStack`,
`ProjectRole`, `TeamMembers`, `Reviews` (`routers/recommendations.py:154-197`); consumer использует
lowercase `users`/`projects` — расхождение со схемой EF (`consumers/event_consumer.py:151,203`).
Зависимости: PostgreSQL, Redis (кэш рекомендаций 1 ч, кэш AI-ответов 10 мин), RabbitMQ
(consumer очереди `devhunt.ml.recommendations`), Groq API (основной LLM), Gemini API (fallback), OpenObserve.

---

## 5. notification-service — эндпоинты

Express 5, порт 5003. Auth: JWT HS256 (`src/middleware/auth.js:9-11`).

| Метод | Путь                                     | Назначение                                | Строка              |
| ----- | ---------------------------------------- | ----------------------------------------- | ------------------- |
| GET   | /                                        | сведения о сервисе                        | src/index.js:323    |
| GET   | /health                                  | health                                    | src/index.js:89     |
| GET   | /metrics, /metrics/throttle              | Prometheus / throttle                     | src/index.js:99,102 |
| POST  | /api/notifications                       | отправка email/SMS/push                   | src/index.js:107    |
| POST  | /api/notifications/bulk                  | 501 Not Implemented (заглушка)            | src/index.js:237    |
| PUT   | /api/notifications/:id/read              | 501 Not Implemented                       | src/index.js:242    |
| PUT   | /api/notifications/user/:userId/read-all | 501 Not Implemented                       | src/index.js:247    |
| POST  | /api/alerts/webhook                      | приём алертов OpenObserve → email админам | src/index.js:254    |

Каналы: email (SendGrid/SMTP, `src/services/emailService.js:11-17`), SMS (Twilio/mock,
`src/services/smsService.js:8-11`), push (FCM/mock, `src/services/pushService.js:8-9`).
БД: нет. Зависимости: RabbitMQ (queue `devhunt.notifications`), Redis (rate limit), SMTP/SendGrid, Twilio, FCM.

---

## 6. integration-gateway — эндпоинты

Express 5, порт 5002. Auth: JWT; вебхуки — HMAC-подпись; к core-api — internal HMAC (`INTERNAL_API_KEY`).

| Метод  | Путь                                                 | Назначение                              | Строка                       |
| ------ | ---------------------------------------------------- | --------------------------------------- | ---------------------------- |
| GET    | /, /health, /metrics, /metrics/throttle              | сервисные                               | src/index.js:521,500,512,515 |
| POST   | /api/oauth/authorize                                 | построить OAuth URL                     | src/routes/oauth.js:20       |
| GET    | /api/oauth/:provider/url                             | OAuth URL (legacy)                      | src/routes/oauth.js:97       |
| POST   | /api/oauth/callback                                  | обмен code → token                      | src/routes/oauth.js:157      |
| GET    | /api/oauth/:provider/callback                        | OAuth callback (legacy)                 | src/routes/oauth.js:247      |
| POST   | /api/sync                                            | синхронизация репозитория GitHub/GitLab | src/index.js:130             |
| POST   | /api/webhooks                                        | регистрация вебхука на GitHub/GitLab    | src/index.js:231             |
| DELETE | /api/webhooks/:integrationId/:serviceType/:webhookId | удаление вебхука                        | src/index.js:387             |
| POST   | /api/webhooks/verify-signature                       | проверка HMAC                           | src/routes/webhooks.js:17    |
| POST   | /api/webhooks/github                                 | входящие вебхуки GitHub (issues, push)  | src/routes/webhooks.js:61    |
| POST   | /api/webhooks/gitlab                                 | входящие вебхуки GitLab (только ack)    | src/routes/webhooks.js:115   |

Внешние API: GitHub (`api.github.com`), GitLab (`gitlab.com/api/v4`), code-analyzer (`/analyze`),
core-api (internal endpoints). Jira/Trello — заглушки (`src/services/eventConsumer.js:358-360`).
Зависимости: RabbitMQ (queue `devhunt.integrations`), Redis (rate limit), OpenObserve.

---

## 7. code-analyzer (DevHunt.Analyzer) — эндпоинты

Python 3.13, HTTP-сервер на 8090 (`devhunt_analyzer/api.py:29-30`). Не в решении .NET — это Python-анализатор.

| Метод | Путь     | Auth                         | Назначение                                                      | Строка     |
| ----- | -------- | ---------------------------- | --------------------------------------------------------------- | ---------- |
| GET   | /health  | нет                          | health                                                          | api.py:99  |
| GET   | /rules   | нет                          | список правил (~481)                                            | api.py:101 |
| POST  | /analyze | Bearer `ANALYZER_API_SECRET` | клонирование репо + анализ (tree-sitter, Semgrep, свои правила) | api.py:106 |

Языки: C#, TypeScript, Python, Java, Go, Rust, PHP, Ruby, Kotlin, Swift, C/C++, Docker, YAML.
Зависимости: git clone внешних репозиториев; вызывается только integration-gateway.

---

## 8. frontend — маршруты

Next.js 16 App Router, локали `pl` (default), `en` (`frontend/src/i18n/routing.ts:10-13`).
Защита `/dashboard/**`, `/admin/**` через NextAuth middleware (`frontend/src/middleware.ts:14-45`).

Страницы: `/` (лендинг), `/terms`, `/privacy`, `/maintenance`, `/oauth-callback`,
`/login`, `/register`, `/register/complete-profile`, `/forgot-password`, `/reset-password`, `/verify-email`,
`/projects`, `/search`, `/users/[userId]`, `/showcase`, `/showcase/[projectId]`, `/community`, `/internships` (mock-данные),
`/admin`, `/dashboard`, `/dashboard/projects`, `/dashboard/projects/[id]`, `/dashboard/chats`, `/dashboard/messages`,
`/dashboard/notifications`, `/dashboard/invitations`, `/dashboard/teams`, `/dashboard/internships` (mock),
`/dashboard/work`, `/dashboard/ai`, `/dashboard/support`, `/dashboard/support/new`, `/dashboard/support/[ticketId]`,
`/dashboard/showcase-preview`, `/dashboard/profile`, `/dashboard/profile/edit`, `/dashboard/profile/[userId]`,
`/dashboard/profile/security`, `/dashboard/profile/privacy`, `/dashboard/profile/ai-keys`.

BFF API-роуты: `/api/proxy-core/[...path]`, `/api/proxy-auth/[...path]`, `/api/proxy-ml/[...path]`
(allowlist в `frontend/src/lib/security/proxy-allowlist.ts`), `/api/auth/[...nextauth]`,
`/api/realtime/token` (JWT для SignalR), `/api/client-error`.
SignalR-клиенты: `frontend/src/lib/signalr.ts` (/chatHub), `frontend/src/lib/realtime/client.ts` (/notificationHub).

---

## 9. Модель данных — 63 таблицы (DbSet в `DevHunt.Infrastructure/DevHuntDbContext.cs:42-128`)

Актуальная схема: `Migrations/20260427180340_AddByokAndModelRegistry.Designer.cs` + последующие миграции
(снапшот `DevHuntDbContextModelSnapshot.cs` пуст). Полный каталог и связи — в SRS.md §8. Группировка:

- **Users/auth:** Users, RefreshTokens, UserPrivacySettings, UserApiKeys, User_Follows
- **Projects/teams:** Projects, ProjectBoosts, TeamMembers, Invitations, Project_Roles, Project_Tech_Stack,
  Project_Subscriptions, Reviews, Recommendations
- **Tasks:** Tasks, TaskColumns, TaskLinks, TaskAttachments, TaskBoardSettings
- **Chat:** Conversations, Conversation_Participants, Channel_Role_Definitions, Messages, AiMessageDetails, MessageReactions
- **Showcase:** Showcase_Projects, ShowcaseLikes, Showcase_Comments
- **Notifications/activity/news:** Notifications, Activity_Records, Project_News_Posts, News_Post_Likes, News_Post_Comments
- **Skills:** Skills, Skill_Aliases, User_Skills, User_SkillEntries
- **Integrations:** Integrations
- **Moderation/admin/support:** ModerationReports, ProjectIssues, SupportTickets, TicketMessages, TicketHistories,
  FeedbackItems, FeedbackVotes, FeedbackComments, AdminNotes, PlatformSettings, FeatureFlags, AuditLogs
- **Achievements:** Achievements, User_Achievements
- **AI/code analysis:** AiPlans, AiOperationLogs, ProjectArtifacts, CodeAnalysisResults,
  CodeAnalysisEmbeddings (pgvector 384), CodeAnalysisEmbeddingJobs, LlmModels, UserLlmModels
- **Content:** Project_Files, Project_Documents
- **Infrastructure:** OutboxEvents

---

## 10. Межсервисные связи

### 10.1 HTTP

| Кто                 | Кого                 | Конфиг                               | Файл                                                 |
| ------------------- | -------------------- | ------------------------------------ | ---------------------------------------------------- |
| frontend            | core-api             | `CORE_SERVICE_URL`/proxy             | frontend/src/lib/api/client.ts:53                    |
| frontend            | auth-service         | `AUTH_SERVICE_URL`/proxy             | frontend/src/lib/api/client.ts:67                    |
| frontend            | ml-service           | `/api/proxy-ml`                      | frontend/src/app/api/proxy-ml/[...path]/route.ts:109 |
| core-api            | ml-service           | `MLService:BaseUrl` + `ServiceToken` | DevHunt.CoreApi/Services/MLServiceClient.cs:166-177  |
| core-api            | integration-gateway  | `IntegrationGateway:BaseUrl`         | Services/IntegrationGatewayClient.cs:109             |
| core-api            | notification-service | `NotificationService:BaseUrl`        | Services/NotificationServiceClient.cs:78             |
| integration-gateway | core-api             | `CORE_API_URL` + internal HMAC       | src/services/eventConsumer.js:32, syncService.js:10  |
| integration-gateway | code-analyzer        | `CODE_ANALYZER_URL` + secret         | src/services/webhookHandler.js:14                    |
| integration-gateway | GitHub/GitLab        | публичные API                        | src/config/oauth.js:8-27                             |
| GitHub              | integration-gateway  | вебхуки /api/webhooks/github         | src/routes/webhooks.js:61                            |

### 10.2 RabbitMQ (topic exchange `devhunt.events`, DLX `{exchange}.dlx`)

Единственный публикатор — core-api через транзакционный outbox
(`Services/OutboxEventBusDecorator.cs:28-35`, воркер `OutboxEventProcessorWorker.cs:29-97`, опрос каждые 5 с).
Типы событий — `Services/EventBusService.cs:254-600`: `activity.record.created`, `project.*`,
`project.ownership.transferred`, `showcase.*`, `showcase.comment.*`, `profile.updated`, `avatar.*`,
`user.verified`, `team.member.*`, `invitation.*`, `message.sent`, `conversation.created`.

| Очередь                    | Consumer             | Routing keys                                                                                                                  |
| -------------------------- | -------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| devhunt.notifications      | notification-service | project._, showcase._, team._, invitation._, message.sent, conversation.created (`src/services/rabbitmqConsumer.js:44-51`)    |
| devhunt.ml.recommendations | ml-service           | profile.updated, project.completed, project.created, showcase.published (`consumers/event_consumer.py:69-74`)                 |
| devhunt.integrations       | integration-gateway  | project.created/updated/completed, task.created/updated/completed, showcase.published (`src/services/eventConsumer.js:56-64`) |

Разрывы контракта (код): `task.*` и `showcase.published` объявлены в фабриках `DomainEvents`,
но не публикуются контроллерами (Tasks пишет только activity log; Showcase публикует `showcase.submitted`,
`EventBusService.cs:385`, `ShowcaseController.cs:224`) — consumer-байндинги на них никогда не срабатывают.

### 10.3 Общие данные

- PostgreSQL разделяют: auth-service, core-api, db-migrator (EF Core) и ml-service (asyncpg, прямые SQL).
- Redis разделяют: auth-service (rate limit, OAuth exchange), core-api (кэш, rate limit, SignalR backplane,
  presence), ml-service (кэши), notification-service и integration-gateway (rate limit).
- SeaweedFS S3 — только core-api (аватары, файлы проектов, вложения задач).

---

## 11. Покрытие карты (checkpoint этапа 1)

Сервисы с кодом в репо: auth-service ✅, core-api (48 контроллер-файлов, 50 классов) ✅, ml-service ✅,
notification-service ✅, integration-gateway ✅, code-analyzer ✅, frontend ✅, db-migrator ✅,
database-seeder (не задеплоен) ✅, packages/throttle ✅, nginx/api-gateway ✅, documentation ✅.
Таблицы: 63/63 DbSet перечислены. Карта полна — можно писать SRS.
