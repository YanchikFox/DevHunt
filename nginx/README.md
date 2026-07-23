# DevHunt API Gateway (Nginx)

Nginx reverse proxy that is the single public entry point. Routes:
- `/` → `frontend:3000`
- `/api/*` → `core-api:8080`
- `/auth/*` → `auth-service:8080`

Handles TLS termination; self-signed dev certs are generated at image build time.

| Image | Dockerfile | Config |
|-------|------------|--------|
| Dev / HTTP-only | `Dockerfile` | `nginx-http-only.conf` + `edge-security.inc` |
| Production TLS | `Dockerfile.prod` | `nginx.conf` + `edge-security.inc` |

`docker-compose.prod.yml` builds `api-gateway` from `Dockerfile.prod`.

## Local development

```bash
docker compose up api-gateway
```

HTTP: `http://localhost` (port 80 → container 8080)  
HTTPS (prod image): `https://localhost` (port 443 → container 8443)
