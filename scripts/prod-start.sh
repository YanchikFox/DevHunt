#!/bin/bash
# Start in Production mode

set -e

echo "🚀 Starting DevHunt in PRODUCTION mode..."
echo "⚠️  All internal ports are closed (security)"
echo ""

if [ "$1" = "--build" ]; then
    echo "Building images..."
    docker-compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env up -d --build
else
    docker-compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env up -d
fi

echo ""
echo "✅ Production environment started!"
echo ""
echo "📋 Access points:"
echo "  API Gateway:  http://localhost (port 80)"
echo "  API Gateway:  https://localhost (port 443)"
echo ""
echo "🔒 Security features:"
echo "  ✓ Internal ports closed (DB, Redis, RabbitMQ)"
echo "  ✓ Swagger disabled"
echo "  ✓ Strict CORS policy"
echo "  ✓ Production optimizations enabled"

