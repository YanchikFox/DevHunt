# DevHunt - AI Agent Instructions

## Cursor Cloud specific instructions

### Architecture Overview

DevHunt is a polyglot microservices hackathon platform. See `README.md` for the repo map. Key services:

| Service                        | Tech       | Port | Notes                               |
| ------------------------------ | ---------- | ---- | ----------------------------------- |
| **db** (PostgreSQL + pgvector) | Docker     | 5432 | Must start first                    |
| **cache-service** (Redis)      | Docker     | 6379 | Session cache, SignalR backplane    |
| **message-broker** (RabbitMQ)  | Docker     | 5672 | Async events                        |
| **object-storage** (SeaweedFS) | Docker     | 8333 | S3-compatible file storage          |
| **auth-service**               | .NET 10    | 7001 | JWT auth, OAuth, TOTP 2FA           |
| **core-api**                   | .NET 10    | 7002 | Main REST API (Swagger at /swagger) |
| **frontend**                   | Next.js 16 | 3000 | i18n via next-intl                  |

### Starting the dev environment

1. **Infrastructure** (Docker): `docker compose up -d db cache-service message-broker object-storage`
2. **Wait for all containers to be healthy**: `docker compose ps`
3. **Database migrations**: Use `dotnet ef` (see gotchas below)
4. **Auth Service**: Run with env vars for connection strings, JWT, Redis (port 7001)
5. **Core API**: Run with env vars for connection strings, JWT, Redis, RabbitMQ, ObjectStorage, Encryption (port 7002)
6. **Frontend**: `cd frontend && npm run dev`

### Known gotchas

- **Running migrations**: `DevHunt.DatabaseMigrator` applies EF migrations cleanly (`dotnet run --project DevHunt.DatabaseMigrator` with `ConnectionStrings__DefaultConnection` pointing at `Host=localhost;Port=5432;...`). The old `Username` migration conflict is already resolved in code (the duplicate `AddColumn` in `20260225125026` is commented out), so no manual workaround is needed there.
- **EF-undiscoverable migrations (IMPORTANT)**: Four migrations lack both a `[Migration]` attribute and a `.Designer.cs`, so EF never applies them: `20260529000000_AddUserLlmModels`, `20260604180000_AddRefreshTokenFamilyId`, `20260614120000_RestoreTeamMemberUniqueIndex`, `20260614130000_AddShowcaseLikes`. The missing `RefreshTokens.TokenFamilyId` column makes **login fail with HTTP 500**. After running the migrator, apply the idempotent fix: `docker cp scripts/database/dev-fix-missing-migrations.sql devhunt-database-postgres:/tmp/fix.sql && docker exec -e PGPASSWORD=$POSTGRES_PASSWORD devhunt-database-postgres psql -U postgres -d devhunt_db -f /tmp/fix.sql`. That script also adds the model-snapshot-only columns `Projects.OpenRoles` (jsonb) and `Tasks.Tags` (varchar(500)).
- **Email verification / auto-verify**: Registration requires email verification, but there is no SMTP server in dev. Set `Email__SmtpHost=""` (empty) so the auth-service auto-verifies new users in Development (`ShouldAutoVerify()` = `IsDevelopment() && SmtpHost` empty). If auto-verify is silently skipped, check that `ASPNETCORE_ENVIRONMENT` is exactly `Development` — a stray trailing `\r` (from CRLF env files) makes `IsDevelopment()` false and also disables the dev CSRF bypass below. Alternatively verify a user directly: `UPDATE "Users" SET "IsEmailVerified"=true WHERE "Email"='...';`.
- **CSRF blocks UI mutations in non-Docker dev**: `Program.cs` registers `app.UseCsrfToken()` before `app.UseAuthentication()`, so antiforgery tokens bind to the anonymous user and every authenticated state-changing request (POST/PUT/PATCH/DELETE) fails with `AntiforgeryValidationException: ... different claims-based user`. This affects the web UI (e.g. "Create Project" shows "Security token expired"). For end-to-end backend testing, call `core-api` directly with a Bearer token and **no** CSRF cookie/header — in Development the global filter allows requests that send no antiforgery token at all. Reads (GET) work fine in the UI.
- **.NET 10 IISServerOptions**: `IISServerOptions` was removed in .NET 10. The `DevHunt.CoreApi/Program.cs` has a conditional compilation guard (`#if NET9_0_OR_GREATER && !NET10_0_OR_GREATER`) to skip this block. This is a no-op on Linux.
- **CoreApi.Tests**: Controller tests use `DevHuntTestDbContext` (SQLite/in-memory) to skip PostgreSQL-only mappings (`OpenRoles` jsonb, pgvector). Tests that call `BeginTransactionAsync` need SQLite, not EF InMemory. Unit/Integration folders are excluded unless `-p:RunUnitTests=true` / `-p:RunIntegrationTests=true`. Note: on the .NET 10 SDK, `dotnet test DevHunt.CoreApi.Tests` currently fails to **restore** with `NU1605` (package downgrade: `Microsoft.Extensions.Logging.Abstractions` 10.0.0 → 9.0.1 pulled transitively by `Microsoft.EntityFrameworkCore.InMemory` 9.0.1). `DevHunt.AuthService.Tests` and the frontend Vitest suites run fine.
- **Frontend .env.local (proxy mode)**: For non-Docker dev, the browser must call the same-origin Next.js proxy routes (`/api/proxy-core`, `/api/proxy-auth`) so the server side can inject the Bearer token from the next-auth session — a direct browser→core-api call has no way to send that token. Create `frontend/.env.local` with the **server-side** proxy targets and next-auth secret, and do NOT set `NEXT_PUBLIC_API_URL`/`NEXT_PUBLIC_AUTH_URL` (so the browser uses the proxy paths):

  ```bash
  AUTH_SERVICE_URL=http://localhost:7001
  CORE_SERVICE_URL=http://localhost:7002
  AUTH_SECRET=dev-auth-secret-minimum-32-characters   # must match auth-service
  NEXTAUTH_URL=http://localhost:3000
  NEXT_PUBLIC_APP_URL=http://localhost:3000
  NEXT_PUBLIC_SITE_URL=http://localhost:3000
  ```

