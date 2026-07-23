---
sidebar_position: 4
title: Backup And Recovery
description: Current PostgreSQL backup service behavior and recovery cautions.
sidebar_label: Backup And Recovery
---

# Backup And Recovery

> _If any detail here contradicts the code, trust the code — not this page._

The current Compose backup service is a PostgreSQL image running an inline shell loop. It is not custom application code.

## Current Backup Behavior

Compose service: `backup-service`

Runtime behavior:

```text
while true:
  pg_dump -h db -U postgres -d devhunt_db -F c -f /backups/db-YYYYMMDD-HHMMSS.dump
  if success:
    delete /backups/*.dump older than 7 days
  sleep 86400
```

Backups are written to the `backup_data` Docker volume.

## Important Limits

- The command hard-codes `-U postgres` and database `devhunt_db`, even though `POSTGRES_USER` and `POSTGRES_DB` are also present in the environment.
- There is no restore automation in Compose.
- There is no explicit backup health check.
- Retention is local to the Docker volume and deletes dumps older than 7 days.
- Object storage data is not backed up by this service.
- OpenObserve data is not backed up by this service.

Treat this as a development/simple-runtime backup loop, not a complete disaster recovery system.

## Restore Cautions

Before restoring:

- stop writers or restore into a separate database;
- confirm which dump timestamp is wanted;
- confirm the target schema/migration state;
- preserve `__EFMigrationsHistory`;
- test with PostgreSQL plus pgvector when vector tables are involved.

The project uses EF migrations through `DevHunt.DatabaseMigrator`. After restoring a database, run the migrator only when you intentionally want to bring that restored DB up to the current schema.

## What A Complete Recovery Plan Still Needs

- documented `pg_restore` procedure;
- regular restore test in an isolated environment;
- off-host backup storage;
- backup and restore plan for SeaweedFS object data;
- backup and restore plan for OpenObserve data;
- alerting when the backup loop fails.
