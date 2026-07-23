# Dependency advisory SLA

| Severity | Response target | CI gate |
|----------|-----------------|--------|
| Critical | 48 hours | `npm audit --audit-level=high`, `dotnet list package --vulnerable` in `pr-checks.yml` |
| High | 1 week | Same gates (high and above block merge) |
| Moderate | Next scheduled dependency sweep | Logged in CI; does not block unless promoted |

## Verification commands

```bash
cd frontend && npm audit --audit-level=high
cd integration-gateway && npm audit --audit-level=high
cd notification-service && npm audit --audit-level=high
dotnet list package --vulnerable --include-transitive
```

Central .NET versions: `Directory.Packages.props`. Node services: bump `package.json` + `npm install` to refresh lockfiles.
