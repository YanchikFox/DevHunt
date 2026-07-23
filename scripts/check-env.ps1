# PowerShell script to check .env file for required variables
# Checks if all required environment variables are set and not using placeholders

param(
    [switch]$Fix,
    [string]$EnvFile = ".env"
)

$ErrorActionPreference = "Stop"

Write-Host "=== DevHunt Environment Variables Check ===" -ForegroundColor Cyan

if (-not (Test-Path $EnvFile)) {
    Write-Host "Error: File $EnvFile not found!" -ForegroundColor Red
    Write-Host "Create it by copying env.example: cp env.example .env" -ForegroundColor Yellow
    exit 1
}

# Load .env file
Write-Host "`nLoading $EnvFile..." -ForegroundColor Yellow
$envVars = @{}
Get-Content $EnvFile | ForEach-Object {
    if ($_ -match '^\s*([^#][^=]*)=(.*)$') {
        $name = $matches[1].Trim()
        $value = $matches[2].Trim()
        $envVars[$name] = $value
    }
}

# Required variables for basic functionality
$required = @{
    "POSTGRES_PASSWORD" = @{
        Description = "PostgreSQL database password"
        Critical = $true
        MinLength = 8
    }
    "RABBITMQ_DEFAULT_PASS" = @{
        Description = "RabbitMQ password"
        Critical = $true
        MinLength = 8
    }
    "JWT_KEY" = @{
        Description = "JWT signing key"
        Critical = $true
        MinLength = 32
    }
}

# Important variables (warnings)
$important = @{
    "OPENSEARCH_INITIAL_ADMIN_PASSWORD" = @{
        Description = "OpenSearch admin password"
        Critical = $false
        MinLength = 8
    }
    "OPENOBSERVE_ROOT_PASSWORD" = @{
        Description = "OpenObserve root password"
        Critical = $false
        MinLength = 8
    }
    "OBJECT_STORAGE_ACCESS_KEY" = @{
        Description = "Object Storage access key"
        Critical = $false
        MinLength = 16
    }
    "OBJECT_STORAGE_SECRET_KEY" = @{
        Description = "Object Storage secret key"
        Critical = $false
        MinLength = 16
    }
}

$errors = @()
$warnings = @()
$ok = @()

# Check required variables
Write-Host "`nChecking required variables..." -ForegroundColor Yellow
foreach ($varName in $required.Keys) {
    $config = $required[$varName]
    $value = $envVars[$varName]
    
    if ([string]::IsNullOrEmpty($value)) {
        $errors += "❌ $varName - NOT SET ($($config.Description))"
    }
    elseif ($value -like "*CHANGEME*" -or $value -like "*changeme*") {
        $errors += "❌ $varName - Using placeholder 'CHANGEME' ($($config.Description))"
    }
    elseif ($value.Length -lt $config.MinLength) {
        $errors += "❌ $varName - Too short (minimum $($config.MinLength) characters, got $($value.Length))"
    }
    else {
        $ok += "✅ $varName - OK"
    }
}

# Check important variables
Write-Host "`nChecking important variables..." -ForegroundColor Yellow
foreach ($varName in $important.Keys) {
    $config = $important[$varName]
    $value = $envVars[$varName]
    
    if ([string]::IsNullOrEmpty($value)) {
        $warnings += "⚠️  $varName - NOT SET ($($config.Description))"
    }
    elseif ($value -like "*CHANGEME*" -or $value -like "*changeme*") {
        $warnings += "⚠️  $varName - Using placeholder 'CHANGEME' ($($config.Description))"
    }
    elseif ($value.Length -lt $config.MinLength) {
        $warnings += "⚠️  $varName - Too short (minimum $($config.MinLength) characters, got $($value.Length))"
    }
    else {
        $ok += "✅ $varName - OK"
    }
}

# Display results
Write-Host "`n=== Results ===" -ForegroundColor Cyan

if ($ok.Count -gt 0) {
    Write-Host "`n✅ OK:" -ForegroundColor Green
    $ok | ForEach-Object { Write-Host $_ -ForegroundColor Green }
}

if ($warnings.Count -gt 0) {
    Write-Host "`n⚠️  Warnings:" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
}

if ($errors.Count -gt 0) {
    Write-Host "`n❌ Errors:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
}

# Summary
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "OK: $($ok.Count)" -ForegroundColor Green
Write-Host "Warnings: $($warnings.Count)" -ForegroundColor Yellow
Write-Host "Errors: $($errors.Count)" -ForegroundColor Red

if ($errors.Count -gt 0) {
    Write-Host "`n⚠️  System may not work correctly with missing required variables!" -ForegroundColor Red
    Write-Host "`nTo generate secure passwords:" -ForegroundColor Yellow
    Write-Host "  openssl rand -base64 32" -ForegroundColor White
    exit 1
}
elseif ($warnings.Count -gt 0) {
    Write-Host "`n⚠️  Some important variables are not set. System will work but some features may be unavailable." -ForegroundColor Yellow
    exit 0
}
else {
    Write-Host "`n✅ All variables are properly configured!" -ForegroundColor Green
    exit 0
}

