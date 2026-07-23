#!/bin/bash
# Generates seaweedfs/s3.json from env (never commit the output file).

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUTPUT="${ROOT}/seaweedfs/s3.json"

ACCESS_KEY="${OBJECT_STORAGE_ACCESS_KEY:-devhunt_access_key_dev_123456789012}"
SECRET_KEY="${OBJECT_STORAGE_SECRET_KEY:-devhunt_secret_key_dev_1234567890123456}"

mkdir -p "$(dirname "$OUTPUT")"

cat > "$OUTPUT" <<EOF
{
  "identities": [
    {
      "name": "devhunt",
      "credentials": [
        {
          "accessKey": "${ACCESS_KEY}",
          "secretKey": "${SECRET_KEY}"
        }
      ],
      "actions": [
        "Admin",
        "Read",
        "List",
        "Tagging",
        "Write"
      ]
    }
  ]
}
EOF

echo "Wrote ${OUTPUT} (gitignored — do not commit)"
