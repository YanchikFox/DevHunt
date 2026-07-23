# Launch Guide (Dev vs Prod)

## 1. Local Development

In Dev mode all auxiliary services (Mailpit, Documentation) are started, database ports are exposed to the host, and Hot-Reload is enabled.

### Setup
Use the standard `.env` file (no special profile variables needed).

### Launch
Docker **automatically** picks up `docker-compose.override.yml`, which contains the dev-only services (Mailpit, Documentation).

```bash
docker-compose up -d
```

---

## 2. Production

In this mode `docker-compose.override.yml` is **ignored**, so Mailpit and Documentation simply don't exist in the configuration.

### Launch
Use the `-f` flag to explicitly specify configuration files. It is important **NOT** to include the override file.

```bash
# This command loads ONLY the base + production settings.
# The override with local services will be ignored.
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

### Updating in Production
1. Pull changes: `git pull`
2. Rebuild and restart:
   ```bash
   docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
   ```

---

## Env Files Cheat Sheet

| File | Purpose |
|------|---------|
| `docker-compose.yml` | **Base recipe.** Same for everyone. Describes service relationships. |
| `docker-compose.override.yml` | **Local conveniences.** Docker reads it *automatically* by default. Port forwarding and dev settings. |
| `docker-compose.prod.yml` | **Production armor.** Read only when explicitly specified. Closes ports, sets resource limits. |
| `.env` | **Secrets.** Your personal passwords and keys. Excluded from Git. |

## Working with .env Files (Environment Variables)

It is best practice to have separate files for local development and production.

### Local (`.env`)
Located in the project root. Contains fake SMTP settings (or commented out for Mailpit) and development passwords.
**Docker picks it up automatically.**

### Production (`.env.production`)
File with real production keys, SMTP passwords, and settings. Created only on the server.

How to run production with a specific env file:

```bash
# Explicitly specify the env file via --env-file flag
docker compose --env-file .env.production -f docker-compose.yml -f docker-compose.prod.yml up -d
```
*Note: If the file on the server is simply named `.env`, you can omit the `--env-file` flag.*
