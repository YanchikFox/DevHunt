# DevHunt — Software Requirements Specification (reverse-engineered)

Документ восстановлен методом reverse-engineering исключительно из исходного кода
(контроллеры, модели, миграции, конфиги, docker-compose, csproj/package.json/lock-файлы)
и заметок в `/memory`. README, `/docs` и прочие `*.md` намеренно игнорировались.
Каждое требование подтверждается ссылкой вида `путь:строка`. Требования, описанные в `/memory`,
но отсутствующие или нерабочие в коде, помечены **[not implemented]**.

Снимок: ветка `main`, коммит `36e132c`. Инвентаризационная карта: `SRS_MAP.md`.

Нумерация требований: `FR-<сервис>-<n>` (функциональные), `NFR-<n>` (нефункциональные),
`DM-<n>` (модель данных), `UC-<n>` (use cases).

---

## 1. Overview и цели продукта

DevHunt — платформа для организации хакатон-/pet-проектов и командной разработки.
Целевое поведение выведено из кода:

- **Поиск команды и проектов.** Пользователи создают проекты с требуемыми ролями и tech stack
  (`DevHunt.Infrastructure/Project.cs`, поля `RequiredRoles`, `TechStack`, `OpenRoles`),
  ищут друг друга по навыкам (`DevHunt.CoreApi/Controllers/UsersController.cs:148`,
  `SkillsController.cs:106`) и объединяются через приглашения и заявки
  (`InvitationsController.cs:129`, типы `invite`/`request` — `DevHunt.Infrastructure/Invitation.cs:10-11`).
- **Ведение проекта.** Жизненный цикл проекта draft → recruiting → active → completed
  (`DevHunt.Infrastructure/Constants/ProjectDomainConstants.cs:6-19`,
  `Controllers/ProjectLifecycleController.cs`), kanban-доска задач с колонками, связями,
  вложениями (`TasksController.cs`, `TaskColumnsController.cs`, `TaskLinksController.cs`),
  метрики velocity/burndown (`ProjectMetricsController.cs:50`).
- **Коммуникация.** Реал-тайм чат: личные сообщения, групповые чаты, проектные каналы
  (`ChatController.cs`, `Hubs/ChatHub.cs`), внутриплатформенные уведомления через SignalR
  (`Hubs/NotificationHub.cs`) и внешние email/SMS/push (`notification-service/src/index.js:107`).
- **AI-ассистирование.** Генерация планов реализации, tech stack, диаграмм и «паспорта проекта»
  через LLM (Groq/Gemini на стороне ml-service — `ml-service/services/ai_service.py:68-100`;
  BYOK-чат с ключами пользователя в core-api — `Controllers/AIController.cs:61`,
  `Controllers/UserApiKeysController.cs`), ML-рекомендации проектов по навыкам
  (`ml-service/routers/recommendations.py:382`).
- **Интеграции с VCS.** Подключение GitHub/GitLab-репозиториев, двунаправленная синхронизация
  задач с GitHub Issues (`integration-gateway/src/services/webhookHandler.js:33-45`,
  `Controllers/GitHubTaskSyncController.cs`), автоматический статический анализ кода при push
  (`integration-gateway/src/services/webhookHandler.js:344-348`, `DevHunt.Analyzer/devhunt_analyzer/api.py:106`)
  с семантическим поиском по проблемам (pgvector — `CodeAnalysisController.cs:639`).
- **Портфолио.** Публичная витрина завершённых проектов с лайками, комментариями, feature-курированием
  (`ShowcaseController.cs`), публичные профили с бейджами/достижениями (`BadgesController.cs`,
  56 сидируемых достижений — `DevHunt.DatabaseSeeder/Program.cs:276-334`) и отзывами (`ReviewsController.cs`).
- **Модерация и поддержка.** Жалобы и очередь модерации (`ModerationController.cs`), community-фидбек
  с голосованием (`CommunityController.cs`), тикеты поддержки (`SupportController.cs`,
  `AdminSupportController.cs`), полный административный контур с аудитом (`AdminController.cs`,
  `SuperAdminController.cs`, `DevHunt.Infrastructure/AuditLog.cs`).

Локализация интерфейса: польский (default) и английский (`frontend/src/i18n/routing.ts:10-13`);
уведомления генерируются с en/pl копиями (`AdminController.cs`, локализованные строки).

---

## 2. Актёры и роли

### 2.1 Платформенные роли (строковые, в `Users.Role`)

Источник: `DevHunt.CoreApi/Models/UserRoles.cs:8-13`, `DevHunt.Infrastructure/User.cs:9-14`.

| Роль          | Строка        | Возможности (из authorization-кода)                                                                                                                                                                                                                                                                                                      |
| ------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Участник      | `participant` | роль по умолчанию при регистрации (`DevHunt.AuthService/Services/RegistrationService.cs:47`) и OAuth-входе (`Services/OAuthService.cs:396`); все пользовательские функции                                                                                                                                                                |
| Компания      | `company`     | объявлена в модели как «аккаунт компании, может публиковать стажировки» (`User.cs:11`); допустимая цель демоушена (`SuperAdminController.cs:345`); **[not implemented]** — ни одного эндпоинта/проверки с этой ролью в коде нет, страницы стажировок работают на mock-данных (`frontend/src/app/[locale]/(public)/internships/page.tsx`) |
| Куратор       | `curator`     | модерация, админ-панели чтения/части действий: policy `AdminOrCurator` (`Extensions/AuthenticationExtensions.cs:89`), feature витрины (`ShowcaseController.cs:414`), верификация навыков (`SkillsController.cs:937`), очередь модерации (`ModerationController.cs:55`)                                                                   |
| Администратор | `admin`       | всё, что куратор + смена ролей (`AdminController.cs:243`), резолв project issues (`AdminController.cs:890`), управление бейджами (`BadgesController.cs:139-221`), refresh-all рекомендаций (`RecommendationsController.cs:438`)                                                                                                          |
| Суперадмин    | `superadmin`  | эксклюзивно: hard-delete, promote/demote админов, платформенные настройки, feature-флаги, maintenance mode, IP-whitelist, broadcast (`SuperAdminController.cs`); доступ дополнительно ограничен `[IpWhitelist]` (`SuperAdminController.cs:20-21`) и подтверждением пароля (BCrypt, `SuperAdminController.cs:133`)                        |

Анонимный посетитель — отдельный актёр: публичные каталоги проектов/витрины/профилей
(`ProjectsController.cs:304`, `ShowcaseController.cs:119`, `UsersController.cs:148`, `MetadataController.cs`).

Особенности авторизации: для admin-эндпоинтов роль перепроверяется по БД, а не только по JWT-claim
(`AdminController.cs:118-127`, `ModerationController.cs:89-98`); заблокированный пользователь
(`Users.IsActive=false`) получает 403 на каждый запрос (`Middleware/UserActiveCheckMiddleware.cs:42-46`).

### 2.2 Проектные роли (в рамках проекта)

- **Owner** — `Projects.OwnerId` (`DevHunt.Infrastructure/Project.cs`); эксклюзивно: удаление проекта,
  transfer-ownership, управление skills проекта (`ProjectsController.cs:722,774`, `ProjectSkillsController.cs:89`).
- **Team member** — запись в `TeamMembers` со статусом `active` и гранулярными флагами прав
  (CanManageTasks/CanEditTasks/CanManageFiles и т.п. — `DevHunt.Infrastructure/TeamMember.cs`,
  проверки в `TaskAttachmentsController.cs:142`, `TaskColumnsController.cs:171`).
- **Subscriber** — подписчик проекта (`Project_Subscriptions`), расширенная видимость новостей/файлов
  (`ProjectFilesController.cs:256`, уровни public/subscribers/members).

### 2.3 Роли в каналах чата

`ChannelRole`: `member`/`admin` (`DevHunt.Infrastructure/Models/ConversationParticipant.cs:191-198`)
плюс кастомные роли канала (`Channel_Role_Definitions`, `ChannelMembersController.cs:79`).

### 2.4 Сервисные (машинные) актёры

- **integration-gateway → core-api**: internal HMAC (`X-Request-Signature`,
  `DevHunt.CoreApi/Security/InternalServiceAuthenticator.cs`, `integration-gateway/src/utils/internalApiAuth.js:67-81`).
- **core-api → ml-service**: pre-shared `ML_SERVICE_TOKEN` (`ml-service/security.py:74-77`).
- **integration-gateway → code-analyzer**: Bearer `ANALYZER_API_SECRET` (`DevHunt.Analyzer/devhunt_analyzer/api.py:106`).
- **GitHub** (внешняя система): вебхуки с HMAC `X-Hub-Signature-256` (`integration-gateway/src/routes/webhooks.js:61`).
- **OpenObserve**: алерты по Bearer `ALERT_WEBHOOK_SECRET` (`notification-service/src/index.js:254`).

---

## 3. User flows по ролям

### 3.1 Анонимный посетитель

1. Открывает лендинг `/{locale}` → каталог проектов `/projects`, витрину `/showcase`, публичные профили
   `/users/[userId]` (`frontend/src/app/[locale]/(public)/…`).
2. Читает публичные данные: GET `/api/projects`, `/api/showcase`, `/api/users`, `/api/badges`,
   `/api/metadata/*` — всё `[AllowAnonymous]`.
3. Регистрируется: POST `/api/auth/register` → получает 6-значный код на email (24 ч TTL,
   `RegistrationService.cs:124`) → POST `/api/auth/verify-email` → логин. Альтернатива: OAuth GitHub/Google
   (`AuthController.cs:487-560`).

### 3.2 Участник (participant)

**Онбординг:** login (опц. TOTP) → `/register/complete-profile` → PUT `/api/profile/me` (навыки, bio, username)
→ POST `/api/profile/avatar`.

**Создание проекта:** POST `/api/projects` (лимит 20 проектов, автосоздание группового чата —
`ProjectsController.cs:509`) → настройка skills/ролей → POST `/{id}/publish` (draft→recruiting) →
приглашение участников POST `/api/invitations/send` → при ≥2 активных участниках POST `/{id}/activate` →
работа (задачи, каналы, новости, файлы) → POST `/{id}/complete` → создание витрины
POST `/api/projects/{id}/showcase`.

**Поиск проекта:** GET `/api/recommendations/me` (ML-скоринг по навыкам) или каталог → заявка
POST `/api/invitations/send` (type=request) → владелец отвечает POST `/api/invitations/respond` →
участник появляется в `TeamMembers`.

**Работа с задачами:** kanban `/dashboard/projects/[id]` → CRUD задач, drag-n-drop reorder
(`TasksController.cs:537`), связи между задачами, вложения; при подключённой GitHub-интеграции
закрытие issue на GitHub закрывает задачу (webhook → `GitHubTaskSyncController.cs:116`).

**AI:** добавляет BYOK-ключ (`/dashboard/profile/ai-keys` → POST `/api/me/api-keys`) → AI-чат в проекте
со стримингом и tool-calling (создание задач агентом — `AIController.cs:238`,
`ml-service/tools/definitions.py`) → генерация плана POST `/api/projects/{id}/ai/plans` → apply.

**Социальное:** follow (`UsersController.cs:203`), лайки/комментарии витрины и новостей, отзывы
на сокомандников (`ReviewsController.cs:166`), бейджи начисляются триггерами в контроллерах
(создание проекта, завершение задач, follow и др.).

**Поддержка/жалобы:** POST `/api/support/tickets`, POST `/api/moderation/report`,
community-фидбек POST `/api/community/feedback` + голосование.

### 3.3 Куратор (curator)

Очередь модерации GET `/api/moderation/queue` → решение POST `/api/moderation/decision`;
блокировка/верификация пользователей (`AdminController.cs:163-216`); ведение тикетов
(`AdminSupportController.cs`); feature/unfeature витрины; управление каталогом навыков и верификация
навыков пользователей (`SkillsController.cs:468-937`); статус community-фидбека (`CommunityController.cs:406`).

### 3.4 Администратор (admin)

