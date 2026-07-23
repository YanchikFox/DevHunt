---
title: GitHub Actions — структура и назначение всех workflows
type: cross-cutting
status: verified
sources:
  - .github/workflows/ci.yml
  - .github/workflows/pr-checks.yml
  - .github/workflows/sonarqube.yml
  - .github/workflows/architecture.yml
  - .github/workflows/semgrep.yml
  - .github/workflows/trivy.yml
  - .github/workflows/depscan.yml
  - .github/workflows/iac-security.yml
  - .github/workflows/hadolint.yml
  - .github/workflows/powershell-lint.yml
  - .github/workflows/cd-production.yml
  - .github/workflows/cd-staging.yml
  - .github/workflows/mutation-testing.yml
  - .github/workflows/performance.yml
  - .github/workflows/contract-testing.yml
  - .github/workflows/visual-regression.yml
verified_at: 2026-05-18
verified_against_commit: 0d83e40
last_user_review: null
---

## Удалённые workflows (были избыточны)

- `build.yml` — дублировал `sonarqube.yml`, использовал устаревший .NET 8.0.x
- `cd.yml` — дублировал `cd-production.yml` + `cd-staging.yml` с теми же TODO-заглушками

## Auto-trigger на каждый PR

| Workflow | Инструмент | Что проверяет |
|---|---|---|
| `pr-checks.yml` | TruffleHog, npm audit, dotnet --vulnerable | Секреты, уязвимые зависимости, формат PR-title, большие файлы |
| `semgrep.yml` | Semgrep + `.semgrep/` custom rules | SAST, кастомные паттерны уязвимостей |
| `trivy.yml` | Trivy (fs scan) | CVE HIGH/CRITICAL в зависимостях |
| `architecture.yml` | dependency-cruiser + madge + NetArchTest | Циклические импорты, нарушения слоёв |

## Auto-trigger только при изменении инфра-файлов

- `hadolint.yml` — при изменении `**/Dockerfile*` или `.hadolint.yaml`
- `iac-security.yml` — при изменении `docker-compose*.yml`, `k8s/**`, `**/Dockerfile`
- `powershell-lint.yml` — при изменении `**/*.ps1`

## Только вручную (intentionally)

- `ci.yml` — полный CI: сборка + тесты всех 6 сервисов + docker build без пуша. Auto-trigger закомментирован для экономии минут.
- `sonarqube.yml` — SonarCloud. Требует `SONAR_TOKEN`. Проект: `YanchikFox_DevHunt` / org `yanchikfox`.
- `depscan.yml` — OWASP Dependency-Check. ~20 мин из-за NVD download. Убран PR-триггер — использовать перед релизом.
- `mutation-testing.yml` — Stryker для .NET и JS. Пороги: break 40% / low 60% / high 80%.
- `performance.yml` — k6 smoke + load против живого CoreApi (postgres+redis как services). Параметры: vus, duration.
- `contract-testing.yml` — Pact. Без `PACT_BROKER_BASE_URL`/`PACT_BROKER_TOKEN` публикация в брокер не происходит.
- `visual-regression.yml` — Chromatic (требует `CHROMATIC_PROJECT_TOKEN`) + Storybook a11y.

## CD

- `cd-production.yml` — деплой в prod. Триггер: push тега `v*.*.*` или вручную с параметром `version`. Собирает и пушит core-api + auth-service в GHCR. Реальный деплой-скрипт — TODO.
- `cd-staging.yml` — деплой на staging. Только вручную. Те же два образа. Реальный деплой — TODO.

## Секреты, которые нужно настроить

`SONAR_TOKEN`, `SEMGREP_APP_TOKEN` (без него — community rules), `CHROMATIC_PROJECT_TOKEN`,
`PACT_BROKER_BASE_URL`, `PACT_BROKER_TOKEN`, `STAGING_SSH_KEY`, `PRODUCTION_SSH_KEY`.

## Справочный файл

`.github/ACTIONS.md` — полное описание с примерами запуска для людей.
