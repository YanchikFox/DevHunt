# GitHub Actions — Reference

Brief overview of all workflows in `.github/workflows/`. Selection principle: auto-triggers only where fast and necessary; slow/expensive ones — manual only.

---

## Workflows by Category

### CI / Code Quality

#### `ci.yml` — CI Pipeline
**When it runs:** manual only (`workflow_dispatch`)
> Auto-trigger is commented out to save CI minutes. Run before merging large changes.

**What it does:**
- Builds and tests backend (.NET 10), frontend (Next.js), ml-service (Python), notification-service, and integration-gateway (Node.js)
- Scans dependencies for vulnerabilities (Trivy + npm audit + dotnet --vulnerable)
- Builds Docker images for all 6 services without push (verifies Dockerfile is not broken)

**Parameters:**
- `verbose_logs` — uploads logs from all jobs as artifacts (useful when failing)

**How to run:**
```
Actions → CI Pipeline → Run workflow [branch] [verbose_logs: true/false]
```

---

#### `pr-checks.yml` — PR Checks
**When it runs:** automatically on every PR

**What it does:**
- Validates PR title format (semantic commits: `feat:`, `fix:`, `chore:`, etc.)
- Checks for merge conflicts
- Blocks files >10 MB
- Scans PR diff for secret leaks (TruffleHog)
- Runs npm audit and dotnet --vulnerable for all Node.js and .NET packages
- Runs .NET tests with coverage and comments on PR

**How to use:** nothing to do — runs automatically. If it fails, read the job output for the specific reason.

---

#### `build.yml` — **deleted**
Was a duplicate of `sonarqube.yml` and used outdated .NET 8.0.x.

