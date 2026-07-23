---
title: api-gateway — Nginx TLS-terminating reverse proxy
type: system
status: verified
sources:
  - nginx/nginx.conf
  - nginx/Dockerfile
  - nginx/create-dev-cert.sh
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Nginx-based edge proxy. Terminates TLS, applies global security
headers, fans traffic out to `frontend`, `auth-service`, and
`core-api`, and is the only place that handles the WebSocket
upgrade for SignalR hubs.

Compose service `api-gateway`, exposing host ports
`PORT_API_GATEWAY` (default 80) and `PORT_API_GATEWAY_HTTPS`
(default 443), forwarding to internal 8080/8443.

A second config — `nginx/nginx-http-only.conf` — exists for
HTTP-only deployments; not described here.

## "Entry-point"

The Nginx configuration itself:
[nginx/nginx.conf](nginx/nginx.conf). The Dockerfile installs the
`create-dev-cert.sh` script and either uses provided LE
certificates or generates a self-signed pair for dev.

## Routing rules (HTTPS server, port 8443)

| Location | Upstream | WebSocket? |
|----------|----------|------------|
| `/` | `frontend:3000` | yes (only `Upgrade: websocket`) |
| `/api/auth/` | `auth-service:8080` | no |
| `/api/` | `core-api:8080` | **no — explicitly stripped** (comment line 115) |
| `/chatHub` | `core-api:8080` | yes, `proxy_read_timeout 86400` |
| `/notificationHub` | `core-api:8080` | yes, `proxy_read_timeout 86400` |

The HTTP server on port 8080 redirects everything to HTTPS except
`/.well-known/acme-challenge/` (which serves the Certbot challenge
files from `/var/www/certbot`).

## Security posture

- **TLS** — `TLSv1.2` and `TLSv1.3` only; cipher list whitelisted
  to ECDHE-AES128/256-GCM, CHACHA20-POLY1305, and DHE-RSA fallbacks
  (line 50). Session cache 10m, session tickets disabled.
- **Headers added globally on HTTPS server**:
  `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`,
  `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`,
  `X-XSS-Protection: 1; mode=block`,
  `Referrer-Policy: strict-origin-when-cross-origin`. CSP is
  defined as a commented-out template (line 73) — not enforced.
- **`X-User-Id` is stripped at every location** (`proxy_set_header
  X-User-Id ""`) so a client cannot spoof user identity to
  upstream services.
- **H2C smuggling protection** — the `map $http_upgrade
  $connection_upgrade` and `map $http_upgrade
  $websocket_upgrade` blocks (lines 5–14) only emit `upgrade` /
  `websocket` literally when the client header equals
  `websocket`; everything else maps to empty string. Used at
  `/`, `/chatHub`, and `/notificationHub`. The `/api/` location
  intentionally does not forward `Upgrade` at all.

## Operational notes

- **DNS** — `resolver 127.0.0.11 valid=10s ipv6=off` (line 18)
  uses the embedded Docker resolver and re-resolves upstream
  hostnames every 10 seconds. This is what allows
  `frontend`/`auth-service`/`core-api` to be rebuilt and replaced
  without nginx returning 502 indefinitely.
- **OCSP stapling** is configured but commented out (lines 59–61);
  enabling it requires LE chain files mounted at
  `/etc/nginx/ssl/chain.pem`.
- The Dockerfile builds the cert at image-build time (per Step 0
  observations); the volume mount for LE certs in `compose.yml`
  is commented out (lines 412–417 of `docker-compose.yml`).

## What I should NOT assume

- **CSP is not applied.** The Content-Security-Policy line is
  commented out (line 73). Browsers receive no CSP header from
  this gateway. Don't claim "we enforce CSP" without checking
  whether the comment was uncommented or moved.
- **OCSP stapling is off** — TLS perf and privacy claims tied to
  it would be wrong today.
- **WebSocket upgrade is restricted to literal `"websocket"`.**
  Other upgrade tokens (e.g., `h2c`, `webtransport`) will be
  silently downgraded; if a future feature legitimately needs a
  different upgrade, this gateway will block it without an
  obvious error.
- **The `/api/` location does not forward `Upgrade`.** Any
  attempt to add a WebSocket route under `/api/...` instead of
  `/chatHub` / `/notificationHub` will fail through this gateway.
- **No rate limiting at this layer.** Nginx config has no
  `limit_req` zones — rate limiting is enforced inside Auth
  Service and Core API, not here. Don't recommend
  edge-rate-limiting fixes unless you actually edit this file.