- **Running backends outside Docker**: `dotnet run --project DevHunt.AuthService` (port 7001) and `dotnet run --project DevHunt.CoreApi` (port 7002) with `ASPNETCORE_URLS=http://+:<port>`. Use `localhost` (not the Docker service names) in every connection string: DB `Host=localhost;Port=5432`, Redis `localhost:6379`, RabbitMQ `amqp://devhunt:<pass>@localhost:5672`, ObjectStorage `http://localhost:8333`. OpenObserve/ML/analyzer/notification services are optional — core-api/auth-service log repeated connection errors for them (OTLP traces to `openobserve:5080`, a `CodeAnalysisEmbeddingJobWorker` URI error) but stay healthy.
- **Docker socket permissions**: Run `sudo chmod 666 /var/run/docker.sock` if docker commands fail with permission errors.
- **Docker daemon**: Start with `sudo dockerd &>/tmp/dockerd.log &` if not already running. Needs `fuse-overlayfs` storage driver and `iptables-legacy` for nested containers. On Docker 29+, also disable the containerd snapshotter so `fuse-overlayfs` is used — `/etc/docker/daemon.json` should be `{"storage-driver":"fuse-overlayfs","features":{"containerd-snapshotter":false}}`.
- **SeaweedFS config**: `object-storage` bind-mounts `./seaweedfs/s3.json` (gitignored). If that file does not exist before the container first starts, Docker creates it as a **directory** and SeaweedFS crash-loops. Create it from `seaweedfs/s3.json.example` (filling in `OBJECT_STORAGE_ACCESS_KEY`/`OBJECT_STORAGE_SECRET_KEY`) before `docker compose up`, and if a stale dir was created, `sudo rm -rf seaweedfs/s3.json`, recreate the file, then `docker compose up -d --force-recreate object-storage`.

### Running tests

- .NET Auth tests: `dotnet test DevHunt.AuthService.Tests`
- Frontend unit tests: `cd frontend && npx vitest run tests/unit`
- Frontend lint: `cd frontend && npm run lint`
- .NET build check: `dotnet build DevHunt.slnx`

### Environment variables