Всё кураторское + смена ролей пользователей (кроме назначения superadmin —
`AdminController.cs:255-267`), резолв project issues с follow-up действиями (`AdminController.cs:890`),
CRUD бейджей, контент-модерация (проекты/новости/комментарии/витрина —
`AdminContentController.cs`), просмотр расшифрованных чатов и их удаление (`AdminChatController.cs:172`),
принудительный пересчёт рекомендаций (`RecommendationsController.cs:438`).

### 3.5 Суперадмин (superadmin)

Заходит только с whitelisted IP (`Security/IpWhitelistAttribute.cs:35-38`), для опасных операций
подтверждает пароль: hard-delete пользователей/проектов, promote/demote админов, платформенные
настройки, feature-флаги (в т.ч. `ai_features`), maintenance mode (503 для всех, кроме admin/superadmin —
`Middleware/MaintenanceModeMiddleware.cs:73-74`), broadcast-уведомления, аудит-лог
(`SuperAdminController.cs:178-717`).

### 3.6 Компания (company) — [not implemented]

Флоу «компания публикует стажировки» подразумевается моделью (`User.cs:11`) и mock-страницами
`/internships`, но серверных эндпоинтов стажировок нет ни в одном контроллере.

---

## 4. Функциональные требования (по сервисам)

Формат: требование + покрываемые эндпоинты (метод, путь, auth, файл:строка — полный реестр в `SRS_MAP.md`).

### 4.1 auth-service (DevHunt.AuthService)

- **FR-AUTH-1. Регистрация по email/паролю.** POST `/api/auth/register` валидирует email и пароль
  (8–128 символов, верхний/нижний регистр, цифра, спецсимвол `@$!%*?&` —
  `Services/AuthValidationService.cs:28-74`), хэширует пароль BCrypt (`RegistrationService.cs:120`),
  создаёт пользователя с ролью `participant` и отправляет 6-значный код верификации на 24 ч
  (`RegistrationService.cs:124`); в dev без SMTP — автоверификация (`Controllers/AuthController.cs:62`).
- **FR-AUTH-2. Верификация email.** POST `/api/auth/verify-email` (`AuthController.cs:296`),
  POST `/api/auth/resend-verification` с enumeration-safe ответом (`AuthController.cs:333`).
  Логин до верификации блокируется (`Services/LoginService.cs:50-110`).
- **FR-AUTH-3. Логин и выдача токенов.** POST `/api/auth/login` (`AuthController.cs:122`):
  constant-time BCrypt-сравнение (`LoginService.cs:99-100`), JWT access-токен HMAC-SHA256 на 30 минут
  с claims sub/email/role/jti (`Services/JwtTokenService.cs:34-56`), refresh-токен на 7 дней,
  httpOnly-cookies `access_token`/`refresh_token` (`Services/AuthCookieService.cs:35-74`).
- **FR-AUTH-4. Ротация refresh-токенов с защитой от повторного использования.**
  POST `/api/auth/refresh` (`AuthController.cs:206`): токен хранится как HMAC-SHA256-хэш, одноразовый;
  reuse ревокирует всё семейство токенов (`TokenFamilyId`) с причиной `reuse_detected`
  (`Services/RefreshTokenService.cs:121-138`); Serializable-транзакция с row-lock
  (`RefreshTokenService.cs:92-94`). POST `/api/auth/logout` ревокирует токен и чистит cookies
  (`AuthController.cs:277`).
- **FR-AUTH-5. Сброс пароля.** POST `/api/auth/forgot-password` (всегда success —
  `AuthController.cs:375`) → письмо с токеном на 1 час (`AuthController.cs:402`) →
  POST `/api/auth/reset-password` (`AuthController.cs:416`).
- **FR-AUTH-6. OAuth-вход GitHub и Google.** GET `/api/auth/login/{provider}` →
  GET `/api/auth/callback/{provider}` → одноразовый exchange-код (TTL 2 мин, в distributed cache —
  `Services/OAuthExchangeService.cs:32`) → POST `/api/auth/oauth/exchange` (`AuthController.cs:487-560`).
  Защита: HMAC-подписанный state на 10 минут (`Security/OAuthStateValidator.cs:12,86-87`),
  redirect только на origin из allowlist CORS (`Security/OAuthRedirectValidator.cs:6-47`).
  Привязка аккаунтов по `GithubId`/`GoogleId` (`Services/OAuthService.cs:41-414`).
- **FR-AUTH-7. Двухфакторная аутентификация TOTP.** POST `/api/auth/totp/setup` → секрет + otpauth-QR,
  POST `/api/auth/totp/verify-setup` → включение + 8 одноразовых recovery-кодов (BCrypt-хэши),
  POST `/api/auth/totp/disable` (пароль + опц. код), GET `/api/auth/totp/status` (`AuthController.cs:660-771`).
  Параметры: 30 с окно, 6 цифр, толерантность ±1 шаг (`Services/TotpService.cs:35-69`).
  При включённом 2FA логин без кода возвращает `TwoFactorRequiredResponse` (`AuthController.cs:141-162`).
- **FR-AUTH-8. Проверка username.** GET `/api/auth/check-username`, паттерн
  `^[a-zA-Z0-9_\-\.]{3,50}$` (`AuthController.cs:472`).
- **FR-AUTH-9. Текущий пользователь.** GET `/api/auth/me` по JWT (`AuthController.cs:237`).
- **FR-AUTH-10. CSRF-защита.** Глобальный фильтр на POST/PUT/DELETE/PATCH с заголовком `X-CSRF-TOKEN`
  и cookie `CSRF-TOKEN` (SameSite=Strict); исключения — анонимные auth-эндпоинты
  (`Filters/AuthCsrfValidationFilter.cs:23-34`); выдача токена GET `/api/auth/csrf-token` (`Program.cs:384-388`).
- **FR-AUTH-11. IP rate limiting per-endpoint.** AspNetCoreRateLimit с Redis-стором: login 5/мин,
  30/15 мин, 60/ч; register 3/мин, 10/ч; forgot-password 1/мин, 3/ч; общий `/api/auth/*` 200/15 мин
  и др. (`appsettings.json:36-125`, `Program.cs:238-263`); реальный IP из `X-Real-IP` (`appsettings.json:28`).
- **FR-AUTH-12. Email-рассылка.** SMTP через MailKit: письма верификации и сброса пароля
  (`Services/EmailService.cs:49-136`).

### 4.2 core-api: профили и пользователи

- **FR-USR-1. Профиль.** GET `/api/profile/me` (кэш 10 мин), публичный GET `/api/profile/{id}` с
  учётом privacy-настроек, PUT `/api/profile/me` (профиль/навыки/username, profanity-фильтр),
  деактивация/реактивация аккаунта (`ProfileController.cs:145-429`). Событие `profile.updated` в шину.
- **FR-USR-2. Аватары.** POST/DELETE `/api/profile/avatar`, до 5 МБ, хранение в SeaweedFS S3
  bucket Avatars (`ProfileController.cs:474-504`); события `avatar.uploaded/deleted`.
- **FR-USR-3. Privacy-настройки.** GET/PUT `/api/profile/privacy` и GET/PUT `/api/users/me/settings`
  (видимость профиля/активности, notif-флаги — `ProfileController.cs:565-579`, `UsersController.cs:383-407`,
  таблица `UserPrivacySettings`).
- **FR-USR-4. Поиск и просмотр пользователей.** GET `/api/users` (+ alias `/search`), GET `/api/users/{id}`,
  публичные followers/following, активности и статистика с privacy-фильтрацией
  (`UsersController.cs:148-535`).
- **FR-USR-5. Подписки на пользователей (follow).** POST/DELETE `/api/users/{userId}/follow`,
  GET `/api/users/suggested`; таблица `User_Follows`; ачивка-триггер на follow (`UsersController.cs:203-292`).
- **FR-USR-6. Онлайн-присутствие.** GET `/api/users/online-status` (батч до 50 ID) на основе
  Redis-presence из SignalR-подключений (`UsersController.cs:75`, `Hubs/ChatHub.cs:77`).
- **FR-USR-7. BYOK-ключи LLM.** GET/POST/DELETE `/api/me/api-keys`: ключ валидируется у провайдера,
  шифруется AES-256-GCM, наружу отдаётся только маска; аудит операций
  (`UserApiKeysController.cs:37-99`, `Services/Ai/Llm/UserApiKeyService.cs:110-204`,
  `Security/EncryptionService.cs:88-120`; таблица `UserApiKeys`).

### 4.3 core-api: проекты, команды, приглашения

- **FR-PRJ-1. CRUD проектов.** GET (каталог с фильтрами/пагинацией), GET `/{id}` (кэш 15 мин,
  видимость public/private/unlisted), GET `/by-slug/{slug}`, POST (лимит 20 проектов/пользователя,
  автосоздание группового чата), PUT, DELETE (hard, транзакция) — `ProjectsController.cs:304-773`.
  События `project.created/updated/deleted`.
- **FR-PRJ-2. Жизненный цикл.** Статусы `draft/recruiting/active/completed/archived/cancelled`
  (`Constants/ProjectDomainConstants.cs:6-19`); переходы: publish/unpublish/activate (требует ≥2 активных
  участников — `ProjectLifecycleController.cs:132`)/complete/cancel/archive/unarchive, PATCH `/{id}/status`
  с матрицей переходов (`ProjectsController.cs:601`); `Status` — concurrency token.
- **FR-PRJ-3. Видимость и настройки.** PATCH `/{id}/visibility`, PATCH `/{id}/settings` (дефолтная
  видимость новостей/файлов) — `ProjectsController.cs:664-691`.
- **FR-PRJ-4. Владение.** POST `/{id}/transfer-ownership` только активному участнику
  (`ProjectsController.cs:774`); событие `project.ownership.transferred`.
- **FR-PRJ-5. Буст проектов.** POST `/{id}/toggle-boost`, таблица `ProjectBoosts`, счётчик
  `BoostsCount` (`ProjectsController.cs:459`).
- **FR-PRJ-6. Команда.** GET team/members (anon), join/leave, удаление участника, смена роли,
  transfer-leadership, гранулярные permissions PATCH `/{memberId}/permissions`
  (`ProjectTeamController.cs:65-146`); частичный уникальный индекс: один активный membership
  на пользователя в проекте (`Migrations/20260614120000_RestoreTeamMemberUniqueIndex.cs`).
- **FR-PRJ-7. Приглашения и заявки.** POST `/api/invitations/send` (invite от команды / request от
  кандидата), incoming/sent списки, respond (accept создаёт TeamMember + DM с системным сообщением),
  cancel (`InvitationsController.cs:129-510`). Статусы pending/accepted/declined/cancelled
  (`Invitation.cs:13-14`). События `invitation.*`.
- **FR-PRJ-8. Tech stack проекта.** GET/POST/PUT/DELETE `/api/projects/{id}/skills` (мутации — только
  владелец), таблица `Project_Tech_Stack` (`ProjectSkillsController.cs:40-197`).
- **FR-PRJ-9. Подписки на проекты.** POST/DELETE `/api/projects/{id}/subscriptions` (идемпотентно;
  на приватные — только участникам), таблица `Project_Subscriptions` (`ProjectSubscriptionsController.cs:42-84`).
- **FR-PRJ-10. Метрики проекта.** GET metrics (задачи/velocity/вклады/burndown/timeline),
  `/velocity` (1–52 недели), `/contributions` — только владельцу/активным участникам
  (`ProjectMetricsController.cs:50-135`).
- **FR-PRJ-11. Отзывы.** GET по проекту (anon), POST (активный участник; отзыв на проект или peer-отзыв,
  рейтинг 1–5), PUT (автор, 24 ч), DELETE (автор или admin/curator) с пересчётом агрегатов
  (`ReviewsController.cs:96-306`; таблица `Reviews`).
- **FR-PRJ-12. Project issues (жалобы на проект).** POST/GET issues, cancel, PUT (профанити-фильтр);
  админ-обработка: список, деталь, assign, resolve (admin only), escalate
  (`ProjectIssuesController.cs:69-110`, `AdminController.cs:710-945`; таблица `ProjectIssues`).

### 4.4 core-api: задачи (kanban)

- **FR-TSK-1. CRUD задач.** GET/POST/PUT/DELETE(soft)/restore + батч-reorder drag-n-drop;
  Serializable-транзакция на создание; activity-лог и ачивка на завершение
  (`TasksController.cs:227-537`). Статусы `todo/doing/review/done/archived/cancelled`, приоритеты
  `low/medium/high/urgent` (`TaskItem.cs:9-52`).
