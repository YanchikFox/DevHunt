# Test Integration Gateway

Write-Host "`n🔌 Тестирование Integration Gateway" -ForegroundColor Cyan
Write-Host "=" * 50

$baseUrl = "http://localhost:5002"

# 1. Health Check
Write-Host "`n1. Health Check..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
    Write-Host "   ✓ Service is healthy" -ForegroundColor Green
    Write-Host "   Status: $($response.status)" -ForegroundColor Gray
    Write-Host "   Supported Providers: $($response.supportedProviders -join ', ')" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Service is not responding" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
    exit 1
}

# 2. Root endpoint
Write-Host "`n2. Root Endpoint..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/" -Method Get
    Write-Host "   ✓ Service info retrieved" -ForegroundColor Green
    Write-Host "   Service: $($response.service)" -ForegroundColor Gray
    Write-Host "   Version: $($response.version)" -ForegroundColor Gray
} catch {
    Write-Host "   ⚠ Could not get service info" -ForegroundColor Yellow
}

# 3. Get OAuth URL (GitHub)
Write-Host "`n3. Get OAuth URL for GitHub..." -ForegroundColor Yellow
try {
    $state = [Guid]::NewGuid().ToString()
    $redirectUri = "http://localhost:7002/api/integrations/oauth/github/callback"
    
    $params = @{
        state = $state
        redirect_uri = $redirectUri
    }
    
    $url = "$baseUrl/api/oauth/github/url?" + ($params.GetEnumerator() | ForEach-Object { "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" } | Join-String -Separator "&")
    
    $response = Invoke-RestMethod -Uri $url -Method Get
    
    if ($response.authUrl) {
        Write-Host "   ✓ OAuth URL generated" -ForegroundColor Green
        Write-Host "   Provider: $($response.provider)" -ForegroundColor Gray
        Write-Host "   Auth URL: $($response.authUrl.Substring(0, [Math]::Min(80, $response.authUrl.Length)))..." -ForegroundColor Gray
        
        if (-not $env:GITHUB_CLIENT_ID) {
            Write-Host "   ⚠ GITHUB_CLIENT_ID not set - URL may be invalid" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ✗ Failed to generate OAuth URL" -ForegroundColor Red
    }
} catch {
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "   ⚠ OAuth credentials not configured (expected in dev)" -ForegroundColor Yellow
        Write-Host "   Set GITHUB_CLIENT_ID and GITHUB_CLIENT_SECRET to test fully" -ForegroundColor Gray
    } else {
        Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# 4. Verify Webhook Signature
Write-Host "`n4. Verify Webhook Signature..." -ForegroundColor Yellow
try {
    $testPayload = '{"event":"push","repository":{"name":"test-repo"}}'
    $testSignature = "sha256=test_signature"
    
    $body = @{
        serviceType = "github"
        payload = $testPayload
        signature = $testSignature
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/api/webhooks/verify-signature" -Method Post -Body $body -ContentType "application/json"
    
    Write-Host "   ✓ Signature verification endpoint works" -ForegroundColor Green
    Write-Host "   Is Valid: $($response.isValid)" -ForegroundColor Gray
    Write-Host "   Service Type: $($response.serviceType)" -ForegroundColor Gray
    
    if (-not $response.isValid) {
        Write-Host "   ⚠ Signature invalid (expected - test signature is fake)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ⚠ Error: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n✅ Тестирование завершено!" -ForegroundColor Green
Write-Host "`n💡 Для полного тестирования OAuth:" -ForegroundColor Cyan
Write-Host "   1. Создайте OAuth App на GitHub/GitLab" -ForegroundColor White
Write-Host "   2. Установите переменные:" -ForegroundColor White
Write-Host "      `$env:GITHUB_CLIENT_ID='your_client_id'" -ForegroundColor Gray
Write-Host "      `$env:GITHUB_CLIENT_SECRET='your_client_secret'" -ForegroundColor Gray
Write-Host "   3. Перезапустите сервис: docker-compose restart integration-gateway" -ForegroundColor White

