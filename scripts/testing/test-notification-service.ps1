# Test Notification Service

Write-Host "`n🔔 Тестирование Notification Service" -ForegroundColor Cyan
Write-Host "=" * 50

$baseUrl = "http://localhost:5003"

# 1. Health Check
Write-Host "`n1. Health Check..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
    Write-Host "   ✓ Service is healthy" -ForegroundColor Green
    Write-Host "   Status: $($response.status)" -ForegroundColor Gray
    Write-Host "   Service: $($response.service)" -ForegroundColor Gray
    Write-Host "   Version: $($response.version)" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Service is not responding" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host "`n   Убедитесь что сервис запущен:" -ForegroundColor Yellow
    Write-Host "   docker-compose up notification-service" -ForegroundColor White
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

# 3. Send Test Email (with plain HTML)
Write-Host "`n3. Send Test Email (Plain HTML)..." -ForegroundColor Yellow
$testEmail = Read-Host "   Enter test email address (or press Enter to skip)"

if ($testEmail) {
    try {
        $body = @{
            userId = [Guid]::NewGuid().ToString()
            type = "email"
            recipient = $testEmail
            subject = "DevHunt Test Email"
            content = "<h1>Test Email from DevHunt</h1><p>This is a test email sent from Notification Service.</p><p>If you received this, the service is working correctly!</p>"
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "$baseUrl/api/notifications/send" -Method Post -Body $body -ContentType "application/json"
        
        if ($response.success) {
            Write-Host "   ✓ Email sent successfully!" -ForegroundColor Green
            Write-Host "   Notification ID: $($response.notificationId)" -ForegroundColor Gray
            Write-Host "   Type: $($response.type)" -ForegroundColor Gray
            Write-Host "   Sent At: $($response.sentAt)" -ForegroundColor Gray
            
            if (-not $env:SMTP_USER -and -not $env:SENDGRID_API_KEY) {
                Write-Host "   ⚠ SMTP credentials not configured - email may not be actually sent" -ForegroundColor Yellow
                Write-Host "   Set SMTP_USER/SMTP_PASSWORD or SENDGRID_API_KEY" -ForegroundColor Gray
            }
        } else {
            Write-Host "   ✗ Failed to send email" -ForegroundColor Red
            Write-Host "   Error: $($response.error)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "   ✗ Error sending email" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "   Response: $responseBody" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "   ⏭ Skipped (no email provided)" -ForegroundColor Yellow
}

# 4. Send Email with Template
Write-Host "`n4. Send Email with Template..." -ForegroundColor Yellow
if ($testEmail) {
    try {
        $body = @{
            userId = [Guid]::NewGuid().ToString()
            type = "email"
            recipient = $testEmail
            subject = "Welcome to DevHunt!"
            template = "welcome"
            data = @{
                userName = "Test User"
            }
        } | ConvertTo-Json -Depth 3
        
        $response = Invoke-RestMethod -Uri "$baseUrl/api/notifications/send" -Method Post -Body $body -ContentType "application/json"
        
        if ($response.success) {
            Write-Host "   ✓ Template email sent!" -ForegroundColor Green
            Write-Host "   Template: welcome" -ForegroundColor Gray
        } else {
            Write-Host "   ✗ Failed to send template email" -ForegroundColor Red
        }
    } catch {
        Write-Host "   ⚠ Error: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ⏭ Skipped (no email provided)" -ForegroundColor Yellow
}

# 5. Batch Notification Test
Write-Host "`n5. Batch Notification Test..." -ForegroundColor Yellow
Write-Host "   (Skipping - requires multiple emails)" -ForegroundColor Gray

Write-Host "`n✅ Тестирование завершено!" -ForegroundColor Green
Write-Host "`n💡 Для реальной отправки email:" -ForegroundColor Cyan
Write-Host "   1. Настройте SMTP:" -ForegroundColor White
Write-Host "      SMTP_HOST=smtp.gmail.com" -ForegroundColor Gray
Write-Host "      SMTP_USER=your_email@gmail.com" -ForegroundColor Gray
Write-Host "      SMTP_PASSWORD=your_app_password" -ForegroundColor Gray
Write-Host "`n   2. Или используйте SendGrid:" -ForegroundColor White
Write-Host "      EMAIL_PROVIDER=sendgrid" -ForegroundColor Gray
Write-Host "      SENDGRID_API_KEY=your_api_key" -ForegroundColor Gray
Write-Host "`n   3. Перезапустите сервис:" -ForegroundColor White
Write-Host "      docker-compose restart notification-service" -ForegroundColor Gray