- **FR-TSK-2. Колонки доски.** CRUD + reorder, WIP-limit, canvas-координаты, флаги IsDefault/IsCompleted;
  при удалении — опциональная миграция задач (RepeatableRead) (`TaskColumnsController.cs:126-427`;
  таблица `TaskColumns`).
- **FR-TSK-3. Связи задач.** Типизированные связи `blocks/blocked_by/depends_on/related_to/duplicate_of/
parent_of/child_of` с автосозданием обратной связи и детекцией циклов зависимостей
  (`TaskLinksController.cs:100-278`; уникальность (source,target,type) — `Models/TaskLink.cs`).
- **FR-TSK-4. Вложения задач.** Загрузка до 50 МБ в S3 или линк на существующий файл проекта; скачивание
  потоком; права CanManageFiles/CanManageTasks (`TaskAttachmentsController.cs:95-379`).
- **FR-TSK-5. Настройки доски.** GET/PUT `/api/projects/{id}/board-settings`: режим отображения
  (валидация по `TaskBoardSettings.ValidViewModes`), pan/zoom канваса (клэмп 0.1–3.0), дефолтная
  колонка; автосоздание при первом GET (`TaskBoardSettingsController.cs:94-148`).
- **FR-TSK-6. Синхронизация с GitHub Issues.** Внутренний API `api/internal/github-tasks` (HMAC):
  создание задачи из issue, link, complete/reopen по закрытию/переоткрытию issue, синхронизация
  контента и labels→priority/tags, lookup по issue id (`GitHubTaskSyncController.cs:63-157`);
  инициируется вебхуками GitHub через integration-gateway (`webhookHandler.js:109-227`).
  XSS-санитизация импортируемого контента.

### 4.5 core-api: чат и каналы

- **FR-CHT-1. Диалоги.** Личные (get-or-create по userId), групповые (профанити-фильтр на создание),
  проектные чаты; список диалогов, участники, add/remove участника, leave, mute
  (`ChatController.cs:86-243`; `ConversationType` Direct/Group/ProjectChannel —
  `Models/Conversation.cs:114-131`).
- **FR-CHT-2. Сообщения.** Пагинированная история, отправка (до 10 000 символов —
  `Hubs/ChatHub.cs:260`), редактирование, soft-delete, emoji-реакции (уникальность
  message+user+emoji), pin/unpin (`ChatController.cs:126-180`; таблицы `Messages`,
  `MessageReactions`). Контент шифруется сервисом чата при хранении и расшифровывается для
  админ-просмотра (`AdminChatController.cs:172`, `IEncryptionService`).
- **FR-CHT-3. Реал-тайм.** SignalR `/chatHub` `[Authorize]`: join/leave conversation и project-групп,
  SendMessage, MarkAsRead (LastReadAt), typing-индикатор с троттлингом 3 с, presence online/offline
  (`Hubs/ChatHub.cs:77-328`); JWT в query `access_token` для WebSocket
  (`Extensions/AuthenticationExtensions.cs:50-55`).
- **FR-CHT-4. Проектные каналы.** CRUD каналов (slug уникален в проекте), join/leave
  (`ProjectChannelsController.cs:57-111`); участники: список/banned/candidates, приглашение,
  смена роли/permission-override, kick, ban/unban, кастомные роли канала CRUD
  (`ChannelMembersController.cs:51-152`; таблицы `Conversation_Participants`,
  `Channel_Role_Definitions`).
- **FR-CHT-5. Админ-надзор за чатами.** Статистика, просмотр диалогов и расшифрованных сообщений,
  удаление диалогов/сообщений, cleanup осиротевших групповых чатов (POST `/api/admin/chat/cleanup-orphaned`) —
  admin/curator по БД, с аудитом (`AdminChatController.cs:68-284`).

### 4.6 core-api: активность, лента, новости

- **FR-ACT-1. Журнал активности.** GET `/api/activities` с фильтрами и правилами видимости
  public/subscribers/members, исключение осиротевших проектов, обход для admin/curator
  (`ActivitiesController.cs:80`; таблица `Activity_Records` с jsonb payload). Записи создаются
  `IActivityLogService` из контроллеров задач/файлов/команды; событие `activity.record.created`.
- **FR-ACT-2. Персональная лента.** GET `/api/feed`: только `project.news_published`, обогащение
  контентом новости, лайками, вложениями; видимость по follow/членству/подписке (`FeedController.cs:99`).
- **FR-ACT-3. Новости проекта.** CRUD постов с видимостью public/subscribers/members
  (модерационное удаление admin/curator), лайк-toggle с пересчётом из источника, комментарии
  (правка автором ≤1 ч, soft-delete), профанити-фильтр и HTML-санитизация
  (`ProjectNewsController.cs:84-385`; таблицы `Project_News_Posts`, `News_Post_Likes`, `News_Post_Comments`).

### 4.7 core-api: витрина (showcase)

- **FR-SHW-1. Витрина проекта.** Владелец создаёт/обновляет showcase (summary, скриншоты jsonb,
  метрики jsonb, demo/repo URL c валидацией), удаление owner/admin; просмотр anon с атомарным
  инкрементом просмотров; публичный список с фильтром featured (`ShowcaseController.cs:55-303`;
  таблица `Showcase_Projects`).
- **FR-SHW-2. Лайки.** like/unlike один-на-пользователя, таблица `ShowcaseLikes`, события
  `showcase.liked/unliked` (`ShowcaseController.cs:303-365`).
- **FR-SHW-3. Комментарии.** Древовидные (parent), CRUD автором или админом, soft-delete,
  профанити-фильтр (`ShowcaseController.cs:534-719`; таблица `Showcase_Comments`).
- **FR-SHW-4. Feature-курирование.** POST feature/unfeature — только роли `admin,curator`
  (`ShowcaseController.cs:414-457`).

### 4.8 core-api: файлы и документы

- **FR-FIL-1. Файлы проекта.** Загрузка до 100 МБ в SeaweedFS S3 (bucket ProjectFiles) с блокировкой
  опасных расширений, тарифная видимость owner/member/subscriber/public, галерея медиа GET `/{projectId}/gallery`
  (anon для публичных), скачивание потоком с инкрементом счётчика, soft-delete
  (`ProjectFilesController.cs:256-498`; таблица `Project_Files`).
- **FR-FIL-2. Документы проекта.** CRUD markdown/HTML-документов с видимостью и счётчиком просмотров,
  профанити-цензура; типы `readme/wiki/guide/changelog/api-docs/general/ai-*`
  (`ProjectDocumentsController.cs:89-324`, `Constants/DocumentTypeConstants.cs:6-34`;
  таблица `Project_Documents`).
- **FR-FIL-3. AI-документы.** Генерация «паспорта проекта» (обзор/tech stack/архитектура/roadmap/
  decisions) POST `/{projectId}/docs/generate-passport` и Mermaid-диаграммы
  POST `/{projectId}/docs/generate-diagram` через ml-service в документы проекта
  (`ProjectDocumentsController.cs:372-489`). Устаревший вариант — артефакты проекта
  (`ProjectArtifactsController.cs:44-214`, `[Obsolete]`; таблица `ProjectArtifacts`, уникальность
  (ProjectId, Type)).

### 4.9 core-api: AI/LLM (BYOK)

- **FR-AI-1. AI-чат в диалогах.** POST `/api/ai/chat/conversations/{id}` + regenerate/estimate/cancel:
  LLM-вызовы с пользовательским BYOK-ключом, стриминг дельт через SignalR (`AiStreamStarted/Delta`),
  реестр in-flight запросов для отмены (`AIController.cs:61-212`; фиче-флаг `ai_features` на всех
  AI-контроллерах — `Filters/RequireFeatureFlagFilter.cs:12-40`). Прозрачность: полный payload
  AI-сообщений в `AiMessageDetails`, метаданные в `Messages.AiMetadataJson`.
- **FR-AI-2. Tool-calling.** POST `.../tools/execute` исполняет подтверждённые инструменты агента
  (create/update/move задач, обновление проекта и т.д.) и бродкастит `AiToolsExecuted`
  (`AIController.cs:238`, `Services/Ai/IAiToolExecutionService`).
- **FR-AI-3. Планирование проекта.** Генерация/refine плана и tech stack, генерация диаграммы,
  сохранение плана (`AiPlans`, jsonb), apply плана к проекту; черновики до создания проекта —
  эфемерные, без записи в БД (`AiPlansController.cs:40-143`, `AiPlanDraftsController.cs:40-84`).
  Учёт операций: `AiOperationLogs` (токены, провайдер, модель, статус).
- **FR-AI-4. Реестр моделей.** GET providers/models, синхронизация каталога моделей по BYOK-ключу
  пользователя; платформенный каталог `LlmModels` + пользовательский `UserLlmModels`; tier
  `cheap/balanced/smart` (`LlmController.cs:37-86`, `Models/LlmModel.cs:36-38`).
- **FR-AI-5. AI rate limiting.** 30 запросов/мин/пользователя по умолчанию
  (`Ai:RateLimit:PerMinutePerUser`, `Services/Ai/Llm/AiRateLimiter.cs:57`), 429 + Retry-After.

### 4.10 core-api: анализ кода

- **FR-CDA-1. Приём результатов анализа.** POST `/api/internal/code-analysis/results` (internal HMAC):
  сохранение результата (severity/category counts, issues — jsonb), инвалидация кэша, постановка
  embedding-джоба (`CodeAnalysisController.cs:115-169`; таблицы `CodeAnalysisResults`,
  `CodeAnalysisEmbeddingJobs`).
- **FR-CDA-2. Просмотр результатов.** latest/history/по id/summary/фильтрация issues — только активным
  участникам проекта (`CodeAnalysisController.cs:228-543`).
- **FR-CDA-3. Семантический поиск по проблемам.** GET `/semantic-search` через pgvector (384-мерные
  эмбеддинги, IVFFlat-индекс), генерация эмбеддингов POST `/generate-embeddings` с атомарной заменой
  (`CodeAnalysisController.cs:639-707`; `Migrations/20260310180000_AddCodeAnalysisEmbeddings.cs`).
- **FR-CDA-4. Конфигурация.** exclude-паттерны, dismiss/undismiss отдельных проблем
  (`CodeAnalysisController.cs:1016-1112`).

### 4.11 core-api: интеграции

- **FR-INT-1. CRUD интеграций проекта.** Типы сервисов включают github/gitlab
  (`IntegrationsController.cs:123`); create/update/toggle/delete/sync с проверкой прав manage/sync;
  access-токены шифруются AES-256-GCM и наружу не отдаются (`IntegrationsController.cs:90-450`;
  таблица `Integrations`, config jsonb). SSRF-валидация `base_url`.
- **FR-INT-2. OAuth-подключение внешнего сервиса.** GET `/oauth/{serviceType}/authorize` (CSRF-state
  в Redis) → GET `/oauth/{serviceType}/callback` → редирект на фронтенд
  (`IntegrationsController.cs:483-529`); обмен кода — через integration-gateway
  (`integration-gateway/src/routes/oauth.js:20-247`).
- **FR-INT-3. GitHub-репозитории.** GET `/{integrationId}/github-repos` по OAuth-токену
  (`IntegrationsController.cs:251`).
- **FR-INT-4. Вебхуки.** Приём POST `/webhook/{integrationId}` с проверкой подписи через gateway
  (`IntegrationsController.cs:560`); внутренние эндпоинты для gateway: token/decrypt-token/
  by-repository/for-internal (`IntegrationsController.cs:209,610-691`).

### 4.12 core-api: рекомендации, навыки, бейджи, метаданные

- **FR-REC-1. Рекомендации проектов.** GET `/api/recommendations/me` с опциональным ML-refresh
  (cooldown 1 раз в 5 мин на пользователя — `RecommendationsController.cs:148`), отметки
  viewed/actioned, удаление как негативный сигнал; обратный матчинг «люди для проекта»
  (admin/curator), refresh-all (admin, async) и точечный refresh (`RecommendationsController.cs:148-474`;
  таблица `Recommendations`: MatchScore decimal(5,4), ReasoningJson jsonb).
