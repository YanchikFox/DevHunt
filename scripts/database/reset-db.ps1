# PowerShell script to reset DevHunt database
# This script drops and recreates the database, then applies migrations

param(
    [switch]$Force,
    [string]$DbHost = "localhost",
    [int]$DbPort = 5432,
    [string]$DbName = "devhunt_db",
    [string]$DbUser = "postgres"
)

$ErrorActionPreference = "Stop"

# Function to resolve variable references in .env file
function Resolve-EnvValue {
    param([string]$value, [hashtable]$envVars, [hashtable]$resolvedVars = @{})
    
    if ([string]::IsNullOrEmpty($value)) {
        return $value
    }
    
    # If already resolved, return cached value
    if ($resolvedVars.ContainsKey($value)) {
        return $resolvedVars[$value]
    }
    
    # Replace ${VAR_NAME} with actual value (with recursion protection)
    $resolved = $value
    $maxIterations = 10
    $iteration = 0
    
    while ($resolved -match '\$\{([^}]+)\}' -and $iteration -lt $maxIterations) {
        $iteration++
        $varName = $matches[1]
        
        if ($envVars.ContainsKey($varName)) {
            $varValue = $envVars[$varName]
            # Recursively resolve if the referenced variable also contains references
            if ($varValue -match '\$\{') {
                $varValue = Resolve-EnvValue -value $varValue -envVars $envVars -resolvedVars $resolvedVars
            }
            $resolved = $resolved -replace "\$\{$varName\}", $varValue
        } else {
            # Variable not found, keep original reference
            break
        }
    }
    
    # Cache resolved value
    $resolvedVars[$value] = $resolved
    
    return $resolved
}

# Load environment variables from .env file if it exists
$envVars = @{}
if (Test-Path ".env") {
    # First pass: load all variables
    Get-Content ".env" | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]*)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            $envVars[$name] = $value
        }
    }
    
    # Second pass: resolve variable references
    $resolvedVars = @{}
    foreach ($key in $envVars.Keys) {
        $resolvedVars[$key] = Resolve-EnvValue -value $envVars[$key] -envVars $envVars -resolvedVars $resolvedVars
        [Environment]::SetEnvironmentVariable($key, $resolvedVars[$key], "Process")
    }
}

# Get database password from environment
$DbPassword = $env:POSTGRES_PASSWORD
if (-not $DbPassword -or $DbPassword -like '*${*' -or $DbPassword -like '*CHANGEME*') {
    Write-Host "Error: POSTGRES_PASSWORD environment variable is not set or uses placeholder" -ForegroundColor Red
    Write-Host "Please set it in .env file or environment variables" -ForegroundColor Yellow
    exit 1
}

# Override from environment if set (with validation)
if ($env:POSTGRES_HOST -and $env:POSTGRES_HOST -notlike '*${*') { 
    $DbHost = $env:POSTGRES_HOST 
}

if ($env:POSTGRES_PORT) {
    $portValue = $env:POSTGRES_PORT
    # Skip if contains unresolved variable reference
    if ($portValue -notlike '*${*' -and [int]::TryParse($portValue, [ref]$null)) {
        $DbPort = [int]$portValue
    } else {
        Write-Host "Warning: POSTGRES_PORT contains unresolved variable, using default: $DbPort" -ForegroundColor Yellow
    }
}

if ($env:POSTGRES_DB -and $env:POSTGRES_DB -notlike '*${*') { 
    $DbName = $env:POSTGRES_DB 
}

if ($env:POSTGRES_USER -and $env:POSTGRES_USER -notlike '*${*') { 
    $DbUser = $env:POSTGRES_USER 
}

Write-Host "=== DevHunt Database Reset ===" -ForegroundColor Cyan
Write-Host "Database: $DbName on $DbHost`:$DbPort" -ForegroundColor Yellow
Write-Host "User: $DbUser" -ForegroundColor Yellow

