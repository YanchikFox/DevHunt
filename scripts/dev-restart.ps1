<#
Удобный скрипт для перезапуска сервисов с пересборкой
Использование:
  .\scripts\dev-restart.ps1                    # Перезапустить все сервисы
  .\scripts\dev-restart.ps1 -Service frontend   # Перезапустить только фронтенд
  .\scripts\dev-restart.ps1 -Service core-api   # Перезапустить только Core API
  .\scripts\dev-restart.ps1 -Service auth-service # Перезапустить только Auth Service
  .\scripts\dev-restart.ps1 -NoBuild            # Перезапустить без пересборки
#>
[CmdletBinding()]
param(
    [string]$Service = "",
    [switch]$NoBuild,
    [switch]$NoCache
)

$ErrorActionPreference = 'Stop'

# Включаем BuildKit автоматически
$env:DOCKER_BUILDKIT = "1"
$env:COMPOSE_DOCKER_CLI_BUILD = "1"

function Get-ComposeInvocation {
    param(
        [string[]]$BaseArgs
    )

    $dockerCompose = Get-Command docker-compose -ErrorAction SilentlyContinue
    if ($dockerCompose) {
        return @{ Command = 'docker-compose'; Arguments = $BaseArgs }
    }

    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if ($docker) {
        $args = @('compose')
        $args += $BaseArgs
        return @{ Command = 'docker'; Arguments = $args }
    }

    throw 'Docker Compose is not installed.'
}

function Import-DotEnv {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return @{} }
    
    $backup = @{}
    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }
        
        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -lt 1) { continue }
        
        $name = $trimmed.Substring(0, $separatorIndex).Trim()
        $value = $trimmed.Substring($separatorIndex + 1).Trim().Trim('"').Trim("'")
        
        if (-not $backup.ContainsKey($name)) {
            if (Test-Path "Env:$name") {
                $backup[$name] = @{ HasValue = $true; Value = (Get-Item "Env:$name").Value }
            } else {
                $backup[$name] = @{ HasValue = $false; Value = $null }
            }
        }
        Set-Item -Path "Env:$name" -Value $value
    }
    return $backup
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).ProviderPath
Push-Location $repoRoot

$envBackup = $null

try {
    $envFile = Join-Path $repoRoot '.env'
    if (-not (Test-Path $envFile)) {
        Write-Warning "Файл .env не найден. Создайте его из env.example"
    } else {
        $envBackup = Import-DotEnv -Path $envFile
    }

    # Определяем команды
    $buildArgs = @('--env-file', $envFile, 'build')
    $upArgs = @('--env-file', $envFile, 'up', '-d')
    $downArgs = @('--env-file', $envFile, 'down')

    if ($NoCache) {
        $buildArgs += '--no-cache'
    }

    if ($Service) {
        # Перезапуск конкретного сервиса
        Write-Host "🔄 Перезапуск сервиса: $Service" -ForegroundColor Cyan
        
        if (-not $NoBuild) {
            Write-Host "📦 Пересборка образа..." -ForegroundColor Yellow
            $buildArgs += $Service
            $compose = Get-ComposeInvocation -BaseArgs $buildArgs
            & $compose.Command @($compose.Arguments)
            if ($LASTEXITCODE -ne 0) {
                throw "Ошибка сборки: $LASTEXITCODE"
            }
        }

        Write-Host "🛑 Остановка сервиса..." -ForegroundColor Yellow
        $downArgs += $Service
        $compose = Get-ComposeInvocation -BaseArgs $downArgs
        & $compose.Command @($compose.Arguments)

        Write-Host "🚀 Запуск сервиса..." -ForegroundColor Yellow
        $upArgs += $Service
        $compose = Get-ComposeInvocation -BaseArgs $upArgs
        & $compose.Command @($compose.Arguments)
        if ($LASTEXITCODE -ne 0) {
            throw "Ошибка запуска: $LASTEXITCODE"
        }

        Write-Host "✅ Сервис $Service перезапущен!" -ForegroundColor Green
    } else {
        # Перезапуск всех сервисов
        Write-Host "🔄 Перезапуск всех сервисов..." -ForegroundColor Cyan
        
        if (-not $NoBuild) {
            Write-Host "📦 Пересборка образов..." -ForegroundColor Yellow
            $compose = Get-ComposeInvocation -BaseArgs $buildArgs
            & $compose.Command @($compose.Arguments)
            if ($LASTEXITCODE -ne 0) {
                throw "Ошибка сборки: $LASTEXITCODE"
            }
        }

        Write-Host "🛑 Остановка сервисов..." -ForegroundColor Yellow
        $downArgs += '--remove-orphans'
        $compose = Get-ComposeInvocation -BaseArgs $downArgs
        & $compose.Command @($compose.Arguments)

        Write-Host "🚀 Запуск сервисов..." -ForegroundColor Yellow
        $compose = Get-ComposeInvocation -BaseArgs $upArgs
        & $compose.Command @($compose.Arguments)
        if ($LASTEXITCODE -ne 0) {
            throw "Ошибка запуска: $LASTEXITCODE"
        }

        Write-Host "✅ Все сервисы перезапущены!" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "📍 Доступные сервисы:" -ForegroundColor Cyan
    Write-Host "  Frontend:     http://localhost:3000"
    Write-Host "  Core API:     http://localhost:7002"
    Write-Host "  Auth API:     http://localhost:7001"
}
catch {
    Write-Error $_
    exit 1
}
finally {
    if ($envBackup) {
        foreach ($entry in $envBackup.GetEnumerator()) {
            $varName = $entry.Key
            $info = $entry.Value
            if ($info.HasValue) {
                Set-Item -Path "Env:$varName" -Value $info.Value
            } else {
                Remove-Item -Path "Env:$varName" -ErrorAction SilentlyContinue
            }
        }
    }
    Pop-Location
}

