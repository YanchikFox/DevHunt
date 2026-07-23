---
title: DevHunt.Analyzer (code-analyzer) — static analysis HTTP service
type: system
status: verified
sources:
  - DevHunt.Analyzer/devhunt_analyzer/api.py
  - DevHunt.Analyzer/devhunt_analyzer/cli.py
  - DevHunt.Analyzer/pyproject.toml
  - DevHunt.Analyzer/Dockerfile
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Python 3.13 service that performs multi-language static analysis on
git repositories. Owns its own rule registry and uses tree-sitter
parsers for C#, TypeScript, Python, Java, Go, Rust, PHP, Ruby,
Kotlin, C, C++ (Swift is optional). Semgrep is also installed and
available (Dockerfile line 15).

Compose service `code-analyzer`, port `PORT_ANALYZER` (default
8090), bound to 127.0.0.1 only.

The same package ships **two entry-points** but the deployable uses
just one:
- `python -m devhunt_analyzer.api` — HTTP server (used in Docker).
- `devhunt-analyze` console script
  (`devhunt_analyzer.cli:main`) — local CLI; not invoked in
  compose.

## Entry-point (deployable)

`Dockerfile:28` →
`CMD ["python", "-m", "devhunt_analyzer.api"]`

[DevHunt.Analyzer/devhunt_analyzer/api.py](DevHunt.Analyzer/devhunt_analyzer/api.py).

## Boot wiring

- HTTP server is the **stdlib `http.server.HTTPServer` /
  `BaseHTTPRequestHandler`** (line 19). **Not** FastAPI, not
  Flask. No middleware, no async loop.
- `import devhunt_analyzer.rules  # noqa: F401` (line 24) is a
  side-effecting import that registers built-in rules into the
  rule registry.
- Listen address: `ANALYZER_HOST` (default `0.0.0.0`),
  `ANALYZER_PORT` (default 8090) — lines 29–30.
- Limits: `ANALYZER_MAX_REPO_MB` (default 200),
  `ANALYZER_CLONE_TIMEOUT` (default 120s).
- Allow-list of clone hosts (line 33): `github.com`, `gitlab.com`,
  `bitbucket.org` only — enforced in `_validate_repo_url`.

## Endpoints (per docstring at lines 4–11)

- `POST /analyze` — clones a repo (or accepts a path) and returns
  analysis JSON.
- `GET /health` — health check (used by Docker `HEALTHCHECK`).
- `GET /rules` — lists registered rules.

## Outbound dependencies

- **External git hosts** (HTTPS clone). Optional bearer auth via
  `x-access-token:TOKEN@host` injection in `_build_clone_url`
  (lines 51–58) when a token is supplied.
- No PostgreSQL, Redis, RabbitMQ, OpenObserve, or DevHunt service.

## What I should NOT assume

- **Auth handling is unverified.** The compose env passes
  `ANALYZER_API_SECRET`, but the visible portion of `api.py` (top
  60 lines) does not show how it's enforced. The full `api.py` is
  longer than what was read in Step 2; before recommending
  anything that touches auth here, read the rest of the file.
- **Not a FastAPI service.** Don't reach for `uvicorn`, dependency
  injection, or pydantic models — they aren't here. If you add
  endpoints, you'll be writing handler classes.
- **Allow-list is hardcoded.** Adding a new git host means editing
  `ALLOWED_HOSTS` in code, not configuration.
- **Semgrep cache lives in `analyzer`'s home directory**
  (Dockerfile line 20) — running outside the container without
  that home dir will surface different cache behavior.
- **The CLI entry-point is not exercised in production.** Treat
  `cli.py` as a developer tool, not part of the deployable
  contract.