#### `sonarqube.yml` — SonarCloud Analysis
**When it runs:** manual only (`workflow_dispatch`)
> Requires `SONAR_TOKEN` secret. Results appear on [sonarcloud.io](https://sonarcloud.io) in project `YanchikFox_DevHunt`.

**What it does:** comprehensive code quality analysis via `dotnet-sonarscanner begin/end` — code smells, duplications, coverage trends. Builds .NET, frontend, Python.

**How to run:**
```
Actions → SonarCloud Analysis → Run workflow
```

---

#### `architecture.yml` — Architecture Checks
**When it runs:** automatically on PR to `main` / `develop`

**What it does:**
- **JS/TS:** `dependency-cruiser` (validates dependencies per rules in `.dependency-cruiser.js`) + `madge` (finds circular imports) for `frontend/`, `notification-service/`, `integration-gateway/`
- **.NET:** `dotnet test --filter "Category=Architecture"` — runs NetArchTest tests from `DevHunt.CoreApi.Tests/Architecture/`

**Parameters:** `verbose_logs` for artifacts with logs.

---

### Security

#### `semgrep.yml` — Semgrep SAST
**When it runs:** automatically on every PR

**What it does:** static code analysis for vulnerabilities.
- First validates custom rules from `.semgrep/`
- If `SEMGREP_APP_TOKEN` is set — runs cloud rules via Semgrep Platform
- Always runs local DevHunt rules (`.semgrep/`)
- Fallback without token: `semgrep --config auto`

Results are uploaded to GitHub Security tab (if Advanced Security is enabled) and always as artifacts `semgrep-results-<run_id>`.

**How to configure:** add `SEMGREP_APP_TOKEN` secret from [semgrep.dev](https://semgrep.dev) for cloud rules.

---

#### `trivy.yml` — Trivy Security Scan
**When it runs:** automatically on every PR

**What it does:** scans entire repository (filesystem scan) for CVEs with severity HIGH and CRITICAL. Outputs report to log and uploads as artifact.

---

#### `depscan.yml` — Dependency Scan (OWASP)
**When it runs:** manual only (`workflow_dispatch`)
> Downloads NVD database (~2 GB), takes ~15–20 minutes. Run before release or weekly. Trivy does this on every PR — faster.

**What it does:** OWASP Dependency-Check across all project dependencies. Generates detailed HTML/JSON/SARIF report in artifacts (`dependency-check-report`, stored 30 days).

**CVSS parameter:** fails on vulnerabilities with CVSS ≥ 7 (but `continue-on-error: true`, so does not block).

---

#### `iac-security.yml` — IaC Security (Checkov + Kubesec + KICS)
**When it runs:** automatically on PR if `docker-compose*.yml`, `k8s/**`, or `**/Dockerfile` changed

**What it does:**
- **Checkov** — scans Dockerfile and k8s manifests for known misconfigurations
- **Kubesec** — analyzes k8s manifests from `k8s/` by security score
- **KICS** — broad multi-IaC scanner (Dockerfile, compose, k8s)

Results in SARIF (GitHub Security tab) and as artifacts.

---

#### `hadolint.yml` — Dockerfile Lint
**When it runs:** automatically on PR if `**/Dockerfile*` or `.hadolint.yaml` changed

**What it does:** lints all Dockerfiles via hadolint (best practices, shell issues). Config in `.hadolint.yaml`.

---

### CD / Deployment

#### `cd-production.yml` — Deploy to Production
**When it runs:**
- Automatically on push of tag `v*.*.*` (e.g., `git tag v1.2.3 && git push --tags`)
- Manually with version specified

**What it does:** builds and pushes Docker images for `core-api` and `auth-service` to GHCR with version tags and `latest`. Deployment step is a placeholder (TODO: real Blue-Green strategy script).

**Parameters (when running manually):**
- `version` — version tag, e.g., `v1.2.3`

**Required secrets:** `GITHUB_TOKEN` (automatic), `PRODUCTION_SSH_KEY` (reserved for when real deploy logic is wired up)

**How to release:**
```bash
git tag v1.2.3
git push origin v1.2.3
# Actions → CD - Deploy to Production will run automatically
```

---

#### `cd-staging.yml` — Deploy to Staging
**When it runs:** manual only (`workflow_dispatch`)

**What it does:** builds and pushes `core-api` and `auth-service` to GHCR with tag `<sha>`. Deployment to staging is a placeholder (TODO: real kubectl/docker-compose commands).

**Required secrets:** `GITHUB_TOKEN` (automatic), `STAGING_SSH_KEY` (reserved for when real deploy logic is wired up)

**How to run:**
```
Actions → CD - Deploy to Staging → Run workflow
```

---

#### `cd.yml` — **deleted**
Duplicated `cd-production.yml` + `cd-staging.yml` with the same TODO placeholders.

---

### Advanced Testing (manual only)

#### `mutation-testing.yml` — Mutation Testing (Stryker)
**When it runs:** manual only — too slow for every PR.

**What it does:** Stryker.NET for `DevHunt.CoreApi` and `DevHunt.AuthService`, Stryker.js for frontend. Mutates code and checks that tests catch it.

**Thresholds:** break 40% / low 60% / high 80%. Reports in HTML/JSON/Markdown (artifacts, 30 days).

**How to run:**
```
Actions → Mutation Testing → Run workflow
```
Expect 30–90 minutes depending on codebase size.

---

#### `performance.yml` — Performance Testing (k6)
**When it runs:** manual only.

**What it does:** spins up PostgreSQL + Redis as services, builds and runs CoreApi, then runs k6 smoke-test and load-test from `tests/performance/`.

**Parameters:**
- `vus` — virtual users (default: 10)
- `duration` — length (default: `30s`, example: `5m`)
- `verbose_logs`

**How to run:**
```
Actions → Performance Testing (k6) → Run workflow → vus=50, duration=2m
```

---

#### `contract-testing.yml` — Contract Testing (Pact)
**When it runs:** manual only.

**What it does:** consumer tests on frontend (Pact), provider verification on CoreApi and AuthService. Verifies API contract compatibility between frontend and backend.

**Required secrets:** `PACT_BROKER_BASE_URL`, `PACT_BROKER_TOKEN` — without them, tests run locally and pacts are not published to broker.

**How to run:**
```
Actions → Contract Testing (Pact) → Run workflow
```

---

#### `visual-regression.yml` — Visual Regression (Chromatic)
**When it runs:** manual only.

**What it does:**
- **Chromatic:** compares Storybook stories against baseline, publishes diff. `onlyChanged: true` — only changed stories.
- **Storybook a11y:** builds Storybook and runs `@storybook/test-runner` for accessibility checks.

**Required secrets:** `CHROMATIC_PROJECT_TOKEN`

**How to run:**
```
Actions → Visual Regression Testing (Chromatic) → Run workflow
```

---

### Infrastructure File Linting

#### `powershell-lint.yml` — PowerShell Lint
**When it runs:** automatically on PR if `**/*.ps1` changed

**What it does:** runs PSScriptAnalyzer on `scripts/` at Error severity. Scripts: `check-env.ps1`, `dev-rebuild.ps1`, `collect-ci-logs.ps1`, `create-s3-iam-config.ps1`.

---

## Quick Reference

| Goal | Workflow |
|---|---|
| Verify PR did not break build | `CI Pipeline` (manual) |
| Release to production | `git push origin v1.2.3` |
| Deploy to staging | `CD - Deploy to Staging` (manual) |
| Code quality analysis | `SonarCloud Analysis` (manual) |
| Deep dependency audit | `Dependency Scan` (manual) |
| Check test quality | `Mutation Testing` (manual) |
| Load test | `Performance Testing (k6)` (manual) |
| Check UI for regressions | `Visual Regression Testing` (manual) |
| Verify API contracts | `Contract Testing (Pact)` (manual) |

## Secrets to Configure

| Secret | Used in | Required |
|---|---|---|
| `SONAR_TOKEN` | `sonarqube.yml` | for SonarCloud |
| `SEMGREP_APP_TOKEN` | `semgrep.yml` | for cloud rules (without it — community) |
| `CHROMATIC_PROJECT_TOKEN` | `visual-regression.yml` | required |
| `PACT_BROKER_BASE_URL` + `PACT_BROKER_TOKEN` | `contract-testing.yml` | for publishing pacts |
| `STAGING_SSH_KEY` | `cd-staging.yml` | reserved for when real deploy logic is wired up |
| `PRODUCTION_SSH_KEY` | `cd-production.yml` | reserved for when real deploy logic is wired up |
