# Production Readiness

Сводка по готовности DevHunt к эксплуатации и список обязательных мероприятий перед релизом.

## Общий статус

- **Готово:** базовые сценарии SRS (UC-1 … UC-6), Auth, RabbitMQ-события, объектное хранилище, observability.
- **Частично:** ML-рекомендации (алгоритм MVP), интеграции git-провайдеров (OAuth есть, но нужны боевые credentials).
- **Открытые риски:** Outbox паттерн для сообщений, расширенные e2e тесты, автоматизация деплоя.

## Надёжность

- PostgreSQL поддерживает primary + read replica (см. `ReadWriteDbContextFactory`).
- Redis используется для кэша, rate limiting и SignalR backplane.
- RabbitMQ события (`devhunt.events`) доставляют нотификации/интеграции.
- SeaweedFS обеспечивает совместимость с S3 (аватары, файлы проектов).
- Graceful shutdown в .NET и Node сервисах настроен через `SIGTERM` хендлеры.

### Что проверить

- [ ] Настроен бэкап PostgreSQL (ежедневно, хранение ≥ 30 дней).
- [ ] Логи Serilog уходят в OpenSearch/OpenObserve.
- [ ] Alertmanager или сторонние уведомления подключены.
- [ ] Конфигурация rate limiting соответствует прод-нагрузке.
- [ ] Секреты хранятся в Vault/Secret Manager (не в файлах).

## Безопасность

- JWT + refresh-rotation + httpOnly cookies (R7).
- AES-256 с уникальным IV на операцию (SEC-004).
- BCrypt cost factor 12 для паролей.
- CSP/HSTS/anti-CSRF включены.
- Ограничения загрузок: MIME whitelist, 10 МБ.
- `/metrics` защищён токеном/localhost.

### Контрольный список

- [ ] JWT, Encryption, ObjectStorage ключи ≥ 32 символов.
- [ ] Доступ к OpenObserve / RabbitMQ защищён паролями, UI не торчит в интернет без ACL.
- [ ] TLS включён на API Gateway (см. `docs/guides/deployment.md`).
- [ ] GitHub/GitLab OAuth секреты задаются через переменные окружения.
- [ ] Регулярно обновляются базовые образы (dotnet, node, python).

## Производительность и масштабирование

- Core API масштабируется горизонтально (stateless, режим `Kestrel`).
- Read replicas для тяжёлых SELECT.
- Кэширование проектов, профилей, showcase в Redis.
- RabbitMQ используется как decoupled канал.
- Ml-service и Notification-service масштабируются отдельно.

### Мониторинг SLO

- HTTP p95 < 1.5с.
- 5xx rate < 2% (warning)/5% (critical).
- Очереди RabbitMQ < 10k сообщений в норме.
- Интеграции: Throttle queue < 20.

## Roadmap / Outstanding Work

- REL-002: Outbox Pattern для RabbitMQ (8 SP).
- R5: Уведомления в реальном времени для фронтенда (SignalR/WebSocket).
- R11: Оптимизация `ProjectsController`.
- CI/CD pipeline (GitHub Actions/GitLab).
- Additional e2e сценарии (UF-2, UF-5).

## Документы и ссылки

- Архитектура: `docs/architecture/architecture-overview.md`
- Топология и инфраструктура: `docs/architecture/deployment-topology.md`
- Security guide: `docs/architecture/security.md`
- Operation checklist: `docs/guides/operations.md`
