---
sidebar_position: 1
---

# Топология и инфраструктура

Документ описывает, из чего состоит окружение DevHunt и как сервисы связаны на уровне инфраструктуры.

## Хранилища

### PostgreSQL

- Основная БД + read-replica (при необходимости).
- `ConnectionStrings__DefaultConnection` — запись/миграции.
- `ConnectionStrings__ReadOnlyConnection` — чтение/аналитика.
- Миграции запускаются отдельным сервисом `db-migrator` (Kubernetes Job / Compose init).
- Ежедневный бэкап через `scripts/database/backup-db.sh`.

### Redis

- Используется для кэша, rate limiting, SignalR backplane и хранения refresh-токенов.
- Подключение: `ConnectionStrings__RedisConnection`.
- Экспортёр метрик работает на порту `9121`.

### Object storage (SeaweedFS / S3)

- Endpoint `http://object-storage:8333`, Filer UI `:8888`.
- Конфигурация IAM (`seaweedfs/s3.json`) разворачивается скриптом `init-s3.sh`.
- Бакеты: `avatars`, `project-files`, `uploads`, `backups`.

## Интеграционные шины

### RabbitMQ

- Адрес `amqp://devhunt:***@message-broker:5672`.
- Exchange `devhunt.events`, очереди подключаются сервисами:
  - `devhunt.notifications`
  - `devhunt.integrations`
  - `devhunt.ml.recommendations`
- Экспортёр метрик на `9419`.
- Рекомендуемый паттерн для продакшна — Outbox (в роадмапе REL-002).

## Сетевые сервисы

- **Nginx (`api-gateway`)** — публикует фронтенд и API, резолвит `core-api`, `auth-service`, `frontend`, применяет rate limit и security headers.
- **Frontend** — App Router, работает за `api-gateway` (или напрямую в dev).
- **Auth/Core API** — HTTP + SignalR hubs, защищены health checks и Prometheus endpoint.
- **Integration/Notification services** — Node.js, слушают RabbitMQ и REST.
- **ML Service** — FastAPI, REST (`/api/recommendations`), потребляет события RabbitMQ.

## Наблюдаемость

- **Prometheus (`monitoring`)** — основной сборщик (`monitoring/prometheus.yml`, `alerts.yml`).
- **Экспортёры** — PostgreSQL (`9187`), Redis (`9121`), RabbitMQ (`9419`), плюс `/metrics` у всех сервисов.
- **OpenObserve** — единая точка для логов и трэйсов (пользователь `OPENOBSERVE_ROOT_USER`).
- **Jaeger** — UI для распределённых трэйсов (`http://localhost:16686`).

## Deployment варианты

### Docker Compose

- Стандартные файлы `docker-compose.yml` + `docker-compose.prod.yml`.
- Переменные настраиваются через `.env`.
- Миграции/сиды запускаются как отдельные одноразовые контейнеры.
- Сервисы можно запускать частично (`docker compose up core-api auth-service`).

### Kubernetes

- Базовые манифесты в `k8s/`.
- Secrets/ConfigMaps для конфигурации.
- Liveness/readiness на `/health`.
- Горизонтальное масштабирование Core API, Auth Service, Node-сервисов.
- Внешний Ingress (Nginx/Traefik) обеспечивает TLS и маршрутизацию.

## Ресурсные лимиты (Compose пример)

- Core API: 1 CPU / 1.5 GB RAM.
- Auth Service: 0.75 CPU / 768 MB RAM.
- Integration / Notification: 0.5 CPU / 512 MB RAM.
- Prometheus / OpenObserve — отдельные volume и лимиты (настраиваются по нагрузке).

## Секреты и конфигурация

- Dev/stage: `.env` + `docker compose`.
- Prod: Secret Manager / Vault (рекомендуется), переменные передаются контейнерам.
- Критические переменные:
  - `POSTGRES_PASSWORD`, `RABBITMQ_DEFAULT_PASS`.
  - `JWT_KEY`, `ENCRYPTION_KEY`, `ENCRYPTION_IV`.
  - `OBJECT_STORAGE_ACCESS_KEY/SECRET_KEY`.
  - SMTP/OAuth/FCM/Twilio ключи для Notification и Integration сервисов.

## Health и self-healing

- У всех сервисов настроены `healthcheck` (curl/Node HTTP проверка).
- Compose/K8s перезапускает сервисы при сбоях.
- `db-migrator` завершает работу с кодом 0/1, что блокирует загрузку зависящих сервисов.

## Документы

- Архитектура высокого уровня: `docs/architecture/architecture-overview.md`
- Security guide: `docs/architecture/security.md`
- Deployment guide: `docs/guides/deployment.md`
