---
sidebar_position: 3
title: Local Development Workflow
description: Complete guide to setting up and working with DevHunt locally
draft: true
sidebar_label: Local Development Workflow
---

# Local Development Workflow

This guide covers the complete local development workflow for DevHunt, from initial setup to debugging and testing. Whether you're a new contributor or returning developer, this document will help you get productive quickly.

## 🚀 3-Click Setup (New Developer)

### Prerequisites Check

```bash
# Required tools
which docker     # Docker Desktop 4.30+
which dotnet     # .NET 9 SDK
which node       # Node.js 18+
which git        # Git client

# Optional but recommended
which rider      # JetBrains Rider (or VS Code)
which npm        # npm 9+
```

### Step 1: Clone and Setup

```bash
# Clone repository
git clone https://github.com/YanchikFox/DevHunt.git
cd DevHunt

# Copy environment template
cp env.example .env

# Fill in required secrets (see .env file comments)
# POSTGRES_PASSWORD, JWT_KEY, OBJECT_STORAGE_ACCESS_KEY, etc.
```

### Step 2: Start Full Stack

```bash
# Start all services (PostgreSQL, Redis, RabbitMQ, etc.)
docker compose up -d

# Wait for services to be healthy (~2-3 minutes)
docker compose ps

# Verify core services
curl http://localhost:7001/health  # Auth Service
curl http://localhost:7002/health  # Core API
curl http://localhost:3000         # Frontend (may take longer)
```

### Step 3: Verify and Test

```bash
# Run smoke tests
./scripts/testing/test-api.sh

# Access the application
open http://localhost:3000
```

## 🎯 Service-Specific Development

### Backend Only (Core API + Auth Service)

```bash
# Start infrastructure only
docker compose up -d db cache-service message-broker object-storage

# Run Core API locally
cd DevHunt.CoreApi
dotnet restore
dotnet run

# In another terminal, run Auth Service
cd ../DevHunt.AuthService
dotnet restore
dotnet run

# Test API endpoints
curl http://localhost:7002/swagger
curl http://localhost:7001/swagger
```

### Frontend Only (Against Running Backend)

```bash
# Ensure backend is running
curl http://localhost:7002/health

# Start frontend development server
cd frontend
npm install
npm run dev

# Access development UI
open http://localhost:3000
```

### Single Service Development

```bash
# Example: ML Service development
docker compose up -d db message-broker

# Run ML service locally
cd ml-service
python -m venv .venv
source .venv/bin/activate  # Windows: .venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --reload --host 0.0.0.0 --port 8000

# Test ML endpoints
curl http://localhost:8000/health
```

## 🧪 Testing Workflow

### Backend Tests (.NET)

```bash
# All backend tests
dotnet test

# Specific test projects
dotnet test DevHunt.CoreApi.Tests
dotnet test DevHunt.AuthService.Tests

# With coverage (if configured)
dotnet test --collect:"XPlat Code Coverage"

# Integration tests only
dotnet test --filter Category=Integration

# Debug specific test
dotnet test --filter "TestName" --logger "console;verbosity=detailed"
```

### Frontend Tests

```bash
cd frontend

# Unit tests
npm run test

# E2E tests (requires services running)
npm run test:e2e

# Component tests with Storybook
npm run storybook
npm run test:storybook

# Visual regression tests
npm run test:visual
```

### Infrastructure Tests

```bash
# API smoke tests
./scripts/testing/test-api.sh

# Integration tests
./scripts/testing/test-integration-gateway.sh

# Load testing (if configured)
./scripts/testing/test-load.sh
```

## 🔍 Debugging Guide

### Backend Debugging (.NET)

```bash
# Attach debugger in IDE
# Rider: Run → Attach to Process → dotnet
# VS Code: F5 with launch.json configured

# Debug specific service
cd DevHunt.CoreApi
dotnet build
dotnet run --no-launch-profile  # For debugger attachment

# Check logs
docker compose logs -f core-api

# Debug database queries (add to appsettings.Development.json)
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Information"
  }
}
```

