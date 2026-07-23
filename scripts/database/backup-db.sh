#!/bin/bash
# PostgreSQL Backup Script for DevHunt
# Creates daily backups with 30-day retention

set -e

# Configuration
BACKUP_DIR="${BACKUP_DIR:-/backups}"
RETENTION_DAYS=30
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/devhunt_backup_${TIMESTAMP}.sql.gz"

# Database connection from environment
DB_HOST="${POSTGRES_HOST:-db}"
DB_PORT="${POSTGRES_PORT:-5432}"
DB_NAME="${POSTGRES_DB:-devhunt_db}"
DB_USER="${POSTGRES_USER:-postgres}"
DB_PASSWORD="${POSTGRES_PASSWORD}"

# S3 Configuration (optional)
S3_BUCKET="${S3_BACKUP_BUCKET:-}"
S3_ENDPOINT="${S3_ENDPOINT:-}"
S3_ACCESS_KEY="${S3_ACCESS_KEY:-}"
S3_SECRET_KEY="${S3_SECRET_KEY:-}"

echo "Starting backup at $(date)"

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Create backup using pg_dump
export PGPASSWORD="$DB_PASSWORD"
pg_dump -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" \
    --no-owner --no-acl \
    | gzip > "$BACKUP_FILE"

echo "Backup created: $BACKUP_FILE"
echo "Backup size: $(du -h "$BACKUP_FILE" | cut -f1)"

# Upload to S3 if configured
if [ -n "$S3_BUCKET" ] && [ -n "$S3_ACCESS_KEY" ]; then
    echo "Uploading backup to S3..."
    
    if command -v aws &> /dev/null; then
        aws s3 cp "$BACKUP_FILE" "s3://${S3_BUCKET}/devhunt_backup_${TIMESTAMP}.sql.gz" \
            ${S3_ENDPOINT:+--endpoint-url "$S3_ENDPOINT"}
        echo "Backup uploaded to S3"
    elif command -v mc &> /dev/null && [ -n "$S3_ENDPOINT" ]; then
        # MinIO client
        mc alias set myminio "$S3_ENDPOINT" "$S3_ACCESS_KEY" "$S3_SECRET_KEY" || true
        mc cp "$BACKUP_FILE" "myminio/${S3_BUCKET}/"
        echo "Backup uploaded to MinIO"
    else
        echo "Warning: S3 configured but no client available (aws-cli or mc)"
    fi
fi

# Cleanup old backups (retention policy)
echo "Cleaning up backups older than $RETENTION_DAYS days..."
find "$BACKUP_DIR" -name "devhunt_backup_*.sql.gz" -type f -mtime +$RETENTION_DAYS -delete
echo "Cleanup complete"

# List remaining backups
BACKUP_COUNT=$(find "$BACKUP_DIR" -name "devhunt_backup_*.sql.gz" -type f | wc -l)
echo "Total backups retained: $BACKUP_COUNT"

echo "Backup completed at $(date)"

