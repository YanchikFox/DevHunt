---
title: Orphaned top-level projects (DatabaseSeeder, CtFixer)
type: unverified
status: unverified
sources:
  - DevHunt.slnx
  - docker-compose.yml
  - DevHunt.DatabaseSeeder/
  - CtFixer/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review:
---

## What I observed

`DevHunt.slnx` lists exactly three projects: `DevHunt.AuthService`,
`DevHunt.CoreApi`, `DevHunt.Infrastructure`.

`docker-compose.yml` builds the following from this repo: `auth-service`,
`core-api`, `db-migrator` (`DevHunt.DatabaseMigrator/Dockerfile`),
`code-analyzer` (`DevHunt.Analyzer/Dockerfile`), `frontend`, `ml-service`,
`notification-service`, `integration-gateway`, `api-gateway`,
`backup-service`, `openobserve-init`.

Two top-level project directories are not referenced by either:

- `DevHunt.DatabaseSeeder/`
- `CtFixer/`

## Why this is unverified

Without reading documentation, the three Step-1 sources alone do not
explain whether these are:

- one-shot CLI tools run manually (e.g., for dev seeding or codemods),
- abandoned/legacy code that should be ignored,
- something invoked from CI or scripts I haven't read yet,
- or something else entirely.

## Questions for the user

1. Is `DevHunt.DatabaseSeeder` still in active use? If yes, how is it
   invoked (manual `dotnet run`? a script under `scripts/`? a CI job?).
2. Same question for `CtFixer`. The name suggests a CancellationToken
   codemod (cf. `fix_cancellation_tokens.py` at repo root), so it may
   be a one-time tool.
3. If either is dead code, should it stay in the repo or be flagged
   for removal? (I will not delete anything; this is just to decide
   how memory should treat them.)

Answers determine whether each gets its own `systems/*.md`, a brief
mention in `playbooks/` once we run it, or no entry at all.
