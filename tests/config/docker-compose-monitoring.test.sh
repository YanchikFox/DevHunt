#!/bin/bash
# Test for docker-compose.monitoring.yml security configurations

set -e

COMPOSE_FILE="docker-compose.monitoring.yml"

echo "Testing docker-compose.monitoring.yml security configurations..."

# Check if file exists
if [ ! -f "$COMPOSE_FILE" ]; then
  echo "❌ Error: $COMPOSE_FILE not found"
  exit 1
fi

# Check for read_only: true in all services
echo "Checking for read_only: true in services..."
if ! grep -q "read_only: true" "$COMPOSE_FILE"; then
  echo "❌ Error: read_only: true not found in $COMPOSE_FILE"
  exit 1
fi

# Check for tmpfs configuration
echo "Checking for tmpfs configuration..."
if ! grep -q "tmpfs:" "$COMPOSE_FILE"; then
  echo "❌ Error: tmpfs configuration not found"
  exit 1
fi

# Check for read-only volumes (:ro)
echo "Checking for read-only volume mounts..."
if ! grep -q ":ro" "$COMPOSE_FILE"; then
  echo "⚠️  Warning: No read-only volume mounts found (this might be OK if volumes need write access)"
fi

# Check for security_opt
echo "Checking for security_opt configuration..."
if ! grep -q "security_opt:" "$COMPOSE_FILE"; then
  echo "❌ Error: security_opt not found"
  exit 1
fi

# Verify specific services have read_only
SERVICES=("prometheus" "grafana" "alertmanager")
for service in "${SERVICES[@]}"; do
  if ! grep -A 10 "^  $service:" "$COMPOSE_FILE" | grep -q "read_only: true"; then
    echo "❌ Error: Service $service does not have read_only: true"
    exit 1
  fi
done

echo "✅ All security checks passed for docker-compose.monitoring.yml"

