---
title: Configuration & Secrets — source stack, Azure Key Vault, EnvLoader, key conventions
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Program.cs
  - DevHunt.CoreApi/Extensions/ConfigurationExtensions.cs
  - DevHunt.Infrastructure/Configuration/EnvLoader.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

The layered configuration system for Core API: how secrets
and settings are loaded, in what order, and which sources win
at each environment. Includes a custom `.env` loader used by
local tooling so it does not need to duplicate secrets into
`appsettings.json`.

## Configuration source stack (in priority order, last wins)

```
Program.cs:54-59:

1. appsettings.json            — baseline, committed, no secrets
2. appsettings.{Env}.json      — environment overlay (Development, Staging)
3. Environment variables       — Docker Compose / k8s secrets
4. AddSecretsConfiguration():
     if KeyVault:Uri set AND IsProduction()
       → Azure Key Vault (refresh every 5 min)
         fail-safe: KeyVault unavailable → logs warning, continues with env vars
     else (non-Production)
       → .NET User Secrets (assembly-scoped, optional)
```

`EnvLoader.Load()` runs **before** `CreateBuilder` (Program.cs:50).
It populates `Environment.SetEnvironmentVariable` so that source #3
above picks up `.env` values. It does not add a configuration provider.

## EnvLoader — the .env integration

`DevHunt.Infrastructure.Configuration.EnvLoader`:

- **Search strategy**: walks up from `AppContext.BaseDirectory`
  until it finds a `.env` file or hits the filesystem root.
  First file found wins.
- **Never overwrites**: only calls `SetEnvironmentVariable` when
  the key is absent or empty (`if (string.IsNullOrEmpty(existing))`).
  Docker-injected env vars always win over `.env`.
- **Variable expansion**: `${VAR_NAME}` and `${VAR_NAME:-default}`
  syntax supported.
- **Quote stripping**: single and double quotes stripped from values.
- **`ApplyLocalDatabaseOverrides()`**: when `DOTNET_RUNNING_IN_CONTAINER`
  is NOT `"true"`, rewrites `POSTGRES_HOST`, `CONNECTIONSTRINGS__DEFAULTCONNECTION`,
  `CONNECTIONSTRINGS__READONLYCONNECTION`, and `DEVHUNT_DB_CONNECTION`
  to point at `localhost` if the current `POSTGRES_HOST` matches
  `SERVICE_DB_NAME`. This lets `dotnet ef` and console tools reach
  the DB on localhost while Docker services use the internal hostname.
- **Thread-safe, idempotent**: `_loaded` flag with double-checked
  lock; safe to call multiple times.

## Azure Key Vault integration

`AddSecretsConfiguration` (ConfigurationExtensions.cs):

- **Active only in Production** — `environment.IsProduction()` guard.
- Trigger: `KeyVault:Uri` must be non-empty in config at build time.
- Authentication: `DefaultAzureCredential` (supports Managed Identity,
  Azure CLI, env vars `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` /
  `AZURE_TENANT_ID`).
- Refresh interval: 5 minutes (so key rotation takes up to 5 min to
  propagate; immediately restart to force reload).
- Fail-safe: unavailable Key Vault logs `Console.WriteLine` (not
  Serilog — Serilog isn't built yet at this point) and falls through
  to env vars.

In non-Production the block is skipped entirely; User Secrets
(`AddUserSecrets`) is used instead.

## Environment variable naming for ASP.NET Core config

Standard ASP.NET Core convention: `__` (double underscore) replaces
`:` in config key paths.

Examples from appsettings:
```
ConnectionStrings:DefaultConnection → CONNECTIONSTRINGS__DEFAULTCONNECTION
RabbitMQ:ConnectionString          → RABBITMQ__CONNECTIONSTRING
OpenObserve:User                   → OPENOBSERVE__USER
Features:Redis:Enabled             → FEATURES__REDIS__ENABLED
Encryption:Key                     → ENCRYPTION__KEY
```

Docker Compose uses both styles in practice — check the
`docker-compose.yml` environment blocks for exact key names.

## What I should NOT assume

- **Key Vault is the secrets store only in Production.** In
  Staging, Development, and any environment where
  `IsProduction()` returns false, secrets come from env vars
  or User Secrets — Key Vault is not consulted even if
  `KeyVault:Uri` is configured.
- **Key Vault fail-safe logs to `Console`, not Serilog.** The
  Key Vault load happens before `builder.Host.UseSerilog`, so
  the warning is a plain `Console.WriteLine`. It will appear in
  stdout but not in structured OpenObserve logs.
- **`.env` is for local development and tooling only.** It is
  gitignored (contains secrets). Docker Compose injects secrets
  via environment variables, which take precedence over `.env`
  because `EnvLoader` does not overwrite existing env vars.
- **`ApplyLocalDatabaseOverrides` assumes a specific env var
  pattern.** It only fires when `POSTGRES_HOST == SERVICE_DB_NAME`
  (service name as hostname) AND not running in a container.
  It will silently not fire if either condition fails.