All secrets are provided via environment variables. See `docker-compose.yml` for the full list. The `.env` file (gitignored) holds dev secrets. Key required vars:

- `POSTGRES_PASSWORD`, `RABBITMQ_DEFAULT_PASS`, `JWT_KEY` (min 32 chars), `ENCRYPTION_KEY` (32 chars), `ENCRYPTION_IV` (16 chars)

---

## Git & Authorship Standards

### No AI co-author lines — ever

**Never** append any AI co-author attribution to commits:

```
# ❌ NEVER do this
Co-authored-by: Claude <noreply@anthropic.com>
Co-authored-by: GitHub Copilot <copilot@github.com>
Co-authored-by: cursor <cursor@cursor.com>
```

Commits are authored by the human developer who reviews and accepts the change.

### AI must not act on git autonomously

An AI agent **must not** execute any of the following without an explicit, specific request from the user in the current turn:

- `git push` / `git push --force` / `git push --force-with-lease`
- `gh pr merge` / `git merge` / `git rebase` (interactive or otherwise)
- `git tag` followed by push
- Any modification of `~/.gitconfig`, `.git/config`, or global git hooks under `~/.config/git/`

If the user says "commit and push" in one sentence, treat it as two separate confirmations: commit first, show the result, then push only if the user confirms. When in doubt, stop and ask.

### Conventional Commits with mandatory scope

Format: `type(scope): short imperative description`

| type       | when                                               |
| ---------- | -------------------------------------------------- |
| `feat`     | new capability visible to users                    |
| `fix`      | bug fix                                            |
| `refactor` | code change without behavior change                |
| `perf`     | performance improvement                            |
| `test`     | test-only changes                                  |
| `docs`     | documentation only (memory/, prompts/, playbooks/) |
| `chore`    | tooling, deps, CI, config                          |
| `ci`       | GitHub Actions / CI pipeline changes               |

**Scope must be one of the real service/module names:**

| Scope          | Maps to                                            |
| -------------- | -------------------------------------------------- |
| `auth`         | `DevHunt.AuthService/`                             |
| `core-api`     | `DevHunt.CoreApi/`                                 |
| `infra`        | `DevHunt.Infrastructure/`, `k8s/`, `nginx/`        |
| `frontend`     | `frontend/`                                        |
| `ml`           | `ml-service/`                                      |
| `notification` | `notification-service/`                            |
| `integration`  | `integration-gateway/`                             |
| `db`           | migrations in `DevHunt.Infrastructure/Migrations/` |
| `dx`           | tooling, scripts, agent config files               |
| `docs`         | `memory/`, `prompts/`, `documentation/`            |

```bash
# ✅ Correct
feat(auth): add TOTP 2FA with time-window tolerance
fix(frontend): resolve RTL overflow in project card
refactor(core-api): extract ProjectQueryService from ProjectsController
perf(ml): cache embedding vectors in Redis with 1h TTL
chore(infra): upgrade PostgreSQL image to 16.3

# ❌ Wrong — no scope, vague description, past tense
feat: added some stuff
fix: fixed the bug
update: changes
```

Commit **body** (optional): explain WHY the change was made, not WHAT changed (the diff shows the what).

---

## Code Comment Standards

### C# XML doc comments (DocFX-compatible)

Every `public` type member in `DevHunt.AuthService/`, `DevHunt.CoreApi/`, and `DevHunt.Infrastructure/` must have XML doc.

```csharp
// ✅ Correct — explains what, why, and edge cases
/// <summary>
/// Generates a signed JWT for the given user and stores a refresh token.
/// </summary>
/// <param name="user">The authenticated user. Must not be null.</param>
/// <param name="ct">Propagates cancellation from the HTTP request pipeline.</param>
/// <returns>
/// A <see cref="TokenPair"/> containing the access token (15 min) and
/// refresh token (7 days). Returns null if the user account is locked.
/// </returns>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is null.</exception>
public async Task<TokenPair?> IssueTokensAsync(ApplicationUser user, CancellationToken ct)

// ❌ Wrong — restates the method name, omits all useful info
/// <summary>Issues tokens.</summary>
/// <param name="user">The user.</param>
/// <returns>The result.</returns>
public async Task<TokenPair?> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
```

