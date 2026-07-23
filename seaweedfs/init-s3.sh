#!/bin/sh
# Upload S3 IAM config to filer (uses same env vars as docker-compose object-storage).

set -eu

ACCESS_KEY="${OBJECT_STORAGE_ACCESS_KEY:-devhunt_access_key_dev_123456789012}"
SECRET_KEY="${OBJECT_STORAGE_SECRET_KEY:-devhunt_secret_key_dev_1234567890123456}"
FILER_URL="${SEAWEEDFS_FILER_URL:-http://localhost:8888}"

sleep 5

curl -fsS -X POST "${FILER_URL}/etc/seaweedfs/s3.json" \
  -H "Content-Type: application/json" \
  -d "{
  \"identities\": [
    {
      \"name\": \"devhunt\",
      \"credentials\": [
        {
          \"accessKey\": \"${ACCESS_KEY}\",
          \"secretKey\": \"${SECRET_KEY}\"
        }
      ],
      \"actions\": [
        \"Admin\",
        \"Read\",
        \"List\",
        \"Tagging\",
        \"Write\"
      ]
    }
  ]
}"

echo "S3 IAM configuration uploaded to SeaweedFS filer"
