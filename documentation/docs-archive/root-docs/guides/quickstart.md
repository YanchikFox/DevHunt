# Руководство по быстрому запуску

Документ описывает единый способ развернуть DevHunt целиком на локальной машине для разработки или приёмки. Все подробные сценарии эксплуатации вынесены в `docs/guides/operations.md`.

## Предварительные требования

- Docker Desktop 4.30+ (Compose V2 включён по умолчанию).
- .NET SDK 8/9 (для запуска отдельных сервисов вне контейнеров).
- Node.js 18+ (для фронтенда при локальной разработке без Docker).
- Python 3.11+ (если требуется запускать `ml-service` вручную).
- PowerShell 7+ или Bash 5+ для вспомогательных скриптов.

## Подготовка окружения

1. Скопируйте переменные окружения и заполните секреты:
   ```bash
   cp env.example .env
   ```
   Обязательные значения: `POSTGRES_PASSWORD`, `RABBITMQ_DEFAULT_PASS`, `JWT_KEY`, `ENCRYPTION_KEY`, `ENCRYPTION_IV`, `OBJECT_STORAGE_ACCESS_KEY`, `OBJECT_STORAGE_SECRET_KEY`.
2. При необходимости включите дополнительные интеграции (GitHub/GitLab, SMTP и т.д.) — см. таблицы ENV в README соответствующих сервисов.

## Запуск всего стека

```bash
# Запуск всех сервисов в фоне
docker compose up -d

# Просмотр статуса
docker compose ps

# Постоянные логи конкретного сервиса
docker compose logs -f core-api
```

Сценарий включает PostgreSQL, Redis, RabbitMQ, SeaweedFS, Auth Service, Core API, Integration Gateway, Notification Service, ml-service, frontend, nginx (gateway), мониторинг (Prometheus + OpenObserve) и экспортёры.

### Частичный запуск

- Только критичные сервисы API:
  ```bash
  docker compose up -d db cache-service message-broker auth-service core-api
  ```
- Проверка отдельных компонентов:
  ```bash
  docker compose up -d ml-service
  docker compose restart notification-service
  ```

## Проверка после старта

1. Автотесты стабильности API:
   ```powershell
   .\scripts\testing\test-api.ps1 -All   # Windows
   ./scripts/testing/test-api.sh         # Linux/macOS
   ```
2. Быстрые curl-проверки:
   ```bash
   curl http://localhost:7001/health           # Auth Service
   curl http://localhost:7002/health           # Core API
   curl http://localhost:5002/health           # Integration Gateway
   curl http://localhost:5003/health           # Notification Service
   ```
3. Основные интерфейсы:
   - Frontend: http://localhost:3000
   - Swagger Core API: http://localhost:7002/swagger
   - RabbitMQ UI: http://localhost:15672 (devhunt / пароль из `.env`)
   - OpenObserve: http://localhost:5080 (admin@devhunt.local / `OPENOBSERVE_ROOT_PASSWORD`)
   - Jaeger: http://localhost:16686

Полный список точек входа и учёток находится в `docs/guides/operations.md`.

## Остановка и очистка

```bash
# Мягкая остановка
docker compose down

# Полная очистка с удалением томов (используйте осторожно)
docker compose down -v
```

## Частые действия

| Сценарий                       | Команда / действие                                                                             |
| ------------------------------ | ---------------------------------------------------------------------------------------------- |
| Пересобрать один сервис        | `docker compose up core-api --build`                                                           |
| Обновить миграции БД           | `dotnet ef database update --project DevHunt.Infrastructure --startup-project DevHunt.CoreApi` |
| Сбросить БД и перезалить seed  | `.\scripts\database\reset-and-seed.ps1`                                                        |
| Проверить потребление ресурсов | `docker stats`                                                                                 |
| Обновить фронтенд зависимости  | `cd frontend && npm install`                                                                   |

## Следующие шаги

- См. `docs/guides/testing.md` для прогонов unit / integration / E2E.
- См. `docs/guides/deployment.md` для подготовки staging/production.
- Для справочной информации об окружении, паролях и мониторинге переходите к `docs/guides/operations.md`.
