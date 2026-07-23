# Test API endpoints to verify data is available

Write-Host "Testing API endpoints..." -ForegroundColor Cyan

# Test projects endpoint
Write-Host "`n1. Testing GET /api/projects" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:7002/api/projects" -Method Get
    Write-Host "   Status: OK" -ForegroundColor Green
    Write-Host "   Total projects: $($response.data.Count)" -ForegroundColor Green
    if ($response.data.Count -gt 0) {
        Write-Host "   First project: $($response.data[0].title)" -ForegroundColor Green
        Write-Host "   Status: $($response.data[0].status)" -ForegroundColor Green
    }
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
}

# Test projects with status filter
Write-Host "`n2. Testing GET /api/projects?status=recruiting" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:7002/api/projects?status=recruiting" -Method Get
    Write-Host "   Status: OK" -ForegroundColor Green
    Write-Host "   Recruiting projects: $($response.data.Count)" -ForegroundColor Green
    if ($response.data.Count -gt 0) {
        $response.data | ForEach-Object {
            Write-Host "   - $($_.title) ($($_.status))" -ForegroundColor Cyan
        }
    }
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
}

# Test from frontend container
Write-Host "`n3. Testing API from frontend container" -ForegroundColor Yellow
try {
    $response = docker exec frontend-web wget -q -O - http://core-api:8080/api/projects 2>&1
    if ($response -match '"data"') {
        Write-Host "   Status: OK (can reach API from container)" -ForegroundColor Green
    } else {
        Write-Host "   Response: $($response.Substring(0, [Math]::Min(200, $response.Length)))" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
}

Write-Host "`nDone!" -ForegroundColor Cyan

