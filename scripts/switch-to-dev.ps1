# Switch from Production to Development mode
# Stops production and starts development

Write-Host "🔄 Switching from PRODUCTION to DEVELOPMENT mode..." -ForegroundColor Cyan
Write-Host ""

# Stop production
Write-Host "Stopping production environment..." -ForegroundColor Yellow
docker-compose -f docker-compose.yml -f docker-compose.prod.yml down

# Start development
Write-Host "Starting development environment..." -ForegroundColor Yellow
.\scripts\dev-start.ps1

Write-Host ""
Write-Host "✅ Switched to DEVELOPMENT mode!" -ForegroundColor Green