### Frontend Debugging (Next.js)

```bash
cd frontend

# Development mode with source maps
npm run dev

# Debug in browser
# Chrome DevTools → Sources → webpack://
# Add breakpoints in .tsx files

# Check React DevTools
# Install browser extension for component inspection

# Debug API calls
# Browser Network tab → Filter by /api/
```

### Node.js Services Debugging

```bash
# Debug integration-gateway
cd integration-gateway
npm run dev:debug  # If configured with --inspect

# Debug notification-service
cd notification-service
npm run debug

# Check service logs
docker compose logs -f integration-gateway
```

### Database Debugging

```bash
# Connect to PostgreSQL
docker compose exec db psql -U postgres -d devhunt_db

# Check active connections
SELECT * FROM pg_stat_activity;

# Monitor slow queries (enable in postgresql.conf)
SELECT * FROM pg_stat_statements ORDER BY total_time DESC;

# View table sizes
SELECT schemaname, tablename,
       pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

## 🛠️ Local Infrastructure

### Service URLs and Ports

| Service          | URL                    | Purpose        | Logs                                    |
| ---------------- | ---------------------- | -------------- | --------------------------------------- |
| **Frontend**     | http://localhost:3000  | React UI       | `docker compose logs -f frontend`       |
| **Core API**     | http://localhost:7002  | REST API       | `docker compose logs -f core-api`       |
| **Auth Service** | http://localhost:7001  | Authentication | `docker compose logs -f auth-service`   |
| **ML Service**   | http://localhost:8000  | AI/ML          | `docker compose logs -f ml-service`     |
| **PostgreSQL**   | localhost:5432         | Database       | `docker compose logs -f db`             |
| **Redis**        | localhost:6379         | Cache          | `docker compose logs -f cache-service`  |
| **RabbitMQ**     | http://localhost:15672 | Message Queue  | `docker compose logs -f message-broker` |
| **SeaweedFS**    | http://localhost:8333  | File Storage   | `docker compose logs -f object-storage` |

### Database Management

```bash
# Reset database and seed
./scripts/database/reset-and-seed.ps1

# Apply migrations only
./scripts/database/apply-migrations.ps1

# Connect to database
docker compose exec db psql -U postgres -d devhunt_db

# Backup database
./scripts/database/backup-db.sh

# View migration history
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

### Message Queue Monitoring

```bash
# RabbitMQ Management UI
open http://localhost:15672
# Username: devhunt (from .env RABBITMQ_DEFAULT_USER)
# Password: from .env RABBITMQ_DEFAULT_PASS

# Check queue status
docker compose exec message-broker rabbitmqctl list_queues name messages_ready messages_unacknowledged

# View exchange bindings
docker compose exec message-broker rabbitmqctl list_bindings
```

## 🚨 Common Pitfalls & Solutions

### Port Conflicts

```bash
# Check what's using ports
netstat -an | grep :3000  # Linux/macOS
netstat -ano | findstr :3000  # Windows

# Change ports in docker-compose.override.yml
services:
  frontend:
    ports:
      - "3001:3000"  # Use 3001 instead of 3000
```

### Database Migration Issues

```bash
# Force reset database
docker compose down -v
docker compose up -d db
./scripts/database/reset-and-seed.ps1

# Manual migration rollback
cd DevHunt.CoreApi
dotnet ef database update PreviousMigrationName
```

### Environment Variables Missing

```bash
# Check required variables
cat .env | grep -v '^#' | grep -v '^$'

# Validate JWT key length (minimum 32 chars)
echo $JWT_KEY | wc -c

# Check database connectivity
docker compose exec db pg_isready -U postgres
```

### CORS Issues (Frontend Development)

