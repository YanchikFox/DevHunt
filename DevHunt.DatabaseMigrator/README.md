# DevHunt Database Migrator

One-shot .NET job that applies EF Core migrations to the shared PostgreSQL database. In `docker-compose.yml` it runs as `db-migrator` and exits on success; `auth-service` and `core-api` wait for it via `depends_on`. Has 10× retries with exponential backoff.

## Local development

The migrator runs automatically as part of the stack:

```bash
docker compose up db-migrator
```

Or run it directly against a local DB:

```bash
dotnet run --project DevHunt.DatabaseMigrator
```