- **FR-SKL-1. Каталог навыков.** Публичные каталог/поиск (мин. 2 символа)/suggest с алиасами/categories/
  resolve сырых строк к каталогу; CRUD каталога — policy AdminOrCurator; удаление только неиспользуемых
  (`SkillsController.cs:106-588`; таблицы `Skills`, `Skill_Aliases`, нормализация
  `SkillNormalization`).
- **FR-SKL-2. Навыки пользователя.** GET/POST/PUT/DELETE `/api/skills/my*` (proficiency
  `beginner/intermediate/advanced/expert` — `UserSkill.cs:33-35`), публичный просмотр, верификация
  навыка admin/curator (`SkillsController.cs:639-937`; таблицы `User_Skills`, `User_SkillEntries`).
- **FR-BDG-1. Бейджи/достижения.** Публичный каталог и бейджи пользователя со статистикой; свои —
  с прогрессом; CRUD определений и ручное награждение — admin по БД (`BadgesController.cs:87-221`;
  таблицы `Achievements` (уникальный Code), `User_Achievements` (уникальность user+achievement)).
  Начисление — in-process триггеры из ~8 контроллеров (создание проекта, завершение задач, follow,
  отзывы и др.), не событийно-шинное (`memory/domains/badges-and-achievements.md`).
- **FR-MET-1. Метаданные-справочники.** Анонимные списки категорий навыков/бейджей, статусов,
  сложностей и видимостей проектов из БД (`MetadataController.cs:34-123`).

### 4.13 core-api: уведомления

- **FR-NTF-1. In-app inbox.** Пагинированный список, mark-read/mark-all-read, unread-count, удаление
  одного/всех прочитанных (`NotificationsController.cs:72-209`; таблица `Notifications`).
- **FR-NTF-2. Push через SignalR.** Создание уведомления (admin/curator) с мгновенной доставкой
  в группу `user:{userId}` хаба `/notificationHub` (`NotificationsController.cs:157`,
  `Hubs/NotificationHub.cs:119-133`).
- **FR-NTF-3. Системные уведомления.** In-app уведомления создаются напрямую контроллерами при
  блокировке/смене имени/резолве тикетов/отмене проекта/broadcast и др. (`AdminController.cs:163`,
  `ProjectLifecycleController.cs:186`, `SuperAdminController.cs:669`).

### 4.14 core-api: модерация, комьюнити, поддержка, админ

- **FR-MOD-1. Жалобы.** POST `/api/moderation/report` на цели `user/project/message/news_post/
news_comment/showcase_comment/community_post/task/image` (`ProjectDomainConstants.cs:64-83`);
  очередь pending и решения — admin/curator по БД (`ModerationController.cs:37-68`;
  таблица `ModerationReports`).
- **FR-MOD-2. Профанити-фильтрация на записи.** Фильтры `ProfanityFilter`/`ProfanityCensorFilter`
  на создании/правке проектов, новостей, комментариев, отзывов, витрин, задач, тикетов, фидбека,
  групповых чатов (список контроллеров — `SRS_MAP.md` §3).
- **FR-CMN-1. Community-фидбек.** Создание (профанити+XSS-санитизация), публичный список/деталь,
  голосование up/down с repeatable-read транзакциями, комментарии (правка автором ≤1 ч), статус/
  приоритет/назначение — admin/curator, правка автором открытых, удаление автором или admin/curator
  (`CommunityController.cs:139-564`; таблицы `FeedbackItems`, `FeedbackVotes` (уникальность
  feedback+user), `FeedbackComments`). Статусы `open/under_review/planned/in_progress/completed/
rejected/duplicate` (`ProjectDomainConstants.cs:89-104`).
- **FR-SUP-1. Тикеты поддержки (пользователь).** Создание (нотификация админам), свои тикеты,
  деталь с сообщениями (internal-сообщения видны только админам), переписка со сменой статуса,
  close/reopen автором, история изменений (`SupportController.cs:186-656`; таблицы `SupportTickets`,
  `TicketMessages`, `TicketHistories`). Статусы `open/in_progress/waiting_user/resolved/closed`
  (`Models/SupportTicket.cs:30-32`).
- **FR-SUP-2. Тикеты (админ).** Список/фильтры, assign/reassign, resolve с сообщением, приоритет,
  эскалация в urgent с оповещением админов, статистика (`AdminSupportController.cs:184-573`).
- **FR-ADM-1. Управление пользователями.** block/unblock (с уведомлением и аудитом), verify,
  suspend/unsuspend до даты, смена роли (admin only; superadmin-роль недоступна —
  `AdminController.cs:255-267`), смена username (PUT `/users/{userId}/change-name`) и профиля
  (PUT `/users/{userId}/edit-profile`) с уведомлением, заметки админов (`AdminNotes`),
  bulk-операции ≤100 (POST `/users/bulk-action`), деталь+активность пользователя
  (`AdminController.cs:163-1128`).
- **FR-ADM-2. Управление контентом.** Списки/статистика/правка/удаление проектов (каскад по
  activity/news/chat), новостей, комментариев (showcase+news), витрин; действия над проектом
  hide/archive/feature/unfeature; дашборды stats/extended (`AdminContentController.cs:60-473`,
  `AdminController.cs:286,388,1158`).
- **FR-SA-1. Операции суперадмина.** Hard-delete пользователя/проекта, promote/demote админов
  (демоушен в participant/curator/company), просмотр аудит-лога, system-info, список админов —
  всё под `[IpWhitelist]` + подтверждение пароля BCrypt для разрушающих операций
  (`SuperAdminController.cs:178-422`; таблица `AuditLogs`, критическая severity).
- **FR-SA-2. Платформенные настройки и фиче-флаги.** GET/PUT settings (`PlatformSettings`),
  GET/PUT feature-flags (`FeatureFlags`, кэш 30 с, неизвестный флаг = включён —
  `memory/cross-cutting/feature-flags.md`), maintenance mode (503 всем, кроме admin/superadmin;
  пропускаются /health, /metrics, /api/csrf-token, хабы — `MaintenanceModeMiddleware.cs:26-102`),
  IP-whitelist CRUD (GET/POST/DELETE `/api/superadmin/ip-whitelist`), broadcast-уведомление всем
  активным (POST `/api/superadmin/broadcast-notification`) — `SuperAdminController.cs:438-717`;
  клиентский GET `/api/feature-flags` (JWT) — `Program.cs:439-445`.
- **FR-AUD-1. Аудит.** Все административные и чувствительные действия пишутся `IAuditService`
  в `AuditLogs` (action, entityType/Id, severity, IP) — например `AdminController.cs`,
  `SuperAdminController.cs`, `IntegrationsController.cs`, `UserApiKeysController.cs`.

### 4.15 core-api: событийная шина (outbox)

- **FR-EVT-1. Транзакционный outbox.** Доменные события пишутся в `OutboxEvents` в одной транзакции
  с бизнес-данными (`Services/OutboxEventBusDecorator.cs:28-35`), фоновой воркер публикует в RabbitMQ
  topic-exchange `devhunt.events` с опросом каждые 5 с и retry-полями
  (`Services/OutboxEventProcessorWorker.cs:29-97`, `Services/EventBusService.cs:158-206`);
  DLX/DLQ `devhunt.events.dlx/.dlq` (`EventBusService.cs:141-145`). Routing key = EventType.
- **FR-EVT-2. Каталог событий.** `activity.record.created`, `project.created/updated/archived/
unarchived/deleted/published/unpublished/activated/completed/cancelled`,
  `project.ownership.transferred`, `showcase.submitted/retracted/published/unpublished/featured/
unfeatured/liked/unliked`, `showcase.comment.*`, `profile.updated`, `avatar.uploaded/deleted`,
  `user.verified`, `team.member.joined/removed/left/role.updated`, `invitation.sent/accepted/
declined/cancelled`, `message.sent`, `conversation.created` (`EventBusService.cs:254-600`).
  **[not implemented]**: фабрики `task.created/updated/completed` и `showcase.published` объявлены,
  но контроллеры их не публикуют (Tasks пишет только activity-лог; Showcase публикует
  `showcase.submitted` — `ShowcaseController.cs:224`), поэтому байндинги consumer'ов на эти ключи
  никогда не срабатывают.

### 4.16 ml-service

- **FR-ML-1. Генерация рекомендаций.** POST `/api/recommendations/generate`: скоринг кандидатов
  по навыкам пользователя против tech stack/ролей проектов, чтение из PostgreSQL напрямую (asyncpg,
  таблицы `Users/UserSkills/Skills/Projects/ProjectTechStack/ProjectRole/TeamMembers/Reviews` —
  `routers/recommendations.py:154-197`), кэш ответа в Redis на 1 ч
  (`routers/recommendations.py:342-371`); GET `/api/recommendations/user/{user_id}`;
  POST `/api/recommendations/refresh` — bulk 202 (админ);
  DELETE `/api/recommendations/cache/{user_id}` (владелец/admin/curator) — **вызывающих в core-api
  нет** (обнаружено при сверке: `routers/recommendations.py:507`, вызовов из .NET-кода не найдено).
- **FR-ML-2. LLM-генерация.** Список моделей GET `/api/ai/models`; POST `generate-tech-stack`/
  `generate-plan`/`refine-plan`/`refine-tech-stack`/`generate-diagram` (промпты и JSON-схемы в
  `ml-service/prompts/*`), `chat`, агентный `chat-agent` с 17 инструментами (`tools/definitions.py`)
  и их метаданными GET `/api/ai/tools`, `generate-passport` (паспорт проекта), legacy
  `tech-stack`/`roadmap`/`tasks` (`routers/ai.py:66-233`, `routers/passport.py:190`). Провайдеры: Groq — основной
  (модели `openai/gpt-oss-120b`, `llama-3.1-8b-instant` с fallback-цепочкой —
  `clients/groq_client.py:25-45`), Gemini — fallback (`services/ai_service.py:68-100`).
  Кэш AI-ответов в Redis 10 мин по хэшу промпта (`services/ai_cache.py:17-79`).
- **FR-ML-3. Эмбеддинги.** POST `/api/embeddings/generate` (batch) и `/api/embeddings/query`:
  fastembed `BAAI/bge-small-en-v1.5`,
  384 измерения; роутер опционален — пропускается, если fastembed не импортируется
  (`routers/embeddings.py:27-97`, `main.py:59-64`).
- **FR-ML-4. Консюмер событий.** Очередь `devhunt.ml.recommendations`, ключи `profile.updated`,
  `project.completed`, `project.created`, `showcase.published` (`consumers/event_consumer.py:69-74`);
  обновление рейтингов участников по завершении проекта. Известный дефект: SQL консюмера использует
  lowercase-имена таблиц (`users`, `projects`), не совпадающие с реальной схемой EF PascalCase —
  обработчики фактически неработоспособны против текущей БД (`consumers/event_consumer.py:151,203`).
- **FR-ML-5. Auth и метрики.** JWT HS256 (issuer `DevHunt.AuthService`) или сервисный
  `ML_SERVICE_TOKEN`; dev-fallback без секрета — anonymous (`security.py:68-86`); Prometheus-метрики
  requests/latency/tokens/fallback/cache (`metrics.py:16-106`), `/metrics` под `X-Metrics-Token`
  (`metrics_auth.py:22-42`); `/health` с проверкой БД (`main.py:275`).

### 4.17 notification-service

- **FR-NOT-1. Отправка внешних уведомлений.** POST `/api/notifications` (JWT): каналы email
  (SendGrid или SMTP — `src/services/emailService.js:11-17`), SMS (Twilio или mock —
  `src/services/smsService.js:8-11`), push (FCM, APNS-заглушка — `src/services/pushService.js:8-9`).
  HTML-шаблоны welcome/projectInvitation/newMessage/achievementUnlocked с экранированием
  (`src/utils/templateRenderer.js:28-74`). Собственной БД нет — состояние inbox принадлежит core-api.
- **FR-NOT-2. Заглушки.** POST `/api/notifications/bulk`, PUT `/api/notifications/:id/read`,
  PUT `/api/notifications/user/:userId/read-all` возвращают 501 (`src/index.js:237-247`) — **[not implemented]**, намеренная заглушка
  (`memory/gotchas/notification-bulk-userid-localhost-placeholder.md`).
