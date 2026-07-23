#!/bin/bash
# Fail CI if PostgreSQL dumps or other PII-bearing SQL files are tracked in git.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

FORBIDDEN_PATTERNS=(
  'backup\.sql$'
  'backup_clean\.sql$'
  'backup.*\.sql$'
)

fail=0
while IFS= read -r path; do
  for pattern in "${FORBIDDEN_PATTERNS[@]}"; do
    if [[ "$path" =~ $pattern ]]; then
      echo "::error::Forbidden database dump tracked in git: $path"
      fail=1
      break
    fi
  done
done < <(git ls-files '*.sql' 2>/dev/null || true)

if [[ "$fail" -ne 0 ]]; then
  echo "Remove dumps from the index (git rm --cached) and add patterns to .gitignore."
  exit 1
fi

if git ls-files --error-unmatch seaweedfs/s3.json >/dev/null 2>&1; then
  echo "::error::seaweedfs/s3.json must not be committed — use s3.json.example and scripts/seaweedfs/generate-s3-config.sh"
  exit 1
fi

echo "✅ No forbidden database dumps or committed seaweedfs/s3.json"
