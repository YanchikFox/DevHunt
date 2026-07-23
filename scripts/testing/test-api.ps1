# DevHunt API Testing Script
# Универсальный скрипт для тестирования Core API и Auth Service

param(
    [switch]$Auth,
    [switch]$Core,
    [switch]$All
)

Write-Host "🧪 DevHunt API Testing Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Цвета для вывода
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Error { param($msg) Write-Host "❌ $msg" -ForegroundColor Red }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Yellow }

# Конфигурация
$AUTH_URL = "http://localhost:7001"
$API_URL = "http://localhost:7002"
$TEST_EMAIL = "test@example.com"
$TEST_PASSWORD = "TestPassword123!"

# Проверка доступности сервисов
Write-Info "Checking services availability..."

try {
    $healthCheck = Invoke-WebRequest -Uri "$API_URL/health" -UseBasicParsing -TimeoutSec 5
    if ($healthCheck.StatusCode -eq 200) {
        Write-Success "Core API is running"
    }
} catch {
    Write-Error "Core API is not responding. Make sure it's running on $API_URL"
    Write-Host "Run: docker-compose up -d" -ForegroundColor Yellow
    exit 1
}

# Функция для регистрации
function Register-User {
    param($Email, $Password)
    
    Write-Info "Registering user: $Email"
    
    try {
        $body = @{
            Email = $Email
            Password = $Password
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "$AUTH_URL/auth/register" `
            -Method Post `
            -Body $body `
            -ContentType "application/json" `
            -ErrorAction Stop

        Write-Success "User registered successfully"
        return $response.token
    } catch {
        if ($_.Exception.Response.StatusCode -eq 400) {
            Write-Info "User already exists, trying to login..."
            return $null
        } else {
            Write-Error "Registration failed: $($_.Exception.Message)"
            return $null
        }
    }
}

# Функция для логина
function Login-User {
    param($Email, $Password)
    
    Write-Info "Logging in: $Email"
    
    try {
        $body = @{
            Email = $Email
            Password = $Password
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "$AUTH_URL/auth/login" `
            -Method Post `
            -Body $body `
            -ContentType "application/json" `
            -ErrorAction Stop

        Write-Success "Login successful"
        return $response.token
    } catch {
        Write-Error "Login failed: $($_.Exception.Message)"
        return $null
    }
}

# Функция для запроса с токеном
function Invoke-AuthorizedRequest {
    param($Uri, $Method = "Get", $Body = $null)
    
    $headers = @{
        "Authorization" = "Bearer $script:TOKEN"
        "Content-Type" = "application/json"
    }

    try {
        if ($Body) {
            $bodyJson = $Body | ConvertTo-Json
            return Invoke-RestMethod -Uri $Uri -Method $Method -Headers $headers -Body $bodyJson
        } else {
            return Invoke-RestMethod -Uri $Uri -Method $Method -Headers $headers
        }
    } catch {
        Write-Error "Request failed: $($_.Exception.Message)"
        return $null
    }
}

# Тестирование Auth Service
if ($Auth -or $All -or (-not $Core)) {
    Write-Host "`n=== AUTH SERVICE TESTS ===" -ForegroundColor Yellow
    
    $token = Register-User -Email $TEST_EMAIL -Password $TEST_PASSWORD
    
    if (-not $token) {
        $token = Login-User -Email $TEST_EMAIL -Password $TEST_PASSWORD
    }
    
    if (-not $token) {
        Write-Error "Failed to authenticate. Exiting."
        exit 1
    }
    
    $script:TOKEN = $token
    Write-Success "Token obtained: $($token.Substring(0, [Math]::Min(50, $token.Length)))..."
}

# Тестирование Core API
if ($Core -or $All -or (-not $Auth)) {
    Write-Host "`n=== CORE API TESTS ===" -ForegroundColor Yellow
    
    # Получить токен если его нет
    if (-not $script:TOKEN) {
        $token = Register-User -Email $TEST_EMAIL -Password $TEST_PASSWORD
        if (-not $token) {
            $token = Login-User -Email $TEST_EMAIL -Password $TEST_PASSWORD
        }
        $script:TOKEN = $token
    }
    
    # Тест 1: Профиль
    Write-Host "`n[1] Testing Profile..." -ForegroundColor Cyan
    $profile = Invoke-AuthorizedRequest -Uri "$API_URL/api/profile/me"
    if ($profile) {
        Write-Success "Profile retrieved: $($profile.fullName ?? $profile.email)"
        Write-Host "  Role: $($profile.role)" -ForegroundColor Gray
        Write-Host "  Rating: $($profile.rating)" -ForegroundColor Gray
    }
    
    # Тест 2: Публичные endpoints (без авторизации)
    Write-Host "`n[2] Testing Public Endpoints..." -ForegroundColor Cyan
    
    try {
        $users = Invoke-RestMethod -Uri "$API_URL/api/users" -Method Get
        Write-Success "Users endpoint works. Found $($users.Count ?? 0) users"
    } catch {
        Write-Error "Failed to get users: $($_.Exception.Message)"
    }
    
    try {
        $projects = Invoke-RestMethod -Uri "$API_URL/api/projects" -Method Get
        Write-Success "Projects endpoint works. Found $($projects.Count ?? 0) projects"
    } catch {
        Write-Error "Failed to get projects: $($_.Exception.Message)"
    }
    
    try {
        $skills = Invoke-RestMethod -Uri "$API_URL/api/skills" -Method Get
        Write-Success "Skills endpoint works. Found $($skills.Count ?? 0) skills"
    } catch {
        Write-Error "Failed to get skills: $($_.Exception.Message)"
    }
    
    # Тест 3: Создание проекта
    Write-Host "`n[3] Testing Project Creation..." -ForegroundColor Cyan
    $projectBody = @{
        Title = "Test Project $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        Description = "This is a test project created by the test script"
        ShortDescription = "Test project"
        Status = "draft"
        Visibility = "public"
        DifficultyLevel = "beginner"
        ExpectedDurationDays = 7
    }
    
    $newProject = Invoke-AuthorizedRequest -Uri "$API_URL/api/projects" -Method Post -Body $projectBody
    if ($newProject) {
        Write-Success "Project created: $($newProject.title ?? 'Unknown')"
        $projectId = $newProject.id
    }
    
    # Тест 4: Health и метрики
    Write-Host "`n[4] Testing Health & Metrics..." -ForegroundColor Cyan
    try {
        $health = Invoke-RestMethod -Uri "$API_URL/health"
        Write-Success "Health check passed"
    } catch {
        Write-Error "Health check failed"
    }
    
    try {
        $metrics = Invoke-WebRequest -Uri "$API_URL/metrics" -UseBasicParsing
        if ($metrics.StatusCode -eq 200) {
            Write-Success "Metrics endpoint accessible"
        }
    } catch {
        Write-Info "Metrics endpoint not available (expected if not configured)"
    }
    
    # Тест 5: Swagger
    Write-Host "`n[5] Testing Swagger UI..." -ForegroundColor Cyan
    try {
        $swagger = Invoke-WebRequest -Uri "$API_URL/swagger" -UseBasicParsing
        if ($swagger.StatusCode -eq 200) {
            Write-Success "Swagger UI is available at http://localhost:7002/swagger"
        }
    } catch {
        Write-Info "Swagger might not be enabled in production mode"
    }
}

Write-Host "`n================================" -ForegroundColor Cyan
Write-Host "✅ Testing completed!" -ForegroundColor Green
Write-Host "`n📚 Useful Links:" -ForegroundColor Yellow
Write-Host "  Swagger UI: http://localhost:7002/swagger" -ForegroundColor Gray
Write-Host "  RabbitMQ: http://localhost:15672 (devhunt/devhunt_password)" -ForegroundColor Gray
Write-Host "  SeaweedFS Filer: http://localhost:8888" -ForegroundColor Gray

if ($script:TOKEN) {
    Write-Host "`n🔑 Token for manual testing:" -ForegroundColor Cyan
    Write-Host "  $($script:TOKEN.Substring(0, [Math]::Min(50, $script:TOKEN.Length)))..." -ForegroundColor Gray
}

