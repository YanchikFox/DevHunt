# Switch from Development to Production mode
# Stops development and starts production

Write-Host "🔄 Switching from DEVELOPMENT to PRODUCTION mode..." -ForegroundColor Cyan
Write-Host ""

# Stop development
Write-Host "Stopping development environment..." -ForegroundColor Yellow
docker-compose down

# Start production
Write-Host "Starting production environment..." -ForegroundColor Yellow
.\scripts\prod-start.ps1

Write-Host ""
Write-Host "✅ Switched to PRODUCTION mode!" -ForegroundColor Green

