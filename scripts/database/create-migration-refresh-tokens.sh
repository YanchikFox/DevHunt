#!/bin/bash
# Script to create migration for RefreshToken table
# Usage: ./scripts/create-migration-refresh-tokens.sh

set -e

echo "Creating migration for RefreshToken table..."

# Ensure we're in the project root
cd "$(dirname "$0")/.."

# Create migration
dotnet ef migrations add AddRefreshTokens \
    --project DevHunt.Infrastructure/DevHunt.Infrastructure.csproj \
    --startup-project DevHunt.CoreApi/DevHunt.CoreApi.csproj \
    --context DevHuntDbContext \
    --output-dir Migrations

echo "✅ Migration created successfully!"
echo ""
echo "To apply the migration, run:"
echo "  dotnet ef database update --project DevHunt.Infrastructure/DevHunt.Infrastructure.csproj --startup-project DevHunt.CoreApi/DevHunt.CoreApi.csproj"

