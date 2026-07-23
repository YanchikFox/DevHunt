# Script to test production API without Swagger
# This script checks all API endpoints and services

param(
    [string]$BaseUrl = "http://localhost",
    [switch]$Verbose
)

Write-Host "=== Production API Health Check ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray
Write-Host ""

# Colors for output
function Write-Success { param($msg) Write-Host "✓ $msg" -ForegroundColor Green }
function Write-Error { param($msg) Write-Host "✗ $msg" -ForegroundColor Red }
function Write-Info { param($msg) Write-Host "→ $msg" -ForegroundColor Yellow }

# Test function
function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{}
    )
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 204) {
            Write-Success "$Name ($($response.StatusCode))"
            if ($Verbose) {
                Write-Host "  Response: $($response.Content.Substring(0, [Math]::Min(100, $response.Content.Length)))..." -ForegroundColor Gray
            }
            return $true
        } else {
            Write-Info "$Name ($($response.StatusCode))"
            return $false
        }
    } catch {
        Write-Error "$Name - $($_.Exception.Message)"
        return $false
    }
}

# Test API Gateway
Write-Host "--- API Gateway ---" -ForegroundColor Cyan
Test-Endpoint "API Gateway (HTTP)" "$BaseUrl" | Out-Null

# Test Auth Service
Write-Host "`n--- Auth Service ---" -ForegroundColor Cyan
Test-Endpoint "Health Check" "http://localhost:7001/health" | Out-Null
Test-Endpoint "Metrics" "http://localhost:7001/metrics" | Out-Null

# Test Core API
Write-Host "`n--- Core API ---" -ForegroundColor Cyan
Test-Endpoint "Health Check" "http://localhost:7002/health" | Out-Null
Test-Endpoint "Metrics" "http://localhost:7002/metrics" | Out-Null

# Test Core API endpoints (public)
Write-Host "`n--- Core API Endpoints (Public) ---" -ForegroundColor Cyan
Test-Endpoint "Get All Skills" "http://localhost:7002/api/skills?page=1&pageSize=10" | Out-Null
Test-Endpoint "Get All Achievements" "http://localhost:7002/api/achievements?page=1&pageSize=10" | Out-Null

# Test Infrastructure
Write-Host "`n--- Infrastructure Services ---" -ForegroundColor Cyan
Test-Endpoint "Prometheus Metrics" "http://localhost:9090/metrics" | Out-Null
Test-Endpoint "Jaeger UI" "http://localhost:16686" | Out-Null

# Test with authentication (if token provided)
if ($env:TEST_TOKEN) {
    Write-Host "`n--- Authenticated Endpoints ---" -ForegroundColor Cyan
    $headers = @{
        "Authorization" = "Bearer $env:TEST_TOKEN"
    }
    Test-Endpoint "Get My Profile" "http://localhost:7002/api/profile/me" -Headers $headers | Out-Null
    Test-Endpoint "Get My Projects" "http://localhost:7002/api/projects?page=1&pageSize=10" -Headers $headers | Out-Null
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To test with authentication, set environment variable:" -ForegroundColor Yellow
Write-Host '  $env:TEST_TOKEN = "your-jwt-token"' -ForegroundColor Gray
Write-Host ""
Write-Host "To get a token, register/login via:" -ForegroundColor Yellow
Write-Host "  POST http://localhost:7001/api/auth/register" -ForegroundColor Gray
Write-Host "  POST http://localhost:7001/api/auth/login" -ForegroundColor Gray

