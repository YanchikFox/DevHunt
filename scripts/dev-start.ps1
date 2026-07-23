<#
Dev start helper
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [int]$ScaleIntegrationGateway = 1,
    [int]$ScaleNotificationService = 1,
    [int]$ScaleCoreApi = 1,
    [int]$ScaleAuthService = 1,
    [switch]$StartMonitoring
)

$ErrorActionPreference = 'Stop'

function Get-ComposeInvocation {
    param(
        [string[]]$BaseArgs,
        [string]$Profile = 'dev'
    )

    $dockerCompose = Get-Command docker-compose -ErrorAction SilentlyContinue
    if ($dockerCompose) {
        if ($Profile) {
            $env:COMPOSE_PROFILES = $Profile
        } elseif (Test-Path Env:COMPOSE_PROFILES) {
            Remove-Item Env:COMPOSE_PROFILES -ErrorAction SilentlyContinue
        }
        return @{ Command = 'docker-compose'; Arguments = $BaseArgs }
    }

    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if ($docker) {
        $args = @('compose')
        if ($Profile) {
            $args += @('--profile', $Profile)
        }
        $args += $BaseArgs
        return @{ Command = 'docker'; Arguments = $args }
    }

    throw 'Docker Compose is not installed. Install either docker compose v2 ("docker compose") or docker-compose v1.'
}

function Import-DotEnv {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $backup = @{} # Track original values to restore later

    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()

        if (-not $trimmed -or $trimmed.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -lt 1) {
            continue
        }

        $name = $trimmed.Substring(0, $separatorIndex).Trim()
        if (-not $name) {
            continue
        }

        $value = $trimmed.Substring($separatorIndex + 1).Trim()
        if ($value.StartsWith('"') -and $value.EndsWith('"')) {
            $value = $value.Trim('"')
        }
        elseif ($value.StartsWith("'") -and $value.EndsWith("'")) {
            $value = $value.Trim("'")
        }

        if (-not $backup.ContainsKey($name)) {
            if (Test-Path "Env:$name") {
                $backup[$name] = [pscustomobject]@{
                    HasValue = $true
                    Value     = (Get-Item "Env:$name").Value
                }
            }
            else {
                $backup[$name] = [pscustomobject]@{ HasValue = $false; Value = $null }
            }
        }

        Set-Item -Path "Env:$name" -Value $value
    }

    return $backup
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).ProviderPath
Push-Location $repoRoot

# Автоматически включаем BuildKit для оптимизированной сборки
$env:DOCKER_BUILDKIT = "1"
$env:COMPOSE_DOCKER_CLI_BUILD = "1"

$envBackup = $null

try {
    $envFile = Join-Path $repoRoot '.env'
    if (-not (Test-Path $envFile)) {
        throw "Environment file '$envFile' not found. Create it from env.example before starting the stack."
    }

    $envBackup = Import-DotEnv -Path $envFile

    $baseArgs = @('--env-file', $envFile, 'up')
    if ($Build) {
        $baseArgs += '--build'
    }

    if ($ScaleIntegrationGateway -gt 1) {
        $baseArgs += '--scale'
        $baseArgs += "integration-gateway=$ScaleIntegrationGateway"
    }
    if ($ScaleNotificationService -gt 1) {
        $baseArgs += '--scale'
        $baseArgs += "notification-service=$ScaleNotificationService"
    }
    if ($ScaleCoreApi -gt 1) {
        $baseArgs += '--scale'
        $baseArgs += "core-api=$ScaleCoreApi"
    }
    if ($ScaleAuthService -gt 1) {
        $baseArgs += '--scale'
        $baseArgs += "auth-service=$ScaleAuthService"
    }

    $baseArgs += '-d'

    $compose = Get-ComposeInvocation -BaseArgs $baseArgs

    Write-Host 'Starting DevHunt (development profile)...' -ForegroundColor Cyan
    & $compose.Command @($compose.Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose returned exit code $LASTEXITCODE."
    }

    if ($StartMonitoring) {
        Write-Host ''
        Write-Host 'Starting monitoring stack (Prometheus, OpenObserve, Jaeger)...' -ForegroundColor Cyan
        $monitorArgs = @('--env-file', $envFile, 'up', '-d', 'monitoring', 'openobserve', 'tracing-service')
        $monitorCompose = Get-ComposeInvocation -BaseArgs $monitorArgs -Profile 'monitoring'
        & $monitorCompose.Command @($monitorCompose.Arguments)
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose (monitoring) returned exit code $LASTEXITCODE."
        }
    }

    Write-Host ''
    Write-Host 'Development environment started.' -ForegroundColor Green
    Write-Host ''
    Write-Host ''
    Write-Host 'Scaling:' -ForegroundColor Cyan
    Write-Host "  Auth Service replicas:    $ScaleAuthService"
    Write-Host "  Core API replicas:      $ScaleCoreApi"
    Write-Host "  Integration GW replicas: $ScaleIntegrationGateway"
    Write-Host "  Notification replicas:   $ScaleNotificationService"
    Write-Host ''
    Write-Host 'Access points:' -ForegroundColor Cyan
    Write-Host '  Frontend:     http://localhost:3000'
    Write-Host '  Auth API:     http://localhost:7001'
    Write-Host '  Core API:     http://localhost:7002'
    Write-Host '  API Gateway:  http://localhost'
    Write-Host ''
    Write-Host 'Monitoring:' -ForegroundColor Cyan
    Write-Host '  Swagger UI:   http://localhost:7001/swagger (Auth)'
    Write-Host '  Swagger UI:   http://localhost:7002/swagger (Core API)'
    Write-Host '  Prometheus:   http://localhost:9090'
    Write-Host '  Jaeger:       http://localhost:16686'
    Write-Host '  OpenObserve:  http://localhost:5080'
    Write-Host ''
    Write-Host 'All ports are exposed for debugging. Use ".\scripts\dev-stop.ps1" to stop.' -ForegroundColor Yellow
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
            }
            else {
                Remove-Item -Path "Env:$varName" -ErrorAction SilentlyContinue
            }
        }
    }

    if (Test-Path Env:COMPOSE_PROFILES) {
        Remove-Item Env:COMPOSE_PROFILES -ErrorAction SilentlyContinue
    }
    Pop-Location
}
