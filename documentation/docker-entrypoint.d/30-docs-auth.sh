#!/bin/sh
set -eu

AUTH_CONF="/etc/nginx/conf.d/basic-auth.inc"
HTPASSWD_FILE="/etc/nginx/.htpasswd"

: > "$AUTH_CONF"

if [ -n "${DOCS_BASIC_AUTH_USERNAME:-}" ] && [ -n "${DOCS_BASIC_AUTH_PASSWORD:-}" ]; then
  htpasswd -bc "$HTPASSWD_FILE" "$DOCS_BASIC_AUTH_USERNAME" "$DOCS_BASIC_AUTH_PASSWORD" >/dev/null
  cat > "$AUTH_CONF" <<EOF
auth_basic "DevHunt Documentation";
auth_basic_user_file $HTPASSWD_FILE;
EOF
  echo "[docs-auth] Basic Auth enabled for documentation."
else
  echo "[docs-auth] Basic Auth disabled for documentation."
fi
