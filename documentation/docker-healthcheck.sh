#!/bin/sh
set -eu

if [ -n "${DOCS_BASIC_AUTH_USERNAME:-}" ] && [ -n "${DOCS_BASIC_AUTH_PASSWORD:-}" ]; then
  curl -fsS -u "${DOCS_BASIC_AUTH_USERNAME}:${DOCS_BASIC_AUTH_PASSWORD}" http://localhost/portal/
else
  curl -fsS http://localhost/portal/
fi
