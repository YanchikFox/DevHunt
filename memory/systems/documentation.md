---
title: documentation — Docker-first Docusaurus portal with fail-fast generated API docs
type: system
status: verified
sources:
  - documentation/package.json
  - documentation/Dockerfile
  - docker-compose.docs.yml
  - documentation/scripts/gen-swagger.js
  - documentation/scripts/gen-mlapi.js
  - documentation/scripts/gen-typedoc.js
  - documentation/scripts/gen-docfx.js
  - documentation/scripts/prepare-docs.js
  - documentation/scripts/validate-generated-docs.js
  - documentation/scripts/sync-swagger-ui.js
  - documentation/nginx.conf
  - DevHunt.CoreApi/Program.cs
  - DevHunt.AuthService/Program.cs
  - DevHunt.CoreApi/Extensions/LoggingExtensions.cs
  - DevHunt.AuthService/Extensions/LoggingExtensions.cs
verified_at: 2026-05-14
verified_against_commit: bc31fdf + uncommitted docs-build changes
last_user_review: null
---

## What it is

Docusaurus 3 portal under `documentation/`, served as a standalone Docker image
through `docker-compose.docs.yml`. The docs service binds
`${PORT_DOCUMENTATION:-9000}:80` and serves the Docusaurus build under
`/portal/`; root redirects to `/portal/`.

The portal is now Docker-first. The builder image installs Node 20, Python, the
ML requirements, .NET SDK 10, and `dotnet-runtime-9.0` because the pinned
`swashbuckle.aspnetcore.cli` 7.2.0 tool runs on a .NET 9 runtime even when it
exports `net10.0` assemblies.

## Build Pipeline

`npm run prepare:docs` is the authoritative generation chain:

1. `gen:swagger` builds Core API and Auth Service, then exports OpenAPI through
   `dotnet tool run swagger`.
2. `gen:mlapi` imports the FastAPI app and writes `ml-openapi.json`.
3. `prepare-docs.js` syncs generated specs into `documentation/static/api/`.
4. `gen:openapi` emits Docusaurus OpenAPI MDX pages for Core/Auth/ML.
5. `postprocess-openapi.js` cleans generated OpenAPI pages.
6. `gen:typedoc` emits frontend TypeDoc markdown.
7. `gen:docfx` builds .NET XML docs and DocFX output.
8. `gen:diagrams` emits generated architecture pages.
9. `sync-swagger-ui.js` copies local Swagger UI assets from
   `node_modules/swagger-ui-dist`.
10. `validate-generated-docs.js` fails the build if required generated artifacts
    are missing, empty, or placeholder output.

## Fail-fast Rules

Generated docs must not silently publish placeholders. The generators only allow
fallback output when `ALLOW_DOCS_FALLBACK=true` is explicitly set.

`validate-generated-docs.js` checks at least:

- `static/api/swagger.json`
- `static/api/auth-swagger.json`
- `static/api/ml-openapi.json`
- generated Core/Auth OpenAPI MDX pages
- frontend TypeDoc output
- generated diagram output
- DocFX output
- local Swagger UI assets

It also rejects OpenAPI specs with zero `paths` and rejects placeholder TypeDoc
or DocFX pages.

## OpenAPI Export Mode

Core API and Auth Service support a narrow documentation export mode through
`Documentation:OpenApiExport=true` / `Documentation__OpenApiExport=true`.

This mode exists so the swagger CLI can instantiate each service without local
runtime side effects:

- Logging skips `ReadFrom.Configuration(...)`, which avoids user-secret or local
  Serilog sink configuration breaking documentation export.
- Core API skips the BYOK LLM model seeder so OpenAPI export does not need a live
  Postgres connection.

Normal runtime behavior is unchanged when the flag is absent.

Both Core and Auth configure `CustomSchemaIds(type => type.FullName?.Replace('+',
'.') ?? type.Name)` so Swashbuckle does not collide on nested DTOs that share a
short class name.

## Interactive API

The portal includes a static Swagger UI at `/portal/swagger-ui/`. It is backed by
local files copied from `swagger-ui-dist`, not CDN URLs. The UI points at:

- `/api/swagger.json`
- `/api/auth-swagger.json`
- `/api/ml-openapi.json`

Swagger UI has `tryItOutEnabled: true` and `persistAuthorization: true`.

## DocFX

DocFX output is served at `/portal/docfx/index.html` (and direct `/docfx/` in the
nginx config). The DocFX landing page includes an explicit API Reference Index
with Core API, Auth Service, and Infrastructure namespace links because the
default dropdown does not scale well for this codebase.

## Known Verification State

On 2026-05-14, local direct swagger export succeeded for:

- Core API: 283 OpenAPI paths
- Auth Service: 17 OpenAPI paths

Local ML OpenAPI export could not be fully verified outside Docker because the
host Python was too new for `fastembed==0.4.2`. Docker builder installs
`ml-service/requirements.txt`, and the fail-fast validator will catch an empty ML
spec during the Docker build.
