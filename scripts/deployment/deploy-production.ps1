# DevHunt Production Deployment Script (PowerShell)
# This script deploys DevHunt to a production server

$ErrorActionPreference = "Stop"

Write-Host "🚀 DevHunt Production Deployment" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check Docker
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Error: Docker is not installed" -ForegroundColor Red
    exit 1
}

if (-not (Get-Command docker-compose -ErrorAction SilentlyContinue) -and 
    -not (docker compose version 2>&1 | Out-Null)) {
    Write-Host "Error: Docker Compose is not installed" -ForegroundColor Red
    exit 1
}

# Check if .env file exists
if (-not (Test-Path .env)) {
    Write-Host "Warning: .env file not found" -ForegroundColor Yellow
    Write-Host "Creating .env from env.example..."
    if (Test-Path env.example) {
        Copy-Item env.example .env
        Write-Host "Please edit .env file and set all required secrets before continuing!" -ForegroundColor Yellow
        Read-Host "Press Enter to continue after editing .env"
    } else {
        Write-Host "Error: env.example not found" -ForegroundColor Red
        exit 1
    }
}

# Load environment variables
Get-Content .env | ForEach-Object {
    if ($_ -match '^\s*([^#][^=]*)\s*=\s*(.*)$') {
        $name = $matches[1].Trim()
        $value = $matches[2].Trim()
        if ($value -match '^\${.*}$') {
            # Skip variables with ${} substitutions for now
            return
        }
        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

# Validate required environment variables
Write-Host "🔍 Validating environment variables..." -ForegroundColor Cyan

$requiredVars = @(
    "POSTGRES_PASSWORD",
    "RABBITMQ_DEFAULT_PASS",
    "JWT_KEY",
    "ENCRYPTION_KEY",
    "ENCRYPTION_IV",
    "OBJECT_STORAGE_ACCESS_KEY",
    "OBJECT_STORAGE_SECRET_KEY",
    "OPENSEARCH_INITIAL_ADMIN_PASSWORD"
)

$missingVars = @()
foreach ($var in $requiredVars) {
    $value = [Environment]::GetEnvironmentVariable($var, "Process")
    if ([string]::IsNullOrEmpty($value)) {
        $missingVars += $var
    }
}

if ($missingVars.Count -gt 0) {
    Write-Host "Error: Missing required environment variables:" -ForegroundColor Red
    $missingVars | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "Please set them in .env file"
    exit 1
}

Write-Host "✓ All required environment variables are set" -ForegroundColor Green

# Check TLS certificates
Write-Host "🔒 Checking TLS certificates..." -ForegroundColor Cyan
if (-not (Test-Path "nginx/ssl/cert.pem") -or -not (Test-Path "nginx/ssl/key.pem")) {
    Write-Host "Warning: TLS certificates not found" -ForegroundColor Yellow
    Write-Host "You need to set up TLS certificates. See nginx/PRODUCTION_TLS_SETUP.md"
    Write-Host "For quick testing, you can generate self-signed certificates"
    Write-Host ""
    $response = Read-Host "Do you want to generate self-signed certificates now? (y/N)"
    if ($response -eq "y" -or $response -eq "Y") {
        New-Item -ItemType Directory -Force -Path "nginx/ssl" | Out-Null
        openssl req -x509 -nodes -days 365 -newkey rsa:2048 `
            -keyout nginx/ssl/key.pem `
            -out nginx/ssl/cert.pem `
            -subj "/C=US/ST=State/L=City/O=DevHunt/CN=localhost"
        Write-Host "✓ Self-signed certificates generated" -ForegroundColor Green
    } else {
        Write-Host "Continuing without TLS certificates (will fail if HTTPS is required)" -ForegroundColor Yellow
    }
} else {
    Write-Host "✓ TLS certificates found" -ForegroundColor Green
}

# Pull latest images
Write-Host "📥 Pulling latest Docker images..." -ForegroundColor Cyan
docker-compose -f docker-compose.yml -f docker-compose.prod.yml pull

# Build images (if needed)
Write-Host "🔨 Building Docker images..." -ForegroundColor Cyan
docker-compose -f docker-compose.yml -f docker-compose.prod.yml build

# Stop existing containers
Write-Host "🛑 Stopping existing containers..." -ForegroundColor Cyan
docker-compose -f docker-compose.yml -f docker-compose.prod.yml down

# Start services
Write-Host "🚀 Starting services..." -ForegroundColor Cyan
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Wait for services to be ready
Write-Host "⏳ Waiting for services to start..." -ForegroundColor Cyan
Start-Sleep -Seconds 10

# Check service health
Write-Host "🏥 Checking service health..." -ForegroundColor Cyan
$services = @("db", "cache-service", "message-broker", "core-api", "auth-service", "api-gateway")
foreach ($service in $services) {
    $status = docker-compose -f docker-compose.yml -f docker-compose.prod.yml ps | Select-String "$service.*Up"
    if ($status) {
        Write-Host "✓ $service is running" -ForegroundColor Green
    } else {
        Write-Host "✗ $service is not running" -ForegroundColor Red
        Write-Host "Check logs: docker-compose logs $service"
    }
}

# Run database migrations
Write-Host "🗄️  Running database migrations..." -ForegroundColor Cyan
try {
    docker-compose -f docker-compose.yml -f docker-compose.prod.yml exec -T core-api dotnet ef database update
} catch {
    Write-Host "Warning: Migration failed (may need manual intervention)" -ForegroundColor Yellow
}

# Show status
Write-Host ""
Write-Host "📊 Deployment Status:" -ForegroundColor Cyan
docker-compose -f docker-compose.yml -f docker-compose.prod.yml ps

Write-Host ""
Write-Host "✓ Deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 Services should be accessible at:"
$ipAddress = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.*" } | Select-Object -First 1).IPAddress
Write-Host "  - API Gateway (HTTP):  http://$ipAddress"
Write-Host "  - API Gateway (HTTPS): https://$ipAddress"
Write-Host ""
Write-Host "📝 Useful commands:"
Write-Host "  View logs: docker-compose -f docker-compose.yml -f docker-compose.prod.yml logs -f [service]"
Write-Host "  Stop all:  docker-compose -f docker-compose.yml -f docker-compose.prod.yml down"
Write-Host "  Restart:   docker-compose -f docker-compose.yml -f docker-compose.prod.yml restart [service]"


