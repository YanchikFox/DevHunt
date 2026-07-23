# DevHunt Database Seeder

Dev-only .NET console tool that populates the database with realistic test data (users, projects, teams, tasks, etc.). Not part of `docker-compose.yml` — run manually when you need a fresh dev dataset.

## Usage

Requires the database to be running and migrations applied first:

```bash
docker compose up db db-migrator
dotnet run --project DevHunt.DatabaseSeeder
```

Re-running is safe — the seeder skips entities that already exist.