- **FR-NOT-3. Консюмер событий.** Очередь `devhunt.notifications`, ключи `project.*`, `showcase.*`,
  `team.*`, `invitation.*`, `message.sent`, `conversation.created`
  (`src/services/rabbitmqConsumer.js:44-51`). **[not implemented]**: обработчики шлют письма на
  placeholder-адрес `${userId}@devhunt.local` вместо реального email (`rabbitmqConsumer.js:161-275`).
  Retry: до 3 повторных публикаций с заголовком `x-retry-count`, затем drop
  (`rabbitmqConsumer.js:19,89-113`).
- **FR-NOT-4. Приём алертов мониторинга.** POST `/api/alerts/webhook` (Bearer
  `ALERT_WEBHOOK_SECRET` + per-IP лимит) — пересылка алертов OpenObserve админам по email
  (`src/index.js:254`).

### 4.18 integration-gateway

- **FR-GW-1. OAuth-прокси GitHub/GitLab.** Построение authorize-URL (POST `/api/oauth/authorize`,
  legacy GET `/api/oauth/:provider/url`) и обмен code→token (POST `/api/oauth/callback`,
  legacy GET `/api/oauth/:provider/callback`) для интеграций проектов
  (`src/routes/oauth.js:20-247`, конфиг `src/config/oauth.js:8-27`).
- **FR-GW-2. Синхронизация репозиториев.** POST `/api/sync`: получение метаданных/issues из
  GitHub/GitLab API, запись статуса синка обратно в core-api (`src/index.js:130`,
  `src/services/syncService.js:96-365`).
- **FR-GW-3. Управление вебхуками.** Регистрация вебхука на GitHub/GitLab со сгенерированным
  32-байтовым секретом (POST `/api/webhooks`), удаление
  (DELETE `/api/webhooks/:integrationId/:serviceType/:webhookId`), утилита проверки подписи
  POST `/api/webhooks/verify-signature` (`src/index.js:231-387`,
  `src/services/webhookService.js:32-166`, `src/routes/webhooks.js:17`).
- **FR-GW-4. Обработка входящих вебхуков GitHub.** POST `/api/webhooks/github`: `issues` →
  создание/обновление/закрытие/переоткрытие задач DevHunt через internal API core-api; `push` в
  main/master/develop → запуск анализа кода в code-analyzer с постингом результатов в core-api
  (`src/services/webhookHandler.js:33-45,109-227,331-356`). HMAC-проверка `X-Hub-Signature-256`
  (`src/routes/webhooks.js:61`). POST `/api/webhooks/gitlab` проверяет `X-Gitlab-Token`, но события
  не обрабатывает (ack only — `webhooks.js:115-136`) — **[not implemented]**.
- **FR-GW-5. Консюмер событий.** Очередь `devhunt.integrations`, ключи `project.created/updated/
completed`, `task.created/updated/completed`, `showcase.published` (`src/services/
eventConsumer.js:56-64`), concurrency 5; project-события инициируют синк интеграций
  (task/showcase-ключи мертвы — см. FR-EVT-2). Jira/Trello — заглушки (`eventConsumer.js:358-360`).
- **FR-GW-6. Безопасность.** JWT на пользовательских роутах; исходящие вызовы к core-api — HMAC
  internal auth (`src/utils/internalApiAuth.js:67-81`); в production обязательны
  `GITHUB_WEBHOOK_SECRET`, `GITLAB_WEBHOOK_SECRET`, `JWT_SECRET` ≥32 симв., `INTERNAL_API_KEY`
  (`src/index.js:34-60`).

### 4.19 code-analyzer (DevHunt.Analyzer)

- **FR-ANL-1. Анализ репозитория.** POST `/analyze` (Bearer `ANALYZER_API_SECRET`; 503 если секрет
  не задан и auth не отключён): клонирование репо (лимит 200 МБ, таймаут клона 120 с —
  `docker-compose.yml:625`), анализ tree-sitter-парсерами 11+ языков, ~481 собственное правило
  в 18 модулях (SEC-_, CS-_, TS-_, PY-_ и т.д. — `devhunt_analyzer/rules/*.py`) + Semgrep OSS
  (`engine/semgrep_runner.py`), категории security/performance/quality/maintainability/reliability,
  severity critical→info (`engine/models.py:8-28`); `api.py:106-210`.
- **FR-ANL-2. Каталог правил.** GET `/rules` (`api.py:101`); GET `/health` (`api.py:99`);
  CLI `devhunt-analyze` (`devhunt_analyzer/cli.py`).

### 4.20 frontend

- **FR-FE-1. Маршрутизация и i18n.** App Router с локалями `pl` (default) и `en`
  (`src/i18n/routing.ts:10-13`), переводы в `frontend/messages/*.json`; полный список из 40 страниц —
  `SRS_MAP.md` §8.
- **FR-FE-2. Защита маршрутов.** NextAuth v5 middleware: `/dashboard/**` и `/admin/**` требуют
  сессии, редирект на локализованный `/login` (`src/middleware.ts:14-45`); JWT-сессия 7 дней
  (`src/auth.config.ts:20-23`).
- **FR-FE-3. BFF-прокси.** `/api/proxy-core|auth|ml/[...path]` с allowlist путей и блок-листом
  (internal/metrics/swagger/health — `src/lib/security/proxy-allowlist.ts`), пробросом httpOnly-cookie
  и CSRF-заголовка (`src/lib/api/client.ts:76-88`); axios-клиенты с таймаутом 30 с (`client.ts:53,67`);
  служебные роуты `/api/auth/[...nextauth]` (NextAuth-хендлеры) и `/api/client-error`
  (beacon клиентских ошибок, 204 — `src/app/api/client-error/route.ts`).
- **FR-FE-4. Реал-тайм клиент.** SignalR к `/chatHub` (сообщения, typing, реакции, AI-стрим) и
  `/notificationHub`; токен через `/api/realtime/token`; реконнект 0/2/10/30 с
  (`src/lib/signalr.ts:117-131`, `src/lib/realtime/client.ts:97-118`).
- **FR-FE-5. Данные.** TanStack React Query (staleTime 60 с — `src/components/providers.tsx:34-35`),
  40+ модулей запросов в `src/lib/api/queries/`; Zustand только для UI-состояния чат-виджета
  (`src/lib/store/chatStore.ts`).
- **FR-FE-6. Стажировки.** Страницы `/internships` и `/dashboard/internships` рендерят mock-данные
  из кода — **[not implemented]** на бэкенде.
- **FR-FE-7. Community-хаб.** `/community` — лента и suggested users; часть данных mock
  (`src/app/[locale]/(public)/community/page.tsx`).

### 4.21 Вспомогательные компоненты

- **FR-OPS-1. db-migrator.** One-shot джоб: `Database.MigrateAsync()` со стартовым retry до 10 попыток
  с экспоненциальным бэкоффом; выступает gate для старта auth/core (depends_on) —
  (`DevHunt.DatabaseMigrator/Program.cs:59-94`, `docker-compose.yml`).
- **FR-OPS-2. database-seeder.** Не задеплоен (нет в compose/slnx —
  `memory/unverified/orphaned-projects.md`); идемпотентно сидирует demo-данные: e2e-пользователя,
  12 аккаунтов (1 admin, 1 curator, 10 participants), ~200 навыков, ~40 алиасов, 56 достижений,
  6 проектов, колонки, команды, приглашения, ~30 задач, новости, отзывы, тикеты, фидбек, жалобы
  (`DevHunt.DatabaseSeeder/Program.cs:421-502`). Используется в e2e-compose
  (`docker-compose.e2e.yml`).
- **FR-OPS-3. backup-service.** Ежедневный `pg_dump` с ретенцией 7 дней (inline command в
  `docker-compose.yml`).
- **FR-OPS-4. packages/throttle.** Общая backpressure-библиотека: лимит одновременных запросов,
  очередь, отказ 503 при CPU/памяти выше порога, таймаут очереди 30 с
  (`packages/throttle/index.js:148-245`); подключена в notification-service и integration-gateway.
- **FR-OPS-5. api-gateway (nginx).** TLS 1.2/1.3, HTTP→HTTPS редирект, upstream'ы
  frontend/auth-service/core-api/documentation, проксирование SignalR с read-timeout 86400 с
  (`nginx/nginx.conf:48-144`), edge rate-limit: auth 30 req/мин, public API 120 req/мин на IP
  (`nginx/rate-limit-zones.conf:2-5`).

---

## 5. Нефункциональные требования

### 5.1 Производительность

- **NFR-1. Кэширование.** Профиль — 10 мин (`ProfileController.cs:145`), деталь проекта — 15 мин
  (`ProjectsController.cs:366`), рекомендации в Redis — 1 ч (`ml-service/routers/recommendations.py:342`),
  AI-ответы — 10 мин (`ml-service/services/ai_cache.py:17-18`); `ICacheService` поверх Redis либо
  in-memory; `RemoveByPattern` — no-op без Redis (`memory/cross-cutting/caching.md`).
- **NFR-2. Пулы соединений БД.** core-api: Min 5 / Max 100; auth: Min 2 / Max 50; Timeout 30 c,
  Command Timeout 30 с (`DevHunt.CoreApi/appsettings.json:14-17`,
  `DevHunt.AuthService/appsettings.json:9-11`).
- **NFR-3. Лимиты запросов/полезной нагрузки.** Multipart до 10 МБ, поле до 4 МБ, ≤1000 полей
  (`DevHunt.CoreApi/Program.cs:284-290`); Content-Length gate 10 МБ
  (`Middleware/FileUploadValidationMiddleware.cs:32`); SignalR-сообщение ≤8 КБ (`Program.cs:121`);
  доменные лимиты: аватар 5 МБ, файл проекта 100 МБ, вложение задачи 50 МБ, сообщение чата
  10 000 символов; пагинация клэмпится (pageSize ≤100).
- **NFR-4. Индексирование.** Целевые индексы производительности (feed, projects, tokens) и частичные
  уникальные индексы (`Migrations/20250112130000_Projects_AddPerformanceIndexes.cs`,
  `20251125003000_OptimizeFeedIndexes.cs`); IVFFlat для pgvector (`20260310180000`).
- **NFR-5. Ответное сжатие.** `UseResponseCompression` (`DevHunt.CoreApi/Program.cs:381`).
- **NFR-6. Backpressure Node-сервисов.** max concurrent 50, очередь 100, отказ при перегрузке CPU/RAM
  (`notification-service/src/middleware/throttle.js:1-14`, `packages/throttle/index.js:164-213`).

### 5.2 Безопасность

- **NFR-7. Аутентификация.** JWT HMAC-SHA256, issuer `DevHunt.AuthService`, audience `DevHunt.CoreApi`,
  access 30 мин, refresh 7 дней с ротацией и reuse-детекцией (§4.1); в production ключ ≥32 символов
  с отбраковкой слабых паттернов (`DevHunt.AuthService/Program.cs:163-178`). Один JWT-секрет
  валидируют core-api (`Extensions/AuthenticationExtensions.cs:21-76`), ml-service
  (`security.py:68-86`), notification-service (`src/middleware/auth.js:39-48`), integration-gateway.
- **NFR-8. Шифрование данных.** AES-256-GCM для BYOK-ключей и токенов интеграций
  (`Security/EncryptionService.cs:88-120`; ключ `ENCRYPTION_KEY` 32 байта + IV 16 байт); BCrypt для
  паролей и recovery-кодов; HMAC-SHA256-хэши refresh-токенов. Один Encryption:Key на 5 доменов —
  ротация ломает все сразу (`memory/gotchas/encryption-key-rotation-cross-domain.md`).
- **NFR-9. Rate limiting (многоуровневый).** Nginx edge (30/120 req/мин), AspNetCoreRateLimit по IP
  в auth (§4.1 FR-AUTH-11) и core-api (POST projects/users/profile — 10/мин,
  `appsettings.json:103-130`), Redis-backed лимиты Node-сервисов (100 req/мин,
  `notification-service/src/middleware/rateLimit.js:5-12`), AI 30 req/мин/user. Без Redis счётчики
  per-instance — эффективный лимит умножается на число реплик
  (`memory/gotchas/rate-limit-counters-per-instance-without-redis.md`).
