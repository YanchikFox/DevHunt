#!/bin/bash
# Script to initialize SeaweedFS S3 credentials and buckets
# This script should be run after SeaweedFS is started

set -e

SEAWEEDFS_FILER_URL="${SEAWEEDFS_FILER_URL:-http://localhost:8888}"
SEAWEEDFS_S3_URL="${SEAWEEDFS_S3_URL:-http://localhost:8333}"
ACCESS_KEY="${OBJECT_STORAGE_ACCESS_KEY:-devhunt_access_key_dev_123456789012}"
SECRET_KEY="${OBJECT_STORAGE_SECRET_KEY:-devhunt_secret_key_dev_1234567890123456}"

echo "Initializing SeaweedFS S3 credentials and buckets..."
echo "Filer URL: $SEAWEEDFS_FILER_URL"
echo "S3 URL: $SEAWEEDFS_S3_URL"

# Wait for SeaweedFS to be ready
echo "Waiting for SeaweedFS to be ready..."
for i in {1..30}; do
    if curl -s -f "$SEAWEEDFS_FILER_URL" > /dev/null 2>&1; then
        echo "SeaweedFS is ready!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "Error: SeaweedFS is not ready after 30 attempts"
        exit 1
    fi
    echo "Attempt $i/30: Waiting for SeaweedFS..."
    sleep 2
done

# Configure S3 IAM credentials via Filer API
echo "Configuring S3 IAM credentials..."
curl -X POST "$SEAWEEDFS_FILER_URL/admin/iam/set" \
    -H "Content-Type: application/json" \
    -d "{
        \"identities\": [
            {
                \"name\": \"$ACCESS_KEY\",
                \"credentials\": [
                    {
                        \"accessKey\": \"$ACCESS_KEY\",
                        \"secretKey\": \"$SECRET_KEY\"
                    }
                ],
                \"actions\": [\"Admin\", \"Read\", \"Write\", \"List\", \"Tagging\"]
            }
        ]
    }" || echo "Warning: Failed to configure IAM credentials (may already be configured)"

# Create buckets using AWS CLI or curl
echo "Creating buckets..."

BUCKETS=("avatars" "project-files" "uploads" "backups")

for bucket in "${BUCKETS[@]}"; do
    echo "Creating bucket: $bucket"
    # Try to create bucket using S3 API
    curl -X PUT "$SEAWEEDFS_S3_URL/$bucket" \
        -H "Authorization: AWS $ACCESS_KEY:$(echo -n "PUT\n\n\n$(date -u +%Y%m%dT%H%M%SZ)\n/$bucket" | openssl dgst -sha1 -hmac "$SECRET_KEY" -binary | base64)" \
        || echo "Warning: Failed to create bucket $bucket (may already exist)"
done

echo "SeaweedFS initialization completed!"












