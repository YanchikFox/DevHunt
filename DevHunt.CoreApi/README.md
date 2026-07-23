# DevHunt Core API

Main platform backend — ASP.NET Core (.NET 10) with ~50 controllers covering projects, teams, tasks, chat, showcase, moderation, badges, and more. Exposes REST (`/api/*`), SignalR hubs (`/chatHub`, `/notificationHub`), and Swagger UI.

## Local development

```bash
docker compose up core-api
```

Health: `http://localhost:7002/health`  
Swagger: `http://localhost:7002/swagger`

Depends on `db`, `cache-service`, `message-broker`. `db-migrator` must have run at least once before first start.