- **NFR-10. CSRF.** Двухсервисная схема: auth-service `CSRF-TOKEN` SameSite=Strict, core-api
  `CSRF-TOKEN` SameSite=Lax + читаемый `XSRF-REQUEST-TOKEN` (2 ч) с глобальными фильтрами и
  списками исключений (`DevHunt.CoreApi/Middleware/CsrfTokenMiddleware.cs:59-67`,
  `Security/ValidateCsrfAttribute.cs:91-101`).
- **NFR-11. Заголовки безопасности.** nosniff, DENY, HSTS 1 год + preload, CSP default-src 'self',
  Referrer-Policy, Permissions-Policy (`Middleware/SecurityHeadersMiddleware.cs:43-62`); TLS-конфиг
  и HSTS на nginx (`nginx/nginx.conf:48-69`).
- **NFR-12. CORS.** Явные origin'ы из конфигурации; в production ограниченные методы/заголовки,
  preflight-кэш 10 мин (`DevHunt.CoreApi/Program.cs:80-108`).
- **NFR-13. Валидация ввода и санитизация.** Профанити-фильтры на записи, HTML/XSS-санитизация
  (новости, отзывы, документы, импорт из GitHub), SSRF-валидация URL интеграций, блокировка опасных
  расширений файлов, валидация URL витрины.
- **NFR-14. Внутренняя аутентификация сервисов.** HMAC-подписи с таймстемпом для internal-эндпоинтов
  (`Security/InternalServiceAuthenticator.cs`, `integration-gateway/src/utils/internalApiAuth.js:67-81`);
  секреты — только из env (`docker-compose.prod.yml` требует `${VAR:?}`).
- **NFR-15. Защита привилегий.** superadmin: IP-whitelist + password-confirm; admin-роль проверяется
  по БД; maintenance-mode; аудит критических действий.
- **NFR-16. Приватность.** Privacy-настройки видимости профиля/активности применяются на всех
  публичных чтениях (`ProfileController.cs:193`, `UsersController.cs:165,494,535`).

### 5.3 Надёжность

- **NFR-17. Устойчивость исходящих HTTP.** Polly: retry 2 раза с экспоненциальным бэкоффом 2^n c,
  circuit breaker 3 сбоя → пауза 30 с; таймаут клиентов ML/gateway/notification — 30 с
  (`Extensions/InfrastructureExtensions.cs:100-150`).
- **NFR-18. Гарантии доставки событий.** Транзакционный outbox + DLX/DLQ (§4.15); ретраи консюмеров
  до 3 с republish. Известные пробелы: `Processing`-статус outbox не пишется — возможен double-publish
  при >1 воркере (`memory/gotchas/outbox-double-publish-on-scale-out.md`); при недоступном RabbitMQ
  события тихо теряются (NoOp-шина — `memory/gotchas/events-silently-dropped-without-rabbitmq.md`).
- **NFR-19. Здоровье и старт.** `/health` во всех сервисах, compose-healthchecks с retries;
  db-migrator с 10 ретраями как gate; Redis connect 5000 мс / 3 ретрая
  (`InfrastructureExtensions.cs:51-53`).
- **NFR-20. Транзакционность.** Serializable для создания задач и ротации refresh-токенов,
  RepeatableRead для голосований и удаления колонок; concurrency-token на статусе проекта;
  атомарные инкременты счётчиков (views/likes).
- **NFR-21. Бэкапы.** Ежедневный pg_dump, ретенция 7 дней (`docker-compose.yml`, backup-service).
- **NFR-22. Масштабирование.** K8s: по 2 реплики auth/core-api/frontend
  (`k8s/*-deployment.yaml:10`); SignalR Redis backplane (префикс `DevHunt:SignalR` —
  `InfrastructureExtensions.cs:78-83`). Известные single-instance допущения: in-process
  `IAiInFlightRegistry` (cancel no-op при scale-out), локальные кэши фиче-флагов/maintenance
  (лаг до 30 с) — `memory/cross-cutting/scale-out-readiness.md`.

### 5.4 Observability

- **NFR-23. Трейсинг.** OpenTelemetry (ASP.NET Core + HttpClient) с OTLP-экспортом в OpenObserve
  (`Extensions/OpenTelemetryExtensions.cs:18-58`, `DevHunt.AuthService/Program.cs:69-113`);
  OTEL-эндпоинты для ml/notification/gateway из compose (`docker-compose.yml:284,333,382`).
- **NFR-24. Метрики.** `/metrics` Prometheus в auth, core-api, ml-service, notification-service,
  integration-gateway; доступ по `X-Metrics-Token` или loopback
  (`DevHunt.Infrastructure/Security/MetricsAuthorization.cs:10-29`); AI-специфичные метрики
  (токены, fallback, latency — `ml-service/metrics.py:53-106`); scrape-конфиги
  `monitoring/openobserve/scrape.yml`, `monitoring/prometheus.yml`.
- **NFR-25. Логи.** Serilog структурированные логи → OpenObserve `_json`; корреляция через
  `UseCorrelationId` + `LogEnrichmentMiddleware` (`DevHunt.CoreApi/Program.cs:394-396`).
  Известный дефект: LogEnrichment стоит до Authentication — UserId в логах всегда anonymous
  (`memory/cross-cutting/observability.md`).
- **NFR-26. Алертинг.** OpenObserve-алерты сидируются `openobserve-init`
  (`monitoring/openobserve/init-alerts.sh`) и доставляются админам через notification-service
  (FR-NOT-4); ретенция данных 30 дней (`docker-compose.yml:504-505`).

---

## 6. Tech stack (точные версии)

### 6.1 .NET-сервисы (auth-service, core-api, infrastructure, migrator)

TargetFramework `net10.0` (`Directory.Build.props:6`). Версии из `Directory.Packages.props`:
Microsoft.AspNetCore.Authentication.JwtBearer / SignalR.StackExchangeRedis 10.0.0;
EF Core + Npgsql.EntityFrameworkCore.PostgreSQL 9.0.1; Serilog.AspNetCore 9.0.0;
OpenTelemetry 1.15.1–1.15.3; Polly 8.4.2; AspNetCoreRateLimit 5.0.0; Swashbuckle 7.2.0;
RabbitMQ.Client 6.8.1; AWSSDK.S3 3.7.401; prometheus-net.AspNetCore 8.2.1; xunit 2.9.2.

### 6.2 frontend (из `frontend/package-lock.json`, resolved)

next 16.2.7; react / react-dom 19.2.0; typescript 5.9.3; next-auth 5.0.0-beta.30; next-intl 4.13.0;
@tanstack/react-query 5.90.10; @microsoft/signalr 9.0.6; zod ^4.1.12; zustand ^5.0.8; axios ^1.13.2.

### 6.3 ml-service (`ml-service/requirements.txt`, pinned)

Python 3.11 (`ml-service/Dockerfile:1`); fastapi 0.115.0; uvicorn 0.38.0; asyncpg 0.30.0;
pydantic 2.9.2; httpx 0.27.0; PyJWT 2.10.1; aio-pika 9.4.1; redis 7.1.0; fastembed 0.4.2;
google-generativeai 0.8.3; prometheus-client 0.20.0; opentelemetry 1.27.0/0.48b0.

### 6.4 Node-сервисы (resolved из package-lock.json)

Node ≥20 (`node:20-alpine`); express 5.1.0; axios 1.17.0; amqplib ^0.10.7/^0.10.9;
jsonwebtoken ^9.0.2; prom-client ^15.1.3; ioredis ^5.4.2; @sendgrid/mail ^8.1.3;
nodemailer ^8.0.10; twilio ^5.10.6; passport-github2 ^0.1.12.

### 6.5 Инфраструктура (docker-compose.yml / Dockerfiles)

PostgreSQL `pgvector/pgvector:pg16` (+ extension в `infrastructure/db-init/01-extensions.sql`);
Redis `redis:7-alpine`; RabbitMQ `rabbitmq:3-management-alpine`; SeaweedFS `chrislusf/seaweedfs:latest`;
OpenObserve `public.ecr.aws/zinclabs/openobserve:latest`; nginx `nginx:alpine`;
.NET `mcr.microsoft.com/dotnet/sdk:10.0`/`aspnet:10.0`; code-analyzer `python:3.13-slim`;
бэкапы `postgres:16-alpine`.

---

## 7. Use cases

### UC-1. Регистрация с верификацией email

- **Актёр:** анонимный посетитель.
- **Precondition:** email не занят.
- **Main flow:** POST `/api/auth/register` (валидация пароля — FR-AUTH-1) → письмо с 6-значным кодом →
  POST `/api/auth/verify-email` → POST `/api/auth/login` → выданы JWT (30 мин) + refresh (7 дней)
  в httpOnly-cookies.
- **Alternative flows:** (a) код истёк (24 ч) → `resend-verification` (лимит 1/мин, 3/ч);
  (b) логин до верификации → отказ (`LoginService.cs`); (c) dev без SMTP → автоверификация;
  (d) превышение rate limit → 429.
- **Postcondition:** строка в `Users` (Role=participant, IsEmailVerified=true), строка в `RefreshTokens`.

### UC-2. Вход через OAuth GitHub

- **Актёр:** посетитель с GitHub-аккаунтом.
- **Precondition:** настроены `Authentication:GitHub:ClientId/Secret`.
- **Main flow:** GET `/api/auth/login/github` → редирект на GitHub с HMAC-state → callback →
  создание/линковка пользователя по `GithubId` → редирект на фронтенд с one-time кодом (2 мин) →
  POST `/api/auth/oauth/exchange` → токены.
- **Alternative flows:** (a) state просрочен/подделан → отказ (`OAuthStateValidator.cs:86-87`);
  (b) redirect_uri вне allowlist → отказ; (c) exchange-код использован повторно → отказ.
- **Postcondition:** аутентифицированная сессия; `Users.GithubId` заполнен.

### UC-3. Включение 2FA (TOTP)

- **Актёр:** участник (аутентифицирован).
- **Precondition:** 2FA выключен.
- **Main flow:** POST `/totp/setup` → секрет + QR → пользователь сканирует в authenticator →
  POST `/totp/verify-setup` с кодом → 2FA включён, выданы 8 recovery-кодов.
- **Alternative flows:** (a) неверный код (вне ±1 шага 30-с окна) → отказ; (b) вход с recovery-кодом —
  код одноразово сгорает (`TotpService.cs:101-116`); (c) отключение — пароль + опц. код.
- **Postcondition:** `Users.IsTotpEnabled=true`, `TotpSecret` и BCrypt-хэши recovery-кодов сохранены.

### UC-4. Создание и запуск проекта

- **Актёр:** участник.
- **Precondition:** у пользователя <20 проектов.
- **Main flow:** POST `/api/projects` (status=draft, автосоздание группового чата) → настройка
  tech stack/ролей → POST `/{id}/publish` (→recruiting) → приглашения → accept'ы →
  POST `/{id}/activate` (≥2 активных участников) → работа.
- **Alternative flows:** (a) <2 участников при activate → 400 (`ProjectLifecycleController.cs:132`);
  (b) недопустимый переход статуса → отказ по матрице (`ProjectsController.cs:601`);
  (c) конкурентная смена статуса → конфликт по concurrency-токену; (d) профанити в тексте → отказ фильтра.
- **Postcondition:** `Projects.Status=active`; события `project.created/published/activated` в шине;
  запись владельца в `TeamMembers`; `Conversations` (project chat).

### UC-5. Вступление в проект по заявке

- **Актёр:** участник (кандидат); владелец проекта.
- **Precondition:** проект в recruiting, публичный; кандидат не в команде.
- **Main flow:** кандидат POST `/api/invitations/send` (type=request) → владелец видит в incoming →
  POST `/api/invitations/respond` (accept) → создаётся `TeamMembers` (active) + DM с системным
  сообщением; кэши инвалидируются.
- **Alternative flows:** (a) decline → статус declined, событие `invitation.declined`;
  (b) отзыв заявки отправителем DELETE `/{invitationId}`; (c) дубликат активного membership —
  отклоняется частичным уникальным индексом.
