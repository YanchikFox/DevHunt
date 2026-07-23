---
sidebar_position: 1
---

# Операционная справка

Справочник для инженеров сопровождения: куда заходить, какие учётные записи использовать, как перезапускать сервисы и что делать при сбоях.

## Основные URL и доступы

| Компонент            | Адрес / порт                                    | Учетные данные / заметки                                    |
| -------------------- | ----------------------------------------------- | ----------------------------------------------------------- |
| Frontend             | `http://localhost:3000`                         | При проде работает за `api-gateway`                         |
| API Gateway (nginx)  | `http://localhost:80` / `https://localhost:443` | TLS включается по инструкции в deployment guide             |
| Core API             | `http://localhost:7002`                         | Swagger: `/swagger`; health: `/health`; metrics: `/metrics` |
| Auth Service         | `http://localhost:7001`                         | Health: `/health`; логин через Swagger                      |
| Integration Gateway  | `http://localhost:5002`                         | `/health`, `/api/oauth/:provider/url`                       |
| Notification Service | `http://localhost:5003`                         | `/health`, `/metrics`                                       |
| ML Service           | `http://localhost:8000`                         | `/health`, `/docs`                                          |
| RabbitMQ UI          | `http://localhost:15672`                        | Пользователь `devhunt` / пароль из `.env`                   |
| PostgreSQL           | порт `5432`                                     | `psql -h localhost -U postgres -d devhunt_db`               |
| Redis                | порт `6379`                                     | auth optional (`REDIS_PASSWORD`)                            |
| SeaweedFS S3         | `http://localhost:8333`                         | Access/secret из `.env`                                     |
| SeaweedFS Filer UI   | `http://localhost:8888`                         | В dev режим пароль не требуется — не публикуйте наружу      |
| OpenObserve          | `http://localhost:5080`                         | `OPENOBSERVE_ROOT_USER` / `OPENOBSERVE_ROOT_PASSWORD`       |
| Jaeger               | `http://localhost:16686`                        | Без авторизации                                             |

## Мониторинг и алерты

- Prometheus: `http://localhost:9090`, конфиг `monitoring/prometheus.yml`, оповещения — `monitoring/alerts.yml`.
- Экспортёры: PostgreSQL (`9187`), Redis (`9121`), RabbitMQ (`9419`).
- OpenObserve собирает метрики/логи/трейсы (remote_write из Prometheus + OTLP из сервисов).

## Скрипты администрирования

| Назначение           | Команда                                           |
| -------------------- | ------------------------------------------------- |
| Smoke-тест API       | `.\scripts\testing\test-api.ps1 -All` / `.sh`     |
| Проверка интеграций  | `.\scripts\testing\test-integration-gateway.ps1`  |
| Проверка нотификаций | `.\scripts\testing\test-notification-service.ps1` |
| Сброс БД             | `.\scripts\database\reset-db.ps1 [-Force]`        |
| Сброс БД + seed      | `.\scripts\database\reset-and-seed.ps1`           |
| Бэкап БД             | `./scripts/database/backup-db.sh`                 |
| Восстановление БД    | `./scripts/database/restore-db.sh backup.sql.gz`  |

> Все скрипты читают параметры (`DB_HOST`, `DB_USER`, `POSTGRES_PASSWORD` и т.п.) из текущего окружения или `.env`.

## RabbitMQ queues / события

- Exchange: `devhunt.events`.
- Очереди по умолчанию:
  - `devhunt.notifications` — Notification Service.
  - `devhunt.integrations` — Integration Gateway.
  - `devhunt.ml.recommendations` — ML Service.
- Масштабирование потребителей задаётся переменными `INTEGRATION_WORKER_CONCURRENCY`, `RABBITMQ_MAX_RETRIES`.

## Объектное хранилище (SeaweedFS)

- S3 endpoint: `http://localhost:8333` (доступ через AWS CLI или `mc`).
- IAM конфигурация описана в `seaweedfs/s3.json`.
- Скрипт `seaweedfs/init-s3.sh` автоматически создаёт пользователя `devhunt` при старте.
- В проде отключайте публичный доступ к Filer UI, храните ключи в Vault.

## Troubleshooting

1. **Сервис не поднимается.** `docker compose logs -f <service>`, проверьте зависимости (`depends_on`), убедитесь что `db-migrator` завершился успешно.
2. **Проблемы с сетью.** `docker network inspect devhunt_default`, перезапустите Docker Desktop, проверьте `DOCKER_HOST`.
3. **БД недоступна.** Убедитесь, что пароль в `.env` совпадает с контейнером (`docker compose exec db printenv POSTGRES_PASSWORD`).
4. **RabbitMQ переполнен.** Посмотрите `rabbitmqctl list_queues`, увеличьте потребителей (`INTEGRATION_WORKER_CONCURRENCY`), очистите зависшие сообщения.
5. **SeaweedFS возвращает 403.** Проверьте access/secret и файл `seaweedfs/s3.json`, обновите креды через `curl` POST на `/etc/seaweedfs/s3.json`.
6. **Нет метрик.** Убедитесь, что `/metrics` открывается локально и `Prometheus` читает `monitoring/prometheus.yml`.

## Ротация секретов

- JWT/Encryption/ObjectStorage ключи обновляются одновременно в Core API и Auth Service (перезагрузите оба).
- SMTP/SendGrid — меняются в `.env` → `docker compose up -d notification-service`.
- OAuth GitHub/GitLab — обновите переменные `GITHUB_*` / `GITLAB_*` и перезапустите Integration Gateway.
