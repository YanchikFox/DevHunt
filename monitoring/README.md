# Monitoring Stack

Observability for the DevHunt platform. Runs as a separate compose file alongside the main stack.

## Starting

```bash
docker compose -f docker-compose.monitoring.yml up -d
```

OpenObserve UI: `http://localhost:5080`  
Default credentials: `root@devhunt.com` / `devhunt123`

## What it covers

All .NET services export Serilog + OpenTelemetry to OpenObserve. Prometheus scrapes `/metrics` from every service. Alert rules are in `monitoring/alerts.yml`; the init container (`openobserve-init`) applies them on first boot.
