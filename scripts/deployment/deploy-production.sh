#!/bin/bash
# DevHunt Production Deployment Script
# This script deploys DevHunt to a production server

set -e  # Exit on any error

echo "🚀 DevHunt Production Deployment"
echo "================================"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if running as root
if [ "$EUID" -eq 0 ]; then 
    echo -e "${RED}Error: Do not run as root. Use a user with docker permissions.${NC}"
    exit 1
fi

# Check Docker
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: Docker is not installed${NC}"
    exit 1
fi

if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo -e "${RED}Error: Docker Compose is not installed${NC}"
    exit 1
fi

# Check if .env file exists
if [ ! -f .env ]; then
    echo -e "${YELLOW}Warning: .env file not found${NC}"
    echo "Creating .env from env.example..."
    if [ -f env.example ]; then
        cp env.example .env
        echo -e "${YELLOW}Please edit .env file and set all required secrets before continuing!${NC}"
        echo "Press Enter to continue after editing .env..."
        read
    else
        echo -e "${RED}Error: env.example not found${NC}"
        exit 1
    fi
fi

# Validate required environment variables
echo "🔍 Validating environment variables..."
source .env

REQUIRED_VARS=(
    "POSTGRES_PASSWORD"
    "RABBITMQ_DEFAULT_PASS"
    "JWT_KEY"
    "ENCRYPTION_KEY"
    "ENCRYPTION_IV"
    "OBJECT_STORAGE_ACCESS_KEY"
    "OBJECT_STORAGE_SECRET_KEY"
    "OPENSEARCH_INITIAL_ADMIN_PASSWORD"
)

MISSING_VARS=()
for var in "${REQUIRED_VARS[@]}"; do
    if [ -z "${!var}" ]; then
        MISSING_VARS+=("$var")
    fi
done

if [ ${#MISSING_VARS[@]} -ne 0 ]; then
    echo -e "${RED}Error: Missing required environment variables:${NC}"
    printf '%s\n' "${MISSING_VARS[@]}"
    echo "Please set them in .env file"
    exit 1
fi

echo -e "${GREEN}✓ All required environment variables are set${NC}"

# Check TLS certificates
echo "🔒 Checking TLS certificates..."
if [ ! -f nginx/ssl/cert.pem ] || [ ! -f nginx/ssl/key.pem ]; then
    echo -e "${YELLOW}Warning: TLS certificates not found${NC}"
    echo "You need to set up TLS certificates. See nginx/PRODUCTION_TLS_SETUP.md"
    echo "For quick testing, you can generate self-signed certificates:"
    echo "  mkdir -p nginx/ssl"
    echo "  openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout nginx/ssl/key.pem -out nginx/ssl/cert.pem"
    echo ""
    read -p "Do you want to generate self-signed certificates now? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        mkdir -p nginx/ssl
        openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
            -keyout nginx/ssl/key.pem \
            -out nginx/ssl/cert.pem \
            -subj "/C=US/ST=State/L=City/O=DevHunt/CN=localhost"
        echo -e "${GREEN}✓ Self-signed certificates generated${NC}"
    else
        echo -e "${YELLOW}Continuing without TLS certificates (will fail if HTTPS is required)${NC}"
    fi
else
    echo -e "${GREEN}✓ TLS certificates found${NC}"
fi

# Pull latest images
echo "📥 Pulling latest Docker images..."
docker-compose -f docker-compose.yml -f docker-compose.prod.yml pull

# Build images (if needed)
echo "🔨 Building Docker images..."
docker-compose -f docker-compose.yml -f docker-compose.prod.yml build

# Stop existing containers
echo "🛑 Stopping existing containers..."
docker-compose -f docker-compose.yml -f docker-compose.prod.yml down

# Start services
echo "🚀 Starting services..."
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Wait for services to be ready
echo "⏳ Waiting for services to start..."
sleep 10

# Check service health
echo "🏥 Checking service health..."
services=("db" "cache-service" "message-broker" "core-api" "auth-service" "api-gateway")
for service in "${services[@]}"; do
    if docker-compose -f docker-compose.yml -f docker-compose.prod.yml ps | grep -q "$service.*Up"; then
        echo -e "${GREEN}✓ $service is running${NC}"
    else
        echo -e "${RED}✗ $service is not running${NC}"
        echo "Check logs: docker-compose logs $service"
    fi
done

# Run database migrations
echo "🗄️  Running database migrations..."
docker-compose -f docker-compose.yml -f docker-compose.prod.yml exec -T core-api dotnet ef database update || echo -e "${YELLOW}Warning: Migration failed (may need manual intervention)${NC}"

# Show status
echo ""
echo "📊 Deployment Status:"
docker-compose -f docker-compose.yml -f docker-compose.prod.yml ps

echo ""
echo -e "${GREEN}✓ Deployment complete!${NC}"
echo ""
echo "🌐 Services should be accessible at:"
echo "  - API Gateway (HTTP):  http://$(hostname -I | awk '{print $1}')"
echo "  - API Gateway (HTTPS): https://$(hostname -I | awk '{print $1}')"
echo ""
echo "📝 Useful commands:"
echo "  View logs: docker-compose -f docker-compose.yml -f docker-compose.prod.yml logs -f [service]"
echo "  Stop all:  docker-compose -f docker-compose.yml -f docker-compose.prod.yml down"
echo "  Restart:   docker-compose -f docker-compose.yml -f docker-compose.prod.yml restart [service]"


