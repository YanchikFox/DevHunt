# PowerShell script to reset database and seed with data
# This script:
# 1. Resets the database (drops and recreates)
# 2. Applies migrations
# 3. Seeds the database with realistic data

param(
    [switch]$Force,
    [switch]$SkipReset
)

$ErrorActionPreference = "Stop"

Write-Host "=== DevHunt Database Reset and Seed ===" -ForegroundColor Cyan

# Step 1: Reset database
if (-not $SkipReset) {
    Write-Host "`nStep 1: Resetting database..." -ForegroundColor Yellow
    $resetParams = @{}
    if ($Force) { $resetParams.Force = $true }
    & "$PSScriptRoot\reset-db.ps1" @resetParams
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Database reset failed!" -ForegroundColor Red
        exit 1
    }
}

# Step 2: Apply migrations
Write-Host "`nStep 2: Applying migrations..." -ForegroundColor Yellow
try {
    Push-Location "$PSScriptRoot\..\.."
    dotnet ef database update --project DevHunt.Infrastructure --startup-project DevHunt.CoreApi
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Migrations failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Migrations applied successfully!" -ForegroundColor Green
} finally {
    Pop-Location
}

# Step 3: Seed database
Write-Host "`nStep 3: Seeding database..." -ForegroundColor Yellow
try {
    Push-Location "$PSScriptRoot\..\.."
    dotnet run --project DevHunt.DatabaseSeeder
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Seeding failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Database seeded successfully!" -ForegroundColor Green
} finally {
    Pop-Location
}

Write-Host "`n=== Database reset and seed completed! ===" -ForegroundColor Green
Write-Host "You can now start the application and test with seed data." -ForegroundColor Cyan

