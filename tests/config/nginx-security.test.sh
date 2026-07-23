#!/bin/bash
# Static checks for nginx edge security (H2C protection, CSP, rate limits, prod image).

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

NGINX_TLS_CONF="nginx/nginx.conf"
NGINX_HTTP_CONF="nginx/nginx-http-only.conf"
NGINX_EDGE_INC="nginx/edge-security.inc"
NGINX_RATE_ZONES="nginx/rate-limit-zones.conf"
NGINX_PROD_DOCKERFILE="nginx/Dockerfile.prod"

fail() {
  echo "❌ $1"
  exit 1
}

echo "Testing nginx edge security configurations..."

for file in "$NGINX_TLS_CONF" "$NGINX_HTTP_CONF" "$NGINX_EDGE_INC" "$NGINX_RATE_ZONES" "$NGINX_PROD_DOCKERFILE"; do
  [ -f "$file" ] || fail "Missing required file: $file"
done

# H2C: Upgrade must not pass through raw $http_upgrade
if grep -E '^\s*proxy_set_header\s+Upgrade\s+.*\$http_upgrade' "$NGINX_TLS_CONF"; then
  fail "nginx.conf must not set Upgrade to \$http_upgrade (H2C smuggling)"
fi

if ! grep -qE 'map\s+\$http_upgrade\s+.*\$websocket_upgrade' "$NGINX_TLS_CONF"; then
  fail "nginx.conf must map websocket upgrades via \$websocket_upgrade"
fi

# CSP enabled at edge (not commented out)
if ! grep -q 'Content-Security-Policy' "$NGINX_EDGE_INC"; then
  fail "edge-security.inc must define Content-Security-Policy"
fi

if grep -E '^\s*#\s*add_header\s+Content-Security-Policy' "$NGINX_TLS_CONF"; then
  fail "CSP must not remain commented in nginx.conf"
fi

for conf in "$NGINX_TLS_CONF" "$NGINX_HTTP_CONF"; do
  if ! grep -q 'include /etc/nginx/conf.d/edge-security.inc' "$conf"; then
    fail "$conf must include edge-security.inc"
  fi
done

# Rate limit zones and usage
for zone in auth_api_limit public_api_limit; do
  grep -q "zone=${zone}" "$NGINX_RATE_ZONES" || fail "rate-limit-zones.conf missing ${zone}"
done

grep -q 'limit_req zone=auth_api_limit' "$NGINX_TLS_CONF" || fail "nginx.conf must rate-limit /api/auth/"
grep -q 'limit_req zone=auth_api_limit' "$NGINX_HTTP_CONF" || fail "nginx-http-only.conf must rate-limit /api/auth/"
grep -q 'limit_req zone=public_api_limit' "$NGINX_TLS_CONF" || fail "nginx.conf must rate-limit /api/"
grep -q 'limit_req zone=public_api_limit' "$NGINX_HTTP_CONF" || fail "nginx-http-only.conf must rate-limit /api/"

# Request hardening
grep -q 'client_max_body_size' "$NGINX_EDGE_INC" || fail "edge-security.inc must set client_max_body_size"
grep -q 'proxy_connect_timeout' "$NGINX_EDGE_INC" || fail "edge-security.inc must set proxy timeouts"

# TLS prod image uses hardened config
grep -q 'nginx.conf' "$NGINX_PROD_DOCKERFILE" || fail "Dockerfile.prod must copy nginx.conf"
grep -q 'nginx-http-only.conf' "$NGINX_PROD_DOCKERFILE" && fail "Dockerfile.prod must not use nginx-http-only.conf"

grep -q 'dockerfile: Dockerfile.prod' docker-compose.prod.yml || fail "docker-compose.prod.yml must build api-gateway from Dockerfile.prod"

# Security headers — checked across nginx.conf and the shared edge-security
# include (both are already required above to include edge-security.inc, so
# a header defined in either file is present in the effective config).
for header in Strict-Transport-Security X-Frame-Options X-Content-Type-Options; do
  grep -q "$header" "$NGINX_TLS_CONF" "$NGINX_EDGE_INC" || fail "nginx.conf (or edge-security.inc) missing $header"
done

echo "✅ Nginx security checks passed"
