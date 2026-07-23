# Test Production Deployment Script
# This script tests all endpoints after deployment

$ErrorActionPreference = "Continue"

Write-Host "🧪 Testing Production Deployment" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host ""

# Get API Gateway URL from environment or use default
$apiUrl = $env:API_URL
if ([string]::IsNullOrEmpty($apiUrl)) {
    $apiUrl = Read-Host "Enter API Gateway URL (e.g., http://localhost or https://your-domain.com)"
}

if ($apiUrl -notmatch '^https?://') {
    $apiUrl = "http://$apiUrl"
}

Write-Host "Testing endpoints at: $apiUrl" -ForegroundColor Yellow
Write-Host ""

$tests = @(
    @{
        Name = "API Gateway Health"
        Url = "$apiUrl/health"
        ExpectedStatus = 200
    },
    @{
        Name = "Core API Health"
        Url = "$apiUrl/api/core/health"
        ExpectedStatus = 200
    },
    @{
        Name = "Auth Service Health"
        Url = "$apiUrl/api/auth/health"
        ExpectedStatus = 200
    },
    @{
        Name = "Prometheus Metrics"
        Url = "$apiUrl/metrics"
        ExpectedStatus = 200
    }
)

$passed = 0
$failed = 0

foreach ($test in $tests) {
    Write-Host "Testing: $($test.Name)" -ForegroundColor Cyan
    Write-Host "  URL: $($test.Url)" -ForegroundColor Gray
    
    try {
        $response = Invoke-WebRequest -Uri $test.Url -Method Get -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
        $statusCode = $response.StatusCode
        
        if ($statusCode -eq $test.ExpectedStatus) {
            Write-Host "  ✓ PASSED (Status: $statusCode)" -ForegroundColor Green
            $passed++
        } else {
            Write-Host "  ✗ FAILED (Expected: $($test.ExpectedStatus), Got: $statusCode)" -ForegroundColor Red
            $failed++
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq $test.ExpectedStatus) {
            Write-Host "  ✓ PASSED (Status: $statusCode)" -ForegroundColor Green
            $passed++
        } else {
            Write-Host "  ✗ FAILED (Error: $($_.Exception.Message))" -ForegroundColor Red
            $failed++
        }
    }
    Write-Host ""
}

# Test HTTPS if available
if ($apiUrl -match '^https://') {
    Write-Host "Testing HTTPS connection..." -ForegroundColor Cyan
    try {
        $response = Invoke-WebRequest -Uri "$apiUrl/health" -Method Get -UseBasicParsing -TimeoutSec 10
        Write-Host "  ✓ HTTPS is working" -ForegroundColor Green
    } catch {
        Write-Host "  ✗ HTTPS failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
}

# Test CORS (if frontend URL is provided)
$frontendUrl = $env:FRONTEND_URL
if (-not [string]::IsNullOrEmpty($frontendUrl)) {
    Write-Host "Testing CORS..." -ForegroundColor Cyan
    try {
        $headers = @{
            "Origin" = $frontendUrl
            "Access-Control-Request-Method" = "GET"
        }
        $response = Invoke-WebRequest -Uri "$apiUrl/api/core/health" -Method Options -Headers $headers -UseBasicParsing -TimeoutSec 10
        if ($response.Headers["Access-Control-Allow-Origin"]) {
            Write-Host "  ✓ CORS is configured" -ForegroundColor Green
        } else {
            Write-Host "  ✗ CORS headers missing" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ✗ CORS test failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
}

# Summary
Write-Host "===============================" -ForegroundColor Cyan
Write-Host "Test Results:" -ForegroundColor Cyan
Write-Host "  Passed: $passed" -ForegroundColor Green
Write-Host "  Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($failed -eq 0) {
    Write-Host "✓ All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ Some tests failed. Check the logs above." -ForegroundColor Red
    exit 1
}


