# Security Guide

Собрано всё, что касается секретов, шифрования, защиты API и последних фиксов.

## Управление секретами

- **Development:** `.env` (не коммитить) или `dotnet user-secrets`.
- **Production:** Vault/Secrets Manager; Docker/K8s получают только ссылки на секреты.
- **Ключевые переменные:**  
  `ConnectionStrings__DefaultConnection`, `ConnectionStrings__RedisConnection`,  
  `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`,  
  `Encryption__Key` (32 символа), `Encryption__IV` (16 символов),  
  `ObjectStorage__AccessKey/SecretKey`,  
  `SMTP/SENDGRID`, `GITHUB/GITLAB`, `FCM/TWILIO` параметры.

Пример безопасного блока:

```bash
ConnectionStrings__DefaultConnection="Host=db.example;Port=5432;Database=devhunt;Username=devhunt;Password=***"
Jwt__Key="base64(32+ chars)"
Encryption__Key="32_char_secret_key_devhunt!!"
Encryption__IV="16_char_iv_value"
```

## Аутентификация и авторизация

- JWT access + refresh токены (rotation, revocation, хранение в httpOnly cookies).
- OAuth2 для GitHub/GitLab (через Integration Gateway).
- RBAC на уровне Core API (`Admin`, `Curator`, `Participant`).
- Rate limiting и throttling (Redis + middleware).

## Шифрование и хранение данных

- AES-256 CBC с уникальным IV на каждую запись (`EncryptionService`).
- BCrypt (cost 12) для паролей.
- Хранение refresh-токенов в PostgreSQL с хэшированием.
- SeaweedFS / S3: доступ по ключам, bucket ACL ограничены.

## Защита API

- Security headers: `HSTS`, `X-Content-Type-Options`, `X-Frame-Options`, `CSP` с nonce.
- CSRF-токены и double-submit cookies для web-форм.
- Ограничение размера запросов (10 МБ uploads, 4 МБ JSON).
- Валидация входных данных (DataAnnotations + FluentValidation).
- Profanity filter / Moderation middleware для чата.
- `/metrics` защищён токеном или listen-only на localhost.

## Наблюдение и аудит

- Serilog (JSON) + OpenSearch/OpenObserve.
- AuditService фиксирует критичные действия (логин, изменения ролей, публикации showcase).
- Prometheus и Alertmanager отслеживают ошибки (5xx rate, latency p95).
- Jaeger покрывает распределенные запросы (Core API ↔ Auth ↔ ML ↔ интеграции).

## Последние исправления

- **R7:** хранение JWT в httpOnly cookies.
- **SEC-004:** случайные IV и хранение IV вместе с cipher.
- **SEC-007:** защита от SQL injection (только EF Core, параметризованные запросы).
- **SEC-009:** строгая настройка CSP с nonce.
- **SEC-012:** лимиты загрузок и валидация MIME.

## Рекомендации

1. Не храните секреты в git/CI логах.
2. Регулярно обновляйте базовые образы (dotnet, node, python, nginx).
3. Поднимайте TLS в проде (см. `docs/guides/deployment.md`).
4. Ограничивайте доступ к системам мониторинга сетью или SSO.
5. Выполняйте статический анализ (`dotnet format / analyze`, `npm audit`, `pip audit`).

## Документы

- Архитектура: `docs/architecture/architecture-overview.md`
- Топология: `docs/architecture/deployment-topology.md`
- Production readiness: `docs/guides/production-readiness.md`