Rules:

- `<summary>` — one or two sentences, plain prose, ends with period
- `<param>` — what it _represents_, not just its type; note nullability and constraints
- `<returns>` — what is returned and under what conditions (including null)
- `<exception cref="…">` — every exception the method intentionally throws
- Never cite "SRS v1.0" or any document — the code is the truth
- Prose comments must explain WHY or non-obvious behavior — never restate the code

### TypeScript/JavaScript JSDoc (Next.js frontend)

Every exported function or React component in `frontend/src/` must have JSDoc.

```typescript
// ✅ Correct
/**
 * Displays a project card with join/leave controls.
 *
 * @param project - Project data including role counts and visibility.
 * @param onSelect - Called with the project ID when the card is clicked.
 * @returns A focusable card element or null when project is archived.
 *
 * @example
 * <ProjectCard project={data} onSelect={(id) => router.push(`/projects/${id}`)} />
 */
export function ProjectCard({ project, onSelect }: ProjectCardProps) { … }

// ❌ Wrong — empty doc, missing @param, not explaining edge case
/** Project card */
export function ProjectCard(props: any) { … }
```

### General comment rules (all languages)

- Comments explain **WHY** or non-obvious **WHAT** — never transcribe the code
- `// TODO(DH-123): …` — every TODO must link a Linear issue; bare `TODO` is a lint error
- `// FIXME` — same rule; link a Linear issue or delete it
- No commented-out dead code — use `git rm` or just delete it

---

## Security Standards

### Secrets & credentials

```bash
# ❌ Never hardcode — gets committed, leaks in logs
connection_string = "Host=db;Password=hunter2"
api_key = "sk-live-abc123"

# ✅ Always use env vars or secret managers
connection_string = os.environ["DATABASE_URL"]
api_key = os.environ["GEMINI_API_KEY"]
```

Required env vars for each service are in `docker-compose.yml`. Dev secrets live in `.env` (gitignored). Never commit `.env`.

### SQL injection prevention

```csharp
// ❌ Never concatenate user input into SQL
var sql = $"SELECT * FROM Users WHERE Username = '{input}'";

// ✅ Use EF Core (parameterized by default) or FromSqlRaw with params
var user = await _db.Users.Where(u => u.Username == input).FirstOrDefaultAsync(ct);
var user = await _db.Users.FromSqlRaw("SELECT * FROM Users WHERE Username = {0}", input)
                          .FirstOrDefaultAsync(ct);
```

Raw SQL in `DevHunt.Infrastructure/` repositories only — never in controllers or services.

### Authentication & authorization

- JWT keys: minimum **32 characters** (`JWT_KEY` env var)
- Validate `CancellationToken` propagation on all token-issuing paths
- Authorization attributes (`[Authorize]`, `[RequireRole]`) on every non-public endpoint
- Never return raw stack traces in API error responses

### CORS

```csharp
// ❌ Never use wildcard in production
builder.Services.AddCors(o => o.AddPolicy("Default",
    p => p.AllowAnyOrigin())); // NEVER

// ✅ Explicit allowed origins from env var
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]!.Split(',');
builder.Services.AddCors(o => o.AddPolicy("Default",
    p => p.WithOrigins(allowedOrigins).AllowCredentials()));
```

### Logging

```csharp
// ❌ Never use Console — not structured, not correlated
Console.WriteLine($"User {userId} logged in");
Debug.WriteLine("token: " + rawToken);    // leaks token to debug output!

// ✅ Use ILogger<T> with structured properties
_logger.LogInformation("User {UserId} authenticated via {Provider}", userId, provider);
```

Never log passwords, tokens, raw JWTs, PII (email, phone), or connection strings.

### Error handling

```csharp
// ❌ Swallowing exceptions — silent failures are the worst bugs
try { await SomethingAsync(ct); }
catch (Exception) { } // NEVER

// ✅ Log context and either recover or rethrow
try { await SomethingAsync(ct); }
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to {Operation} for project {ProjectId}", op, id);
    throw; // or return Result.Failure(...)
}
```

### Input validation

Server-side validation is **mandatory** for all endpoints, regardless of client-side validation:

