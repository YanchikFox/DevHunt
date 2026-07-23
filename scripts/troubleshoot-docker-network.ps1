# Скрипт для диагностики и исправления проблем с сетью Docker на Windows

# Suppress PSScriptAnalyzer warning about hardcoded computer names
# These are external DNS names for network connectivity checks, not actual computer names
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingComputerNameHardcoded', '', Justification='External DNS names for connectivity checks')]
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

Write-Host "=== Диагностика проблем с сетью Docker ===" -ForegroundColor Cyan
Write-Host ""

# Проверка 1: Docker Desktop запущен?
Write-Host "[1/6] Проверка Docker Desktop..." -ForegroundColor Yellow
try {
    $dockerVersion = docker version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Docker Desktop запущен" -ForegroundColor Green
    } else {
        Write-Host "✗ Docker Desktop не запущен или недоступен" -ForegroundColor Red
        Write-Host "  Запустите Docker Desktop и попробуйте снова" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "✗ Docker не установлен или не в PATH" -ForegroundColor Red
    exit 1
}

# Проверка 2: Сетевое подключение к реестрам
Write-Host ""
Write-Host "[2/6] Проверка подключения к Docker Hub..." -ForegroundColor Yellow
try {
    $testConnection = Test-NetConnection -ComputerName registry-1.docker.io -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue
    if ($testConnection) {
        Write-Host "✓ Подключение к Docker Hub работает" -ForegroundColor Green
    } else {
        Write-Host "✗ Не удается подключиться к Docker Hub" -ForegroundColor Red
        Write-Host "  Проверьте интернет-соединение и настройки прокси" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠ Не удалось проверить подключение (может быть нормально)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[3/6] Проверка подключения к Microsoft Container Registry..." -ForegroundColor Yellow
try {
    $testConnection = Test-NetConnection -ComputerName mcr.microsoft.com -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue
    if ($testConnection) {
        Write-Host "✓ Подключение к MCR работает" -ForegroundColor Green
    } else {
        Write-Host "✗ Не удается подключиться к MCR" -ForegroundColor Red
        Write-Host "  Проверьте интернет-соединение и настройки прокси" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠ Не удалось проверить подключение (может быть нормально)" -ForegroundColor Yellow
}

# Проверка 3: Пробуем загрузить простой образ
Write-Host ""
Write-Host "[4/6] Тестовая загрузка образа..." -ForegroundColor Yellow
try {
    Write-Host "  Пытаемся загрузить hello-world..." -ForegroundColor Gray
    docker pull hello-world 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Загрузка образов работает" -ForegroundColor Green
        docker rmi hello-world 2>&1 | Out-Null
    } else {
        Write-Host "✗ Не удалось загрузить образ" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Ошибка при загрузке образа" -ForegroundColor Red
}

# Проверка 4: Настройки прокси Docker
Write-Host ""
Write-Host "[5/6] Проверка настроек прокси Docker..." -ForegroundColor Yellow
$dockerConfigPath = "$env:USERPROFILE\.docker\config.json"
if (Test-Path $dockerConfigPath) {
    try {
        $dockerConfig = Get-Content $dockerConfigPath | ConvertFrom-Json
        if ($dockerConfig.proxies) {
            Write-Host "✓ Настройки прокси найдены" -ForegroundColor Green
            Write-Host "  HTTP Proxy: $($dockerConfig.proxies.default.httpProxy)" -ForegroundColor Gray
            Write-Host "  HTTPS Proxy: $($dockerConfig.proxies.default.httpsProxy)" -ForegroundColor Gray
        } else {
            Write-Host "  Настройки прокси не найдены (это нормально, если прокси не используется)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "  Не удалось прочитать конфигурацию Docker" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Файл конфигурации Docker не найден (это нормально)" -ForegroundColor Gray
}

# Проверка 5: Проблемы с портами
Write-Host ""
Write-Host "[6/6] Проверка занятых портов..." -ForegroundColor Yellow
$commonPorts = @(443, 80, 2375, 2376)
$conflicts = @()
foreach ($port in $commonPorts) {
    $connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($connection) {
        $conflicts += $port
    }
}
if ($conflicts.Count -eq 0) {
    Write-Host "✓ Нет конфликтов портов" -ForegroundColor Green
} else {
    Write-Host "⚠ Обнаружены потенциальные конфликты портов: $($conflicts -join ', ')" -ForegroundColor Yellow
}

# Рекомендации
Write-Host ""
Write-Host "=== Рекомендации ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Если проблема сохраняется, попробуйте:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Перезапустите Docker Desktop:" -ForegroundColor White
Write-Host "   - Закройте Docker Desktop полностью" -ForegroundColor Gray
Write-Host "   - Запустите снова" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Проверьте настройки сети в Docker Desktop:" -ForegroundColor White
Write-Host "   - Откройте Docker Desktop -> Settings -> Resources -> Network" -ForegroundColor Gray
Write-Host "   - Убедитесь, что 'Use kernel networking' включен" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Если используете прокси, настройте его в Docker Desktop:" -ForegroundColor White
Write-Host "   - Settings -> Resources -> Proxies" -ForegroundColor Gray
Write-Host "   - Добавьте настройки прокси" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Проверьте Windows Firewall:" -ForegroundColor White
Write-Host "   - Убедитесь, что Docker разрешен в брандмауэре" -ForegroundColor Gray
Write-Host "   - Или временно отключите брандмауэр для теста" -ForegroundColor Gray
Write-Host ""
Write-Host "5. Попробуйте использовать VPN или другую сеть:" -ForegroundColor White
Write-Host "   - Некоторые корпоративные сети блокируют Docker" -ForegroundColor Gray
Write-Host ""
Write-Host "6. Очистите кэш Docker:" -ForegroundColor White
Write-Host "   docker system prune -a" -ForegroundColor Gray
Write-Host ""

