# Руководство по деплою

Собрано всё, что нужно для вывода DevHunt на staging/production: переменные окружения, проверки перед запуском, работа с TLS и Kubernetes.

## Чек-лист перед деплоем

- [ ] Подготовлен `.env.production` (секреты ≥ 32 символов).
- [ ] Разрешены порты 80/443 (для `api-gateway`) и 5432/6379/5672 при необходимости.
- [ ] DNS записан на нужный IP.
- [ ] CORS-конфигурация содержит только доверенные домены фронтенда.
- [ ] TLS-сертификаты выписаны и смонтированы.
- [ ] Настроены учётные данные OpenObserve, RabbitMQ, PostgreSQL.

## Docker Compose (стандартный сценарий)

```bash
cp env.production.example .env
# заполните секреты, домены, email SMTP/OAuth переменные

# поднять инфраструктуру
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d db cache-service message-broker object-storage

# применить миграции
docker compose run --rm core-api dotnet ef database update

# старт всех сервисов
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

Полезные команды:

- `docker compose ps` — статус.
- `docker compose logs -f api-gateway` — проверка конфигурации nginx.
- `docker compose exec db psql -U postgres -d devhunt_db` — доступ к БД.

## TLS / HTTPS

1. Установите Certbot на хост (Ubuntu пример):
   ```bash
   sudo apt-get update && sudo apt-get install certbot python3-certbot-nginx
   sudo certbot certonly --nginx -d example.com -d www.example.com
   ```
2. Смонтируйте каталоги Let's Encrypt в контейнер `api-gateway` через volumes:
   ```yaml
   services:
     api-gateway:
       volumes:
         - /etc/letsencrypt:/etc/letsencrypt:ro
         - /var/www/certbot:/var/www/certbot:ro
   ```
3. В `nginx.conf` пропишите:
   ```nginx
   ssl_certificate     /etc/letsencrypt/live/example.com/fullchain.pem;
   ssl_certificate_key /etc/letsencrypt/live/example.com/privkey.pem;
   ssl_trusted_certificate /etc/letsencrypt/live/example.com/chain.pem;
   ssl_stapling on;
   ssl_stapling_verify on;
   ```
4. Проверка: `sudo certbot renew --dry-run` + `docker compose restart api-gateway`.

## Kubernetes (опционально)

В каталоге `k8s/` лежат базовые манифесты. Общий порядок:

```bash
kubectl apply -f namespace.yaml
kubectl create secret generic devhunt-secrets \
  --from-literal=ConnectionStrings__DefaultConnection="..." \
  --from-literal=Jwt__Key="..." \
  --from-literal=Encryption__Key="..." \
  -n devhunt
kubectl apply -f core-api-deployment.yaml
kubectl apply -f auth-service-deployment.yaml
kubectl apply -f frontend-deployment.yaml
```

Рекомендации:

- Добавьте Ingress (Nginx/Traefik) для публикации фронтенда и API.
- Настройте HorizontalPodAutoscaler для Core API (минимум 3 реплики).
- Используйте Secrets/ConfigMaps для разделения конфигурации.

## Мониторинг и alerting

- Prometheus (`monitoring` сервис) подключается к `alerts.yml`. Добавьте Alertmanager или webhooks при необходимости.
- OpenObserve (порт 5080) хранит трэйсы/logs. Аккаунт `admin@devhunt.local` → пароль `OPENOBSERVE_ROOT_PASSWORD`.
- Экспортёры PostgreSQL/Redis/RabbitMQ должны быть доступны на 9187/9121/9419.

## Резервное копирование

- PostgreSQL: `./scripts/database/backup-db.sh` (gzip в каталоге `backups/`).
- SeaweedFS: используйте S3-compatible бэкапы (например, `rclone sync`).
- Перед масштабными изменениями сохраняйте `.env` и снимайте snapshot с дисков.

## Обновления и откаты

1. `docker compose -f docker-compose.yml -f docker-compose.prod.yml pull`
2. `docker compose ... up -d --build`
3. Проверить health endpoints (см. `docs/guides/operations.md`).
4. При неудаче: `docker compose ... down`, затем запустить предыдущую версию образов (`docker compose ... up -d --no-build`).

## Что ещё проверить перед релизом

- Health checks (`/health`, `/metrics`) отвечают 200.
- Метрики доступны в Prometheus; алерты активны.
- Бэкапы проходят автоматически и тестировались на восстановление.
- Удалены все `CHANGE_ME` значения в `.env` и `docker-compose.prod.yml`.