- Use FluentValidation for .NET endpoints (`DevHunt.CoreApi/Validators/`)
- Use Zod schemas for Next.js API routes and forms (`frontend/src/lib/schemas/`)
- Never trust `Content-Type` header alone — validate the parsed payload

---

## Clean Code Standards

These thresholds are enforced by CodeScene. Violations appear in `codescene-devhunt-issues.json`.

### Method/function length — 40 lines max

```csharp
// ❌ 80-line method mixing DB query, business logic, and response shaping
public async Task<ActionResult> CreateProject(CreateProjectDto dto, CancellationToken ct)
{
    // 80 lines of mixed concerns...
}

// ✅ Thin controller action; concerns delegated
public async Task<ActionResult<ProjectDto>> CreateProjectAsync(
    CreateProjectDto dto, CancellationToken ct)
{
    var result = await _projectService.CreateAsync(dto, ct);
    return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
}
```

### Cyclomatic complexity — 10 max per method

Reduce branching with early returns, guard clauses, and dispatch tables:

```typescript
// ❌ CC = 15
function processTask(task: Task) {
  if (task.status === "new") {
    if (task.priority === "high") {
      if (task.assignee) {
        /* … */
      } else {
        /* … */
      }
    } else {
      /* … */
    }
  } else if (task.status === "progress") {
    /* … */
  }
}

// ✅ CC = 3 — dispatch table + extracted handlers
const handlers: Record<TaskStatus, (t: Task) => void> = {
  new: handleNewTask,
  progress: handleInProgressTask,
  done: handleDoneTask,
};
function processTask(task: Task) {
  handlers[task.status]?.(task);
}
```

### Class size — max 7 public methods

More than 7 usually means a class has more than one responsibility (SRP violation). Extract a focused collaborator.

Known hotspots that need attention:

| File                                                | Issue                                       |
| --------------------------------------------------- | ------------------------------------------- |
| `DevHunt.CoreApi/Controllers/ProjectsController.cs` | Constructor over-injection, complex methods |
| `DevHunt.CoreApi/Controllers/UsersController.cs`    | Many conditionals                           |
| `frontend/src/app/projects/[id]/page.tsx`           | File size > 500 LoC, large methods          |
| `ml-service/main.py`                                | Many conditionals, large methods            |

### Nesting depth — 3 levels max

Use early returns / guard clauses:

```csharp
// ❌ 4-level nesting
if (user != null) {
    if (user.IsActive) {
        if (project != null) {
            if (project.IsPublic) { return project; }
        }
    }
}

// ✅ Guard clauses — flat and readable
if (user is null || !user.IsActive) return Unauthorized();
if (project is null) return NotFound();
if (!project.IsPublic) return Forbid();
return Ok(project);
```

### Avoid boolean parameters — use enums

```csharp
// ❌ Boolean blindness — caller can't tell what true means
await SendNotificationAsync(userId, message, true, false);

// ✅ Named enum values — self-documenting
await SendNotificationAsync(userId, message,
    channel: NotificationChannel.Email,
    priority: NotificationPriority.Normal);
```

### Magic values — use named constants

```typescript
// ❌
if (score > 0.85) {
  /* … */
}
setTimeout(refresh, 300000);

// ✅
const RECOMMENDATION_CONFIDENCE_THRESHOLD = 0.85;
const DASHBOARD_REFRESH_INTERVAL_MS = 5 * 60 * 1000;
```

---

## Documentation Format Standards (Docusaurus-compatible)

Applies to files under `documentation/`, `memory/`, and `prompts/`.

- **Single `#` title per file** — only one `h1` at the top
- **Heading hierarchy** — never jump from `h2` to `h4`; use `h2 → h3 → h4`
- **Code blocks must specify language:**

  ````
  ```csharp
  ```typescript
  ```yaml
  ```bash
  ```python
  ```json
  ````

- **Admonitions** use Docusaurus syntax:

  ```markdown
  :::note
  Informational context.
  :::

  :::warning
  Something that can go wrong.
  :::

  :::danger
  Irreversible or security-critical action.
  :::

  :::tip
  Helpful shortcut or best practice.
  :::
  ```

