# Скрипт для исправления проблем с сетью Docker на Windows

[CmdletBinding()]
param(
    [string]$ProxyHttp,
    [string]$ProxyHttps,
    [switch]$ResetNetwork
)

$ErrorActionPreference = 'Stop'

Write-Host "=== Исправление проблем с сетью Docker ===" -ForegroundColor Cyan
Write-Host ""

# Создаем директорию для конфигурации Docker, если её нет
$dockerDir = "$env:USERPROFILE\.docker"
if (-not (Test-Path $dockerDir)) {
    New-Item -ItemType Directory -Path $dockerDir -Force | Out-Null
    Write-Host "✓ Создана директория для конфигурации Docker" -ForegroundColor Green
}

$configPath = "$dockerDir\config.json"

# Если нужно сбросить сеть
if ($ResetNetwork) {
    Write-Host "[1/3] Сброс сетевых настроек Docker..." -ForegroundColor Yellow
    if (Test-Path $configPath) {
        Remove-Item $configPath -Force
        Write-Host "✓ Конфигурация Docker удалена" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "Перезапустите Docker Desktop и попробуйте снова" -ForegroundColor Yellow
    exit 0
}

# Настройка прокси (если указан)
if ($ProxyHttp -or $ProxyHttps) {
    Write-Host "[1/3] Настройка прокси для Docker..." -ForegroundColor Yellow
    
    $config = @{}
    if (Test-Path $configPath) {
        try {
            $config = Get-Content $configPath | ConvertFrom-Json -AsHashtable
        } catch {
            $config = @{}
        }
    }
    
    if (-not $config.proxies) {
        $config.proxies = @{}
    }
    
    if (-not $config.proxies.default) {
        $config.proxies.default = @{}
    }
    
    if ($ProxyHttp) {
        $config.proxies.default.httpProxy = $ProxyHttp
        Write-Host "✓ HTTP прокси установлен: $ProxyHttp" -ForegroundColor Green
    }
    
    if ($ProxyHttps) {
        $config.proxies.default.httpsProxy = $ProxyHttps
        Write-Host "✓ HTTPS прокси установлен: $ProxyHttps" -ForegroundColor Green
    }
    
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath
    Write-Host "✓ Конфигурация сохранена" -ForegroundColor Green
    Write-Host ""
    Write-Host "Перезапустите Docker Desktop для применения изменений" -ForegroundColor Yellow
    exit 0
}

# Основные исправления
Write-Host "[1/3] Проверка Docker Desktop..." -ForegroundColor Yellow
try {
    docker version | Out-Null
    Write-Host "✓ Docker Desktop работает" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker Desktop не запущен" -ForegroundColor Red
    Write-Host "  Запустите Docker Desktop и попробуйте снова" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "[2/3] Очистка кэша Docker..." -ForegroundColor Yellow
try {
    docker system prune -f | Out-Null
    Write-Host "✓ Кэш очищен" -ForegroundColor Green
} catch {
    Write-Host "⚠ Не удалось очистить кэш (может быть нормально)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[3/3] Тестовая загрузка образа..." -ForegroundColor Yellow
try {
    Write-Host "  Загружаем hello-world для проверки..." -ForegroundColor Gray
    docker pull hello-world 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Загрузка образов работает!" -ForegroundColor Green
        docker rmi hello-world 2>&1 | Out-Null
    } else {
        Write-Host "✗ Проблема сохраняется" -ForegroundColor Red
        Write-Host ""
        Write-Host "Попробуйте:" -ForegroundColor Yellow
        Write-Host "  1. Перезапустить Docker Desktop" -ForegroundColor White
        Write-Host "  2. Запустить: .\scripts\troubleshoot-docker-network.ps1" -ForegroundColor White
        Write-Host "  3. Проверить настройки прокси в Docker Desktop" -ForegroundColor White
        exit 1
    }
} catch {
    Write-Host "✗ Ошибка при загрузке образа" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Готово! ===" -ForegroundColor Green
Write-Host "Теперь попробуйте запустить: .\scripts\dev-start.ps1 -Build" -ForegroundColor Cyan

