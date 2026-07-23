# Обзор архитектуры платформы

DevHunt — модульная платформа для хакатонов, showcase-проектов и коллабораций. Архитектура соответствует SRS v1.0 и строится вокруг Core API с набором сервисов-спутников.

## Ключевые компоненты

| Компонент            | Технологии                 | Ответственность                                   |
| -------------------- | -------------------------- | ------------------------------------------------- |
| Core API             | ASP.NET Core 8/10, EF Core | Пользователи, проекты, команды, задания, showcase |
| Auth Service         | ASP.NET Core               | Регистрация/логин, JWT, refresh rotation, OAuth2  |
| Integration Gateway  | Node.js, Express           | GitHub/GitLab OAuth, webhooks, CI/CD события      |
| Notification Service | Node.js                    | Email/SMS/push, обработка событий RabbitMQ        |
| ML Service           | FastAPI, Python            | Рекомендации проектов и команд                    |
| Frontend             | Next.js 14, TypeScript     | Веб-клиент (App Router, React 18)                 |
| API Gateway (nginx)  | nginx                      | Объединение фронтенда и API, TLS, rate limiting   |
| Monitoring stack     | Prometheus, OpenObserve    | Метрики, логи, трэйсы                             |
| Object Storage       | SeaweedFS (S3)             | Аватары, файлы проектов                           |

## Данные и модель

- PostgreSQL (pgvector при необходимости) хранит пользователей, проекты, команды, задачи, приглашения, ревью, showcase, уведомления.
- Entity Framework Core обеспечивает доступ; для чтения доступна реплика (`ReadWriteDbContextFactory`).
- SeaweedFS выступает S3-совместимым сториджем (бакеты avatars, project-files, uploads).

## Потоки данных

1. **Регистрация и профиль.** Frontend → Auth Service (JWT) → Core API (`ProfileController`) → PostgreSQL.
2. **Проекты и showcase.** Core API управляет CRUD, хранит метаданные, файлы — в SeaweedFS, статусы публикуются через RabbitMQ.
3. **Коммуникации.** Core API создаёт события (`project.created`, `team.member.joined`), Notification Service обрабатывает и рассылает email/SMS, ML Service использует данные для обучения.
4. **Интеграции.** Integration Gateway выдаёт OAuth ссылку, получает webhooks, транслирует командные события в Core API и RabbitMQ.
5. **Наблюдаемость.** Каждый сервис отдаёт `/metrics`, логирует в JSON (Serilog / pino), шлёт OTLP-трейсы в Jaeger/OpenObserve.

## Use Cases ↔ сервисы

| UC   | Описание                          | Реализация                                    |
| ---- | --------------------------------- | --------------------------------------------- |
| UC-1 | Регистрация пользователя, профиль | Auth Service + Core API (`ProfileController`) |
| UC-2 | Управление проектами              | `ProjectsController` + объектное хранилище    |
| UC-3 | Поиск и приглашения в команды     | `InvitationsController`, `TeamsController`    |
| UC-4 | Работа в команде (tasks, чат)     | `TasksController`, SignalR hubs               |
| UC-5 | Showcase и публикация             | `ProjectsController` + showcase entities      |
| UC-6 | Модерация и верификация           | `AdminController`, `ModerationController`     |

## Интеграции и события

- RabbitMQ exchange `devhunt.events` обслуживает нотификации, ML и внешние связки.
- Integration Gateway подписывается на webhooks GitHub/GitLab и публикует события в Core API и очередь `devhunt.integrations`.
- Notification Service слушает `devhunt.notifications`, отправляет email/SMS/Push и обновляет статус в Core API.

## Наблюдаемость

- Prometheus → `/metrics` всех сервисов + экспортёры PostgreSQL/Redis/RabbitMQ.
- OpenObserve концентрирует логи (через Serilog sink) и трэйсы (OTLP).
- Jaeger UI используется для отладки распределённых запросов.

## Масштабирование

- Core API, Auth, Node-сервисы — stateless; масштабирование горизонтальное.
- PostgreSQL: primary + read replicas.
- Redis: отдельный сервис для кэша и pub/sub.
- ml-service и notification-service масштабируются независимо в зависимости от нагрузки очередей.

## Документы по теме

- Топология и инфраструктура: `docs/architecture/deployment-topology.md`
- Security guide: `docs/architecture/security.md`
- Продакшн-чеклист: `docs/guides/production-readiness.md`
