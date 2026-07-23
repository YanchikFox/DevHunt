---
title: Observability — Serilog, OpenTelemetry, Prometheus
type: cross-cutting
status: verified
sources:
  - DevHunt.CoreApi/Extensions/LoggingExtensions.cs
  - DevHunt.CoreApi/Extensions/OpenTelemetryExtensions.cs
  - DevHunt.CoreApi/Middleware/MetricsMiddleware.cs
  - DevHunt.CoreApi/Middleware/CorrelationIdMiddleware.cs
  - DevHunt.CoreApi/Middleware/LogEnrichmentMiddleware.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-10
verified_against_commit: 866e2a8
last_user_review: null
---

## What it is

Three independent observability layers active on Core API:
structured logging (Serilog), distributed tracing (OpenTelemetry),
and metrics scraping (Prometheus). All three export to
**OpenObserve** as the single observability backend.

## Components

| Class | Location | Role |
|---|---|---|
| `LoggingExtensions.ConfigureSerilog` | Extensions/LoggingExtensions.cs:18 | wires Serilog with console + HTTP sink |
| `OpenObserveHttpClient` | Extensions/LoggingExtensions.cs:52 | custom Serilog HTTP client with Basic Auth |
| `OpenTelemetryExtensions.AddOpenTelemetryTracing` | Extensions/OpenTelemetryExtensions.cs:18 | wires OTLP exporter to OpenObserve |
| `MetricsMiddleware` | Middleware/MetricsMiddleware.cs | Prometheus counters + histograms per request |
| `CorrelationIdMiddleware` | Middleware/CorrelationIdMiddleware.cs | propagates X-Correlation-ID across services |
| `LogEnrichmentMiddleware` | Middleware/LogEnrichmentMiddleware.cs | pushes TraceId + UserId to Serilog LogContext |

## How signals flow

### Logs (Serilog)

```
Program.cs → builder.Host.UseSerilog(LoggingExtensions.ConfigureSerilog)
  → reads structured config via ReadFrom.Configuration
  → enriches all logs: application="core-api", environment, service
  → sinks:
      Console (always) — human-readable with timestamp
      OpenObserve HTTP sink (conditional):
        OpenObserve:LogsEndpoint + User + Password must all be set
        → RenderedCompactJsonFormatter (CLEF)
        → queueLimitBytes=1_000_000 (1 MB in-memory queue)
        → OpenObserveHttpClient: new HttpClient() at startup (intentional,
          DI unavailable in pre-DI logging context — nosemgrep annotation present)
```

`CorrelationIdMiddleware` → pushes CorrelationId to Serilog scope
`LogEnrichmentMiddleware` → pushes TraceId + UserId (⚠ see Known traps)

### Traces (OpenTelemetry)

```
AddOpenTelemetryTracing → WithTracing:
  AddAspNetCoreInstrumentation (RecordException=true)
  AddHttpClientInstrumentation
  AddOtlpExporter("openobserve"):
    endpoint: OpenObserve:OtlpTracesEndpoint (default: https://openobserve:5080/api/default/otlp/v1/traces)
    protocol: HttpProtobuf
    auth: Basic via OpenObserve:User + Password headers
```

Service name: `Tracing:ServiceName` config key (default: "DevHunt.CoreApi").

### Metrics (Prometheus)

`MetricsMiddleware` (step 6 of pipeline) measures every request:

| Metric | Type | Labels |
|---|---|---|
| `http_requests_total` | Counter | method, endpoint, status_code |
| `http_request_duration_seconds` | Histogram | method, endpoint |
| `support_tickets_created_total` | Counter | — |
| `feedback_items_created_total` | Counter | type |
| `project_issues_created_total` | Counter | type |
| `avatar_uploads_total` | Counter | — |

Path normalization applied in `GetEndpointPath` to avoid high
cardinality (e.g. `/api/support/tickets/{id}`). The
normalization covers only a few specific patterns — all other
paths are passed through verbatim, which can still produce
high cardinality for routes with IDs.

Metrics endpoint: `GET /metrics` — custom access guard
(localhost IPs or `METRICS_TOKEN` header).

## Configuration keys

| Key | Purpose |
|---|---|
| `OpenObserve:LogsEndpoint` | Serilog HTTP sink destination |
| `OpenObserve:OtlpTracesEndpoint` | OTLP traces exporter URL |
| `OpenObserve:User` | Basic auth username (both sinks) |
| `OpenObserve:Password` | Basic auth password (both sinks) |
| `Tracing:ServiceName` | Service label in trace spans |

All three OpenObserve keys must be non-empty for the HTTP
sink to activate. If any is absent, Serilog logs console-only.

## Known traps

- **`LogEnrichmentMiddleware` runs before `UseAuthentication`
  (pipeline step 5 vs 10).** `UserId` is always "anonymous"
  at the point the Serilog scope is established, even for
  authenticated requests. See
  [cross-cutting/middleware-pipeline.md](middleware-pipeline.md).
- **`MetricsMiddleware` path normalization is incomplete.**
  Routes not explicitly listed in `GetEndpointPath` emit the
  full path including entity IDs (e.g. `/api/projects/{guid}`
  as a verbatim string). High cardinality accumulates over time
  in the Prometheus registry for endpoints with unique IDs in
  the path.
- **OpenObserve HTTP sink uses a bare `new HttpClient()`.**
  The `nosemgrep` annotation confirms this is intentional
  (Serilog HTTP client is initialized before the DI container
  is built). This HttpClient is not managed by `IHttpClientFactory`
  and does not share connection pools.

## What I should NOT assume

- **Traces and logs share the same OpenObserve instance but
  different endpoints.** `LogsEndpoint` and `OtlpTracesEndpoint`
  are separate config keys. Configuring one without the other
  leaves the other sink disabled.
- **There is no separate Jaeger exporter.** A comment in
  OpenTelemetryExtensions.cs:38 explicitly states "Jaeger
  exporter removed - using OpenObserve for all observability."
  Don't add Jaeger config expecting it to be picked up.
