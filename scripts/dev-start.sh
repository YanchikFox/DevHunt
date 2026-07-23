#!/bin/bash
# Start in Development mode

set -e

echo "🚀 Starting DevHunt in DEVELOPMENT mode..."
echo ""

export COMPOSE_PROFILES=dev

if [ "$1" = "--build" ]; then
    echo "Building images..."
    docker-compose --env-file .env up -d --build
else
    docker-compose --env-file .env up -d
fi

echo ""
echo "✅ Development environment started!"
echo ""
echo "📋 Access points:"
echo "  Frontend:     http://localhost:3000"
echo "  Auth API:     http://localhost:7001"
echo "  Core API:     http://localhost:7002"
echo "  API Gateway:  http://localhost"
echo ""
echo "📊 Monitoring:"
echo "  Swagger UI:   http://localhost:7001/swagger (Auth)"
echo "  Swagger UI:   http://localhost:7002/swagger (Core API)"
echo "  Prometheus:   http://localhost:9090"
echo "  Jaeger:       http://localhost:16686"
echo ""
echo "💡 All ports are exposed for easy debugging"