if (-not $Force) {
    $confirm = Read-Host "WARNING: This will DROP and RECREATE the database. All data will be lost! Continue? (yes/no)"
    if ($confirm -ne "yes") {
        Write-Host "Reset cancelled" -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "`nStarting database reset at $(Get-Date)" -ForegroundColor Green

# Check if psql is available, otherwise use Docker
$useDocker = $false
$psqlCmd = Get-Command psql -ErrorAction SilentlyContinue

if (-not $psqlCmd) {
    Write-Host "psql not found locally, using Docker..." -ForegroundColor Yellow
    $useDocker = $true
    
    # Check if Docker is available
    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $dockerCmd) {
        Write-Host "Error: Neither psql nor docker is available!" -ForegroundColor Red
        Write-Host "Please install PostgreSQL client or Docker" -ForegroundColor Yellow
        exit 1
    }
    
    # Check if container is running
    $containerName = $env:CONTAINER_DB_NAME
    if (-not $containerName) {
        $containerName = "database-postgres"
    }
    
    $containerRunning = docker ps --filter "name=$containerName" --format "{{.Names}}" 2>&1
    if (-not $containerRunning -or $containerRunning -notlike "*$containerName*") {
        Write-Host "Error: Database container '$containerName' is not running!" -ForegroundColor Red
        Write-Host "Please start it with: docker-compose up -d db" -ForegroundColor Yellow
        exit 1
    }
}

try {
    if ($useDocker) {
        # Use Docker to execute psql commands
        Write-Host "Terminating existing connections..." -ForegroundColor Yellow
        docker exec -e PGPASSWORD=$DbPassword $containerName psql -U $DbUser -d postgres -c "SELECT pg_terminate_backend(pg_stat_activity.pid) FROM pg_stat_activity WHERE pg_stat_activity.datname = '$DbName' AND pid <> pg_backend_pid();" 2>&1 | Out-Null
        
        Write-Host "Dropping database..." -ForegroundColor Yellow
        docker exec -e PGPASSWORD=$DbPassword $containerName psql -U $DbUser -d postgres -c "DROP DATABASE IF EXISTS $DbName;" 2>&1 | Out-Null
        
        Write-Host "Creating database..." -ForegroundColor Yellow
        docker exec -e PGPASSWORD=$DbPassword $containerName psql -U $DbUser -d postgres -c "CREATE DATABASE $DbName;" 2>&1 | Out-Null
    } else {
        # Use local psql
        $env:PGPASSWORD = $DbPassword
        
        Write-Host "Terminating existing connections..." -ForegroundColor Yellow
        & psql -h $DbHost -p $DbPort -U $DbUser -d postgres -c "SELECT pg_terminate_backend(pg_stat_activity.pid) FROM pg_stat_activity WHERE pg_stat_activity.datname = '$DbName' AND pid <> pg_backend_pid();" 2>&1 | Out-Null
        
        Write-Host "Dropping database..." -ForegroundColor Yellow
        & psql -h $DbHost -p $DbPort -U $DbUser -d postgres -c "DROP DATABASE IF EXISTS $DbName;" 2>&1 | Out-Null
        
        Write-Host "Creating database..." -ForegroundColor Yellow
        & psql -h $DbHost -p $DbPort -U $DbUser -d postgres -c "CREATE DATABASE $DbName;" 2>&1 | Out-Null
    }
    
    Write-Host "Database reset completed successfully!" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "1. Run migrations: dotnet ef database update --project DevHunt.Infrastructure --startup-project DevHunt.CoreApi" -ForegroundColor White
    Write-Host "2. Run seed script: dotnet run --project DevHunt.DatabaseSeeder" -ForegroundColor White
    
} catch {
    Write-Host "Error occurred: $_" -ForegroundColor Red
    exit 1
} finally {
    # Clear password from environment (only if using local psql)
    if (-not $useDocker) {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    }
}

Write-Host "`nReset completed at $(Get-Date)" -ForegroundColor Green