- **Postcondition:** кандидат — активный участник; события `invitation.accepted`,
  `team.member.joined`.

### UC-6. Работа с задачей, синхронизированной с GitHub

- **Актёр:** участник команды; внешняя система GitHub.
- **Precondition:** интеграция GitHub активна, вебхук зарегистрирован (FR-GW-3).
- **Main flow:** в GitHub открывается issue → вебхук `issues` → gateway проверяет HMAC → POST
  internal `/api/internal/github-tasks/projects/{id}/tasks` → задача появляется на kanban →
  issue закрывается на GitHub → PATCH `.../complete` → задача done.
- **Alternative flows:** (a) label-изменения → синк priority/tags; (b) reopen → PATCH `.../reopen`;
  (c) невалидная подпись вебхука → 401; (d) push в main → запуск code-analyzer → результаты
  сохраняются POST `/api/internal/code-analysis/results` → участники смотрят issues и semantic-search.
- **Postcondition:** `Tasks` синхронизирована с GitHub Issue (`GitHubIssueId` заполнен);
  `CodeAnalysisResults` пополнены (при push).

### UC-7. AI-генерация плана проекта

- **Актёр:** участник команды.
- **Precondition:** фиче-флаг `ai_features` включён; настроен LLM-провайдер (BYOK или платформенный).
- **Main flow:** POST `/api/projects/{id}/ai/plans` → core-api вызывает ml-service
  `/api/ai/generate-plan` (Groq; при недоступности — Gemini) → план сохраняется в `AiPlans`
  (jsonb) → пользователь уточняет POST `/refine` → POST `/{planId}/apply` — задачи/структура
  применяются к проекту.
- **Alternative flows:** (a) флаг выключен → 503 (`RequireFeatureFlagFilter.cs`); (b) превышен
  лимит 30 req/мин → 429 + Retry-After; (c) повторный идентичный промпт → ответ из Redis-кэша
  (10 мин); (d) отмена стрима POST `.../cancel/{requestId}` (работает только в пределах одного
  инстанса — `memory/gotchas/ai-plan-cancel-process-local.md`).
- **Postcondition:** `AiPlans` (+ `AiOperationLogs` с токенами/моделью); при apply — задачи проекта.

### UC-8. Публикация в витрину и лайк

- **Актёр:** владелец проекта; любой участник; анонимный читатель.
- **Precondition:** проект завершён (по флоу; код требует владения проектом).
- **Main flow:** владелец POST `/api/projects/{id}/showcase` (summary, скриншоты, demo/repo URL) →
  запись в `Showcase_Projects`, событие `showcase.submitted` → куратор может feature →
  посетители смотрят GET `/api/showcase` (просмотры атомарно инкрементятся), участники лайкают
  POST `.../like` и комментируют.
- **Alternative flows:** (a) невалидный demo-URL → отказ; (b) повторный лайк → отказ (PK Showcase+User);
  (c) удаление витрины owner/admin.
- **Postcondition:** проект в публичной галерее; `ShowcaseLikes`, `Showcase_Comments` пополняются.

### UC-9. Жалоба и модерация

- **Актёр:** участник (репортер); куратор/админ.
- **Precondition:** существует объект жалобы (user/project/message/…).
- **Main flow:** POST `/api/moderation/report` → запись в `ModerationReports` (pending) →
  куратор GET `/api/moderation/queue` → POST `/api/moderation/decision` → статус resolved/dismissed,
  фиксация `ProcessedByUserId`.
- **Alternative flows:** (a) не-админ запрашивает очередь → 403 (проверка роли по БД);
  (b) решение о блокировке → POST `/api/admin/users/block` с уведомлением и аудитом.
- **Postcondition:** жалоба закрыта; действия зафиксированы в `AuditLogs`.

### UC-10. Тикет поддержки до резолва

- **Актёр:** участник; админ/куратор.
- **Main flow:** POST `/api/support/tickets` (нотификация админам) → админ assign
  (`AdminSupportController.cs:228`) → переписка POST `.../messages` (internal-заметки скрыты
  от автора) → POST resolve с сообщением → пользователь может reopen.
- **Alternative flows:** (a) эскалация → priority urgent + оповещение всех админов; (b) закрытие
  автором PUT `/close`; (c) все изменения — в `TicketHistories`.
- **Postcondition:** тикет resolved/closed; история изменений полна.

### UC-11. Maintenance mode

- **Актёр:** суперадмин.
- **Precondition:** запрос с whitelisted IP; пароль подтверждён.
- **Main flow:** POST `/api/superadmin/maintenance` (enable) → `PlatformSettings['maintenance_mode']='true'`
  → все запросы не-админов получают 503 (кэш проверки 30 с).
- **Alternative flows:** (a) health/metrics/csrf-token/хабы продолжают работать;
  (b) admin/superadmin работают в обход; (c) при >1 реплике выключение доходит с лагом до 30 с
  [known issue].
- **Postcondition:** платформа в режиме обслуживания; действие в `AuditLogs` (critical).

### UC-12. Получение ML-рекомендаций

- **Актёр:** участник.
- **Precondition:** заполнены навыки; существуют открытые проекты.
- **Main flow:** GET `/api/recommendations/me?refresh=true` → core-api вызывает ml-service →
  скоринг по пересечению навыков/ролей/рейтингов → запись в `Recommendations` → выдача с
  ReasoningJson; пользователь отмечает viewed/actioned или удаляет.
- **Alternative flows:** (a) повторный refresh <5 мин → отдаётся кэш; (b) ml-service недоступен →
  circuit breaker, отдаются сохранённые рекомендации; (c) admin запускает refresh-all (202, фон).
- **Postcondition:** актуальные строки `Recommendations` с MatchScore.

---

## 8. Data model

Единственная БД PostgreSQL 16 + pgvector; 63 DbSet (`DevHunt.Infrastructure/DevHuntDbContext.cs:42-128`).
Ground truth: designer `Migrations/20260427180340_AddByokAndModelRegistry.Designer.cs` + последующие
миграции (актуальный `DevHuntDbContextModelSnapshot.cs` пуст — известная проблема, §9).
Полный список миграций (56) — `SRS_MAP.md`; сводка связей ниже. PK везде uuid, если не указано иное.

### DM-1. Пользователи и аутентификация

| Таблица             | Ключевые колонки                                                                                                                                                           | Связи                                                            |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| Users               | Email, PasswordHash (BCrypt), Role, Username varchar(50), Skills text[], TotpSecret, TotpRecoveryCodes, GithubId, GoogleId, verification/reset/suspension-поля (`User.cs`) | корневая; на неё ссылаются почти все                             |
| RefreshTokens       | TokenHash (HMAC-SHA256), TokenFamilyId (default gen_random_uuid), ExpiresAt, IsRevoked (`RefreshToken.cs`)                                                                 | FK→Users cascade; IX по UserId, TokenFamilyId (`20260604180000`) |
| UserPrivacySettings | UQ UserId; ProfileVisibility, ActivityVisibility, notif-флаги                                                                                                              | FK→Users cascade                                                 |
| UserApiKeys         | UQ (UserId, Provider); EncryptedKey (AES-256-GCM), KeyHint (`Models/UserApiKey.cs:27-32`)                                                                                  | FK→Users cascade                                                 |
| User_Follows        | составной PK (FollowerId, FollowedId)                                                                                                                                      | FK→Users ×2                                                      |

### DM-2. Проекты и команды

| Таблица               | Ключевые колонки                                                                                                                                                                                     | Связи                                                |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| Projects              | Title varchar(200), частичный UQ Slug varchar(48), Status (concurrency token), Visibility, OwnerId (без FK), TechStack text[], RequiredRoles text[], **OpenRoles jsonb**, BoostsCount (`Project.cs`) | корневая для проектных таблиц                        |
| ProjectBoosts         | PK (ProjectId, UserId)                                                                                                                                                                               | FK→Projects, Users cascade                           |
| TeamMembers           | Role varchar(100), Status, permission-флаги; частичный UQ (ProjectId,UserId) WHERE Status='active' (`TeamMember.cs`, `20260614120000`)                                                               | FK→Projects, Users cascade                           |
| Invitations           | Type (invite/request), Status, Role (`Invitation.cs`)                                                                                                                                                | FK→Projects cascade; Users(Inviter/Invitee) restrict |
| Project_Roles         | RoleName, **RequiredSkillsJson jsonb**, RequiredCount/FilledCount (`ProjectRole.cs`)                                                                                                                 | FK→Projects cascade                                  |
| Project_Tech_Stack    | IsRequired, ProficiencyRequired (`ProjectTechStack.cs`)                                                                                                                                              | FK→Projects, Skills cascade                          |
| Project_Subscriptions | PK (ProjectId, UserId)                                                                                                                                                                               | FK→Projects, Users cascade                           |
| Reviews               | Rating 1–5, ReviewText varchar(2000), ReviewedUserId? (peer) (`Review.cs`)                                                                                                                           | FK→Projects, Users ×2                                |
| Recommendations       | MatchScore decimal(5,4), **ReasoningJson jsonb**, Viewed/Actioned (`Recommendation.cs`)                                                                                                              | FK→Projects, Users cascade                           |

### DM-3. Задачи

| Таблица           | Ключевые колонки                                                                                                                  | Связи                                             |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| Tasks             | Title varchar(200), Status, Priority, ColumnId?, PositionInColumn, Tags varchar(500), GitHub-sync поля, IsDeleted (`TaskItem.cs`) | FK→Projects cascade, Users(assignee), TaskColumns |
| TaskColumns       | Name, Position, canvas-координаты, Color, IsDefault/IsCompleted, WipLimit?                                                        | FK→Projects cascade                               |
| TaskLinks         | UQ (Source,Target,LinkType); LinkType varchar(20) (`Models/TaskLink.cs:55-64`)                                                    | FK→Tasks ×2 cascade, Users restrict               |
| TaskAttachments   | inline-файл или ProjectFileId?, StorageKey                                                                                        | FK→Tasks cascade, Project_Files set-null          |
| TaskBoardSettings | ViewMode, pan/zoom, DefaultColumnId?                                                                                              | FK→Projects, TaskColumns                          |

### DM-4. Чат

| Таблица                   | Ключевые колонки                                                                                                                       | Связи                                                              |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Conversations             | Type enum (Direct/Group/ProjectChannel), ProjectId?, Slug varchar(50) (частичный UQ с ProjectId), IsPrivate (`Models/Conversation.cs`) | FK→Projects set-null                                               |
| Conversation_Participants | UQ (ConversationId, UserId); Role enum, State (Active/Left/Banned), permission-overrides, ban-метаданные                               | FK→Conversations, Users cascade; Channel_Role_Definitions set-null |
| Channel_Role_Definitions  | UQ (ConversationId, Name); дефолтные права                                                                                             | FK→Conversations cascade                                           |
| Messages                  | Content varchar(10000), MessageType, **AiMetadataJson jsonb**, pin/soft-delete/edit-поля, ReplyToId self-ref (`Models/Message.cs`)     | FK→Users(sender) cascade, Conversations, Projects                  |
| AiMessageDetails          | PK=MessageId; **FullPayloadJson jsonb**                                                                                                | FK→Messages cascade                                                |
| MessageReactions          | UQ (MessageId, UserId, Emoji)                                                                                                          | FK→Messages, Users cascade                                         |

### DM-5. Витрина

| Таблица           | Ключевые колонки                                                                                                  | Связи                                 |
| ----------------- | ----------------------------------------------------------------------------------------------------------------- | ------------------------------------- |
| Showcase_Projects | Summary, **ScreenshotsJson jsonb**, **MetricsJson jsonb**, LikesCount/ViewsCount, Featured (`ShowcaseProject.cs`) | FK→Projects                           |
| ShowcaseLikes     | PK (ShowcaseId, UserId) (`ShowcaseLike.cs`, `20260614130000`)                                                     | FK→Showcase_Projects, Users cascade   |
| Showcase_Comments | ParentCommentId? (дерево), soft-delete                                                                            | FK→Showcase_Projects, Users, self-ref |

### DM-6. Уведомления / активность / новости

