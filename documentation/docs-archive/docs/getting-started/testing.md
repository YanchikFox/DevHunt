---
sidebar_position: 1
---

# Руководство по тестированию

Единый набор практик для проверки сервисов DevHunt перед мерджем или релизом.

## Матрица тестов

| Сервис / слой            | Команда                                     | Назначение                     |
| ------------------------ | ------------------------------------------- | ------------------------------ |
| Core API / Auth (.NET)   | `dotnet test`                               | Unit и интеграционные тесты    |
| Core API (интеграция)    | `dotnet test --filter Category=Integration` | HTTP-флоу через TestServer     |
| ml-service               | `pytest` _(если включён)_                   | Логика рекомендаций            |
| Integration/Notification | `npm run test` _(Jest, при наличии)_        | Проверка middleware и сервисов |
| Frontend                 | `npm run test` / `npm run test:e2e`         | Vitest и Playwright            |
| E2E smoke                | `./scripts/testing/test-api.sh`             | Быстрый регресс критичных API  |

## .NET сервисы (Core API, Auth, Database tools)

```bash
dotnet test                             # полный прогон
dotnet test --collect:"XPlat Code Coverage"
dotnet test --filter Category=Unit
```

Рекомендации:

- Для интеграционных тестов используются `WebApplicationFactory` + InMemory/PostgreSQL.
- Покрытие целевое ≥ 70%. Отчёты появляются в `TestResults/`.
- Для миграторов/сидеров достаточно smoke-теста: `dotnet run --project DevHunt.DatabaseMigrator -- --help`.

## Node.js сервисы (Integration Gateway, Notification Service)

В проектах настроены линтеры и базовые тестовые заготовки. Перед релизом:

```bash
cd integration-gateway
npm install
npm run lint
npm test

cd ../notification-service
npm install
npm run lint
npm test
```

Даже если покрытие пока минимальное, команда `npm test` должна выполняться без ошибок (Jest/TS-Jest готовы к расширению).

## ML Service (FastAPI)

База для unit-тестов находится в `ml-service/tests`. Пример:

```bash
cd ml-service
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
pytest
```

Для smoke-проверки REST-эндпоинта достаточно `curl http://localhost:8000/health`.

## Frontend (Next.js, Vitest/Playwright)

```bash
cd frontend
npm install
npm run lint
npm run test           # Vitest
npm run test:ui        # Vitest UI при необходимости
npm run test:e2e       # Playwright (headless)
npm run test:e2e:ui    # Playwright UI
```

Playwright использует базовые сценарии UF-1/UF-3, поэтому перед прогоном убедитесь, что API отвечает (можно задействовать mock-адаптеры через `NEXT_PUBLIC_USE_MOCKS=true`).

## Скрипты и вспомогательные проверки

- `scripts/testing/test-api.ps1|sh` — минимальный регресс CRUD/аутентификации.
- `scripts/testing/test-notification-service.ps1` — проверка очередей RabbitMQ и email/SMS-шлюзов.
- `scripts/testing/test-integration-gateway.ps1` — smoke oauth/webhook.
- `scripts/database/reset-and-seed.ps1` — очистка базы + загрузка эталонных данных.

## Советы по CI

1. Соберите артефакты: `dotnet build`, `npm run build` для фронтенда.
2. Прогоните `docker compose -f docker-compose.yml -f docker-compose.test.yml up -d` если требуется изолированное окружение.
3. Репортинг: публикация `TestResults/*.trx` и Playwright HTML.
4. При падении smoke-скриптов см. `docs/guides/operations.md#troubleshooting`.