- **Internal links** use relative paths, not absolute URLs:
  - ✅ `[Auth service](../systems/auth-service.md)`
  - ❌ `[Auth service](http://localhost:9000/docs/auth-service)`

- **Tables** must have header rows and use pipe alignment.

---

## Frontend Standards (Next.js / React)

### No hardcoded user-visible strings

```typescript
// ❌ Breaks i18n
<h1>Your Projects</h1>
<button>Join Team</button>

// ✅ Use next-intl
const t = useTranslations('projects');
<h1>{t('title')}</h1>
<button>{t('joinTeam')}</button>
```

Translation keys live in `frontend/messages/`.

### JSDoc on all exported components

Every exported React component must document its props:

```typescript
/**
 * Badge showing a user's earned achievement.
 *
 * @param badge - Badge metadata including icon URL and earned date.
 * @param size - Render size: 'sm' for list views, 'lg' for profile page.
 */
export function AchievementBadge({ badge, size = 'sm' }: AchievementBadgeProps) { … }
```

### Accessibility requirements

- Every `<button>` and `<a>` must have visible label text or `aria-label`
- Form inputs need `<label>` or `aria-labelledby`
- Images need `alt` text (empty `alt=""` for decorative images)
- Interactive components need keyboard support (`onKeyDown` or semantic HTML)

### No `any` type

```typescript
// ❌
const data: any = response.json();
function process(input: any) { … }

// ✅
const data: unknown = response.json();
function process(input: ProjectDto) { … }
// For truly unknown shapes, narrow with type guards
```

### Error boundaries

Every page-level component must be wrapped in an error boundary. Sections with independent data sources (e.g., sidebar, recommendations panel) should have their own boundary so one failure doesn't blank the whole page.

---

## Backend Standards (.NET 10)

### All async methods: `Async` suffix + `CancellationToken`

```csharp
// ❌
public Task<Project> GetProject(int id)
public async Task SaveChanges()

// ✅
public Task<Project?> GetProjectAsync(int id, CancellationToken ct)
public async Task SaveChangesAsync(CancellationToken ct)
```

`CancellationToken` must be the **last** parameter and must be forwarded to all `await` calls.

### Use `ILogger<T>` — never `Console`

```csharp
// ❌ Not structured, not correlated, not searchable in OpenObserve
Console.WriteLine("Project created: " + project.Id);

// ✅ Structured, correlated, shows up in OpenObserve dashboards
_logger.LogInformation("Project {ProjectId} created by user {UserId}", project.Id, userId);
```

### Repository pattern

Raw SQL and EF queries belong in `DevHunt.Infrastructure/Repositories/` only. Controllers call services; services call repositories.

```csharp
// ❌ Query in controller
[HttpGet("{id}")]
public async Task<ActionResult<Project>> Get(int id, CancellationToken ct)
{
    var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
    return project is null ? NotFound() : Ok(project);
}

// ✅ Controller delegates to service, service delegates to repo
[HttpGet("{id}")]
public async Task<ActionResult<ProjectDto>> GetAsync(int id, CancellationToken ct)
{
    var dto = await _projectService.GetByIdAsync(id, ct);
    return dto is null ? NotFound() : Ok(dto);
}
```

### Swagger documentation

Every `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]` action must have:

```csharp
/// <summary>Returns a single project by ID.</summary>
/// <param name="id">The project's primary key.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>The project DTO, or 404 if not found.</returns>
/// <response code="200">Project found and returned.</response>
/// <response code="404">No project with the given ID.</response>
[HttpGet("{id}")]
[ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<ProjectDto>> GetAsync(int id, CancellationToken ct)
```

### Result pattern over exceptions in business logic

```csharp
// ❌ Throwing exceptions for expected business outcomes
public async Task JoinProjectAsync(int userId, int projectId, CancellationToken ct)
{
    if (await _repo.IsMemberAsync(userId, projectId, ct))
        throw new InvalidOperationException("Already a member");
}

// ✅ Return a discriminated result
public async Task<Result> JoinProjectAsync(int userId, int projectId, CancellationToken ct)
{
    if (await _repo.IsMemberAsync(userId, projectId, ct))
        return Result.Failure("User is already a project member");
    await _repo.AddMemberAsync(userId, projectId, ct);
    return Result.Success();
}
```