| Таблица            | Ключевые колонки                                                                                                        | Связи                                            |
| ------------------ | ----------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------ |
| Notifications      | Type, Title varchar(255), Priority, полиморфные RelatedEntityType/Id (`Notification.cs`)                                | без FK (UserId логический)                       |
| Activity_Records   | ActorId, ProjectId?, TargetUserId?, **PayloadJson jsonb**, EventType/EventGroup/Visibility (`Models/ActivityRecord.cs`) | FK→Users(actor) cascade, Projects, Users(target) |
| Project_News_Posts | Title, Content, Visibility, AttachmentsJson text, денормализованные счётчики                                            | FK→Projects, Users cascade                       |
| News_Post_Likes    | UQ (NewsPostId, UserId)                                                                                                 | FK→Project_News_Posts cascade, Users restrict    |
| News_Post_Comments | soft-delete                                                                                                             | FK→Project_News_Posts cascade, Users restrict    |

### DM-7. Навыки

| Таблица           | Ключевые колонки                                     | Связи                                           |
| ----------------- | ---------------------------------------------------- | ----------------------------------------------- |
| Skills            | Name varchar(100), Category varchar(50) (`Skill.cs`) | — (UQ имени сброшен миграцией `20260209201025`) |
| Skill_Aliases     | Alias, AliasNormalized (`SkillAlias.cs`)             | FK→Skills cascade                               |
| User_Skills       | ProficiencyLevel (`UserSkill.cs`)                    | FK→Users, Skills                                |
| User_SkillEntries | Raw/RawNormalized, SkillId? (`UserSkillEntry.cs`)    | FK→Users, Skills                                |

### DM-8. Интеграции / AI / анализ кода

| Таблица                   | Ключевые колонки                                                                                                 | Связи                                                    |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| Integrations              | ServiceType, **ConfigJson jsonb**, AccessTokenEncrypted, IsActive, sync-таймстемпы (`Integration.cs`)            | FK→Projects cascade                                      |
| AiPlans                   | **PlanJson jsonb**, Status, PlanVersion, Idea, TechStack (`Models/AiPlan.cs`)                                    | FK→Projects cascade, Users restrict                      |
| AiOperationLogs           | **MetadataJson jsonb**, токены, провайдер/модель, Status (`Models/AiOperationLog.cs`)                            | FK→Projects cascade, AiPlans set-null, Users restrict    |
| ProjectArtifacts          | UQ (ProjectId, Type); Content, Version (`ProjectArtifact.cs`)                                                    | → Projects                                               |
| CodeAnalysisResults       | **SeverityCountsJson/CategoryCountsJson/IssuesJson jsonb**, Repository, Branch, Status (`CodeAnalysisResult.cs`) | FK→Projects cascade, Integrations                        |
| CodeAnalysisEmbeddings    | RuleId, **TopFilesJson jsonb**, **Embedding vector(384)** (только в SQL, не в EF-модели — `20260310180000`)      | FK→CodeAnalysisResults, Projects cascade; IVFFlat-индекс |
| CodeAnalysisEmbeddingJobs | UQ AnalysisResultId; Status, retry-поля (`Models/CodeAnalysisEmbeddingJob.cs`)                                   | FK→CodeAnalysisResults cascade                           |
| LlmModels                 | UQ (Provider, ModelId); цены, capabilities, Tier (`Models/LlmModel.cs`)                                          | —                                                        |
| UserLlmModels             | UQ (UserId, Provider, ModelId) (`Models/UserLlmModel.cs`)                                                        | FK→Users cascade                                         |

### DM-9. Модерация / поддержка / админ

| Таблица           | Ключевые колонки                                                                      | Связи                                     |
| ----------------- | ------------------------------------------------------------------------------------- | ----------------------------------------- |
| ModerationReports | TargetType/TargetId (полиморфные), Status, ProcessedByUserId? (`ModerationReport.cs`) | FK→Users(reporter) cascade                |
| ProjectIssues     | Type/Status/Priority, admin-назначение/резолюция (`Models/ProjectIssue.cs`)           | FK→Projects restrict, Users               |
| SupportTickets    | Category/Status/Priority (`Models/SupportTicket.cs`)                                  | FK→Users restrict                         |
| TicketMessages    | IsInternal                                                                            | FK→SupportTickets cascade, Users restrict |
| TicketHistories   | ChangeType, old/new values                                                            | FK→SupportTickets cascade, Users restrict |
| FeedbackItems     | Type/Status/Priority, счётчики голосов (`Models/FeedbackItem.cs`)                     | FK→Users restrict                         |
| FeedbackVotes     | UQ (FeedbackId, UserId); IsUpvote                                                     | FK→FeedbackItems cascade, Users restrict  |
| FeedbackComments  | —                                                                                     | FK→FeedbackItems cascade, Users restrict  |
| AdminNotes        | Content varchar(2000) (`Models/AdminNote.cs`)                                         | FK→Users ×2 cascade                       |
| PlatformSettings  | PK Key varchar(128); Value varchar(4000) (`Models/PlatformSetting.cs`)                | —                                         |
| FeatureFlags      | PK Key; Enabled (`Models/FeatureFlag.cs`)                                             | —                                         |
| AuditLogs         | Action, EntityType/Id, Severity, IpAddress (`AuditLog.cs`)                            | без FK (UserId nullable)                  |

### DM-10. Достижения / файлы / служебные

| Таблица           | Ключевые колонки                                                                                                           | Связи                      |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------- | -------------------------- |
| Achievements      | UQ Code varchar(50); Title, Category, Points (`Achievement.cs`)                                                            | —                          |
| User_Achievements | UQ (UserId, AchievementId); EarnedAt, Progress? (`UserAchievement.cs`)                                                     | FK→Users, Achievements     |
| Project_Files     | StorageKey varchar(500), Visibility, soft-delete (`Models/ProjectFile.cs`)                                                 | FK→Projects, Users cascade |
| Project_Documents | DocumentType, ContentFormat, Path, soft-delete (`Models/ProjectDocument.cs`)                                               | FK→Projects, Users cascade |
| OutboxEvents      | EventType, Payload text, Status enum (Pending/Processing/Completed/Failed), RetryCount/MaxRetries (`OutboxEvent.cs:24-30`) | —                          |

### DM-11. Ключевые enum'ы/словари

Статусы проекта, участника команды, видимости, жалоб, фидбека —
`Constants/ProjectDomainConstants.cs:6-104`; статусы задач/приоритеты — `TaskItem.cs:9-52`;
типы связей задач — `Models/TaskLink.cs:55-64`; типы/статусы приглашений — `Invitation.cs:10-14`;
статусы тикетов — `Models/SupportTicket.cs:30-32`; статусы project issues —
`Models/ProjectIssue.cs:34-36`; типы документов — `Constants/DocumentTypeConstants.cs:6-34`;
статусы embedding-джобов — `Models/CodeAnalysisEmbeddingJobStatus.cs:8-21`;
enum'ы чата — `Models/Conversation.cs:114-131`, `Models/Message.cs:194-210`,
`Models/ConversationParticipant.cs:191-213`.

---

## 9. Ограничения, известные проблемы, out-of-scope

### 9.1 Ограничения (by design, из кода)

- Один PostgreSQL на всю платформу; ml-service ходит в те же таблицы напрямую через asyncpg
  (`ml-service/routers/recommendations.py:154-197`) — схема связана с EF-моделью.
- Единственный публикатор событий — core-api; остальные сервисы только потребляют (§10.2 SRS_MAP).
- Максимум 20 проектов на пользователя (`ProjectsController.cs:509`); pageSize ≤100;
  файловые лимиты §5.1.
- Swagger доступен только в Development/Staging (`DevHunt.CoreApi/Program.cs:369-378`).
- SMS/push фактически mock без Twilio/FCM-ключей (`smsService.js:59-104`, `pushService.js:50-106`).

### 9.2 Известные проблемы (код + /memory)

1. **Пустой EF model snapshot.** `DevHuntDbContextModelSnapshot.cs` содержит только ProductVersion —
   `dotnet ef` тулинг не отражает актуальную модель до регенерации.
2. **pgvector-колонка вне EF.** `CodeAnalysisEmbeddings.Embedding` существует только в SQL-миграции
   (`20260310180000`), в entity-классе свойства нет.
3. **Outbox double-publish.** Статус `Processing` объявлен, но не пишется; без row-lock две реплики
   воркера могут опубликовать событие дважды (`memory/gotchas/outbox-double-publish-on-scale-out.md`).
4. **Тихая потеря событий без RabbitMQ.** NoOp-шина при невыставленной конфигурации
   (`memory/gotchas/events-silently-dropped-without-rabbitmq.md`).
5. **Мертвые байндинги consumer'ов.** `task.*` и `showcase.published` не публикуются (FR-EVT-2) —
   GitHub-issue-создание по задачам DevHunt и ML-обработка публикаций витрин не срабатывают.
6. **Placeholder-адресаты в notification-service.** Обработчики событий шлют на
   `${userId}@devhunt.local`; реальный lookup email не реализован (намеренная заглушка).
7. **Схемное расхождение ml-consumer.** lowercase-таблицы в SQL консюмера против PascalCase EF —
   обработчики событий фактически не работают против реальной БД (`consumers/event_consumer.py:151`).
8. **ReadOnly-подключение не используется.** `CreateReadContext()` нигде не вызывается; конфиг
   `ReadOnlyConnection` мёртв (`memory/gotchas/readonly-connection-registered-but-unused.md`).
9. **Scale-out допущения.** In-process AI cancel-реестр; локальные кэши feature-флагов и maintenance
   (лаг ≤30 с на реплику); rate-limit per-instance без Redis; SignalR без backplane теряет
   межрепличную доставку (`memory/cross-cutting/scale-out-readiness.md`).
10. **LogEnrichment до Authentication** — UserId в логах всегда anonymous
    (`memory/cross-cutting/observability.md`).
11. **TotpSecret хранится в открытом Base32** несмотря на doc-комментарий «encrypted at rest»
    (`AuthController.cs:706` vs `User.cs:144-146`).
12. **Account lockout отсутствует** — брутфорс сдерживается только IP-rate-limit (нет логики
    блокировки по неудачным попыткам в `LoginService.cs`).
13. **UseForwardedHeaders не сконфигурирован** ни в auth, ни в core-api; IP берётся вручную из
    `X-Real-IP`/`X-Forwarded-For` (`Security/IpWhitelistAttribute.cs:35-38`) — доверие заголовкам
    без валидации прокси.
14. **Дублирующая миграционная история.** `20260225125026` повторно добавляет `Username`;
    `OpenRoles`/`Tags` есть в модели, но не в миграциях (подтверждено `memory/data/migration-timeline.md`
    и AGENTS-заметками по обходу).
15. **mailpit** указан как дефолтный SMTP-хост, но сервис в compose не определён
    (`docker-compose.yml:116-118`).
16. **DELETE `/api/recommendations/cache/{userId}`** в ml-service не имеет вызывающих в core-api.
17. **GitLab-вебхуки принимаются, но не обрабатываются** (`webhooks.js:115-136`).

### 9.3 Out-of-scope (заявлено в коде/моделях, но не реализовано)

- **[not implemented] Стажировки и роль company.** Модель роли есть (`User.cs:11`), фронтовые страницы
  на mock-данных; серверного API стажировок нет.
- **[not implemented] Bulk-уведомления и read-state в notification-service** — 501; владение
  состоянием отдано core-api (`notification-service/src/index.js:237-247`).
- **[not implemented] Jira/Trello-интеграции** — заглушки в eventConsumer
  (`integration-gateway/src/services/eventConsumer.js:358-360`).
- **[not implemented] Telegram/Discord/Slack-каналы уведомлений** — отсутствуют в notification-service.
- **[not implemented] GitLab OAuth-вход** — только GitHub/Google; GitLab существует только как тип
  интеграции проектов (`Services/OAuthService.cs`, `IntegrationsController.cs:123`).
- **[not implemented] APNS-push** — заглушка (`pushService.js:50-106`).
- **[not implemented] issue_comment / pull_request GitHub-вебхуки** — обработчики-стабы
  (`webhookHandler.js:33-45`).
- Биллинг, платные тарифы, email-дайджесты, мобильные приложения — следов в коде нет.
