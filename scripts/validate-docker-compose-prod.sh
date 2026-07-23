#!/usr/bin/env sh
# Validates merged docker-compose.yml + docker-compose.prod.yml (DEV-19).
set -eu

ROOT="$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)"
ENV_FILE="${COMPOSE_ENV_FILE:-$ROOT/env/compose-ci.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

docker compose --env-file "$ENV_FILE" \
  -f "$ROOT/docker-compose.yml" \
  -f "$ROOT/docker-compose.prod.yml" \
  config --quiet

echo "docker compose prod config: OK"