```bash
# Check CORS headers in browser Network tab
# Verify frontend NEXT_PUBLIC_API_URL matches backend port

# For local frontend development against remote backend
cd frontend
NEXT_PUBLIC_API_URL=http://localhost:7002/api npm run dev
```

### JWT Token Issues

```bash
# Check token format
curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:7002/api/projects

# Decode token payload (for debugging)
echo "YOUR_TOKEN" | cut -d'.' -f2 | base64 -d

# Verify token expiration
# Check JWT_KEY consistency between Auth and Core API services
```

### Volume Conflicts (Docker)

```bash
# Clear all volumes
docker compose down -v

# Clear specific volume
docker volume rm devhunt_postgres_data

# Rebuild without cache
docker compose build --no-cache
```

### RabbitMQ Connection Issues

```bash
# Check RabbitMQ connectivity
docker compose exec message-broker rabbitmq-diagnostics ping

# Reset message broker
docker compose down message-broker
docker volume rm devhunt_message_broker_data
docker compose up -d message-broker
```

## 🛠️ Useful Commands Reference

### Docker Compose

```bash
# Full stack management
docker compose up -d                    # Start all services
docker compose down                     # Stop all services
docker compose logs -f core-api         # Follow service logs
docker compose ps                       # Show service status
docker compose exec core-api sh         # Shell into container
docker compose restart frontend         # Restart specific service

# Development helpers
docker compose up -d --build frontend   # Rebuild and start frontend
docker compose down -v                  # Stop and remove volumes
docker system prune -f                  # Clean unused resources
```

### .NET Development

```bash
# Project management
dotnet restore                          # Restore packages
dotnet build                            # Build project
dotnet run                              # Run project
dotnet watch run                        # Run with file watching

# Testing
dotnet test                             # Run all tests
dotnet test --filter Category=Unit      # Run unit tests only
dotnet test --logger "trx;logfilename=testresults.trx"

# Database operations
dotnet ef migrations list                # List migrations
dotnet ef database update               # Apply migrations
dotnet ef migrations add NewMigration   # Create migration
```

### Node.js Development

```bash
# Package management
npm install                             # Install dependencies
npm update                              # Update packages
npm audit fix                          # Fix security issues

# Development
npm run dev                             # Start development server
npm run build                           # Build for production
npm run lint                            # Run linter
npm run test                            # Run tests

# Service-specific
npm run debug                          # Debug mode (if configured)
npm run test:watch                     # Watch mode tests
```

### Database Operations

```bash
# PostgreSQL client
psql -h localhost -U postgres -d devhunt_db

# Common queries
SELECT * FROM "Users" LIMIT 5;
SELECT COUNT(*) FROM "Projects" WHERE status = 'active';
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

# Redis CLI
docker compose exec cache-service redis-cli

# Redis commands
KEYS *                                  # List all keys
TTL your:key                           # Check key expiration
GET your:key                           # Get key value
```

## 📚 Related Documentation

- **[Quickstart Guide](../quickstart.md)** - Initial setup overview
- **[Configuration Guide](../configuration.md)** - Environment variables
- **[Database Schema](../../backend/database/schema.md)** - Database structure
- **[API Reference](../../api/intro.md)** - Service endpoints
- **[Operations Guide](../operations.md)** - Production operations

## 🎯 Getting Help

### Development Issues

1. Check service logs: `docker compose logs -f <service-name>`
2. Verify environment variables in `.env`
3. Test individual services with health checks
4. Check database connectivity and migrations

### Code Issues

1. Run tests to identify problems: `dotnet test`
2. Check API documentation for correct endpoints
3. Verify database schema matches your queries
4. Review recent commits for breaking changes

### Performance Issues

1. Monitor database query performance
2. Check Redis cache hit rates
3. Monitor RabbitMQ queue depths
4. Review application logs for bottlenecks

This workflow should cover 90% of your local development needs. For complex issues, check the troubleshooting section above or ask in the development team chat.
