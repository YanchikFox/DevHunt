# DevHunt Infrastructure

Shared .NET library — not a standalone deployable. Owns `DevHuntDbContext`, all 45 EF Core entity classes, and every database migration. Referenced by `auth-service`, `core-api`, and `db-migrator`.

## Adding a migration

```bash
dotnet ef migrations add <Name> \
  --project DevHunt.Infrastructure \
  --startup-project DevHunt.CoreApi
```

Then run `db-migrator` (or `dotnet ef database update`) to apply it.
