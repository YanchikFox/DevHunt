# DevHunt Auth Service

ASP.NET Core (.NET 10) service that owns user credentials, JWT issuance, refresh token rotation, and OAuth2 login via GitHub/GitLab. CSRF is enforced with `SameSite=Strict`.

## Local development

```bash
docker compose up auth-service
```

Health: `http://localhost:7001/health`  
Swagger: `http://localhost:7001/swagger`

The service depends on `db` and `cache-service`. Run `docker compose up db cache-service db-migrator` first if starting it in isolation.
