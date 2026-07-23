# PowerShell script to create migration for RefreshToken table
# Usage: .\scripts\create-migration-refresh-tokens.ps1

Write-Host "Creating migration for RefreshToken table..." -ForegroundColor Cyan

# Ensure we're in the project root
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
Set-Location $projectRoot

# Create migration
dotnet ef migrations add AddRefreshTokens `
    --project DevHunt.Infrastructure/DevHunt.Infrastructure.csproj `
    --startup-project DevHunt.CoreApi/DevHunt.CoreApi.csproj `
    --context DevHuntDbContext `
    --output-dir Migrations

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Migration created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "To apply the migration, run:" -ForegroundColor Yellow
    Write-Host "  dotnet ef database update --project DevHunt.Infrastructure/DevHunt.Infrastructure.csproj --startup-project DevHunt.CoreApi/DevHunt.CoreApi.csproj" -ForegroundColor Yellow
} else {
    Write-Host "❌ Failed to create migration" -ForegroundColor Red
    exit 1
}

