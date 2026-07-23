[CmdletBinding()]
param(
    [switch]$Build
)

$ErrorActionPreference = 'Stop'

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

$envBackup = $null

try {
    $envFile = Join-Path $repoRoot '.env'
    if (-not (Test-Path $envFile)) {
        throw "Environment file '$envFile' not found. Create it from env.example before starting production."
    }

    $envBackup = Import-DotEnv -Path $envFile

    $composeArgs = @(
        '-f', 'docker-compose.yml',
        '-f', 'docker-compose.prod.yml',
        '--env-file', $envFile,
        'up'
    )

    if ($Build) {
        $composeArgs += '--build'
    }

    $composeArgs += '-d'

    $compose = Get-ComposeInvocation -BaseArgs $composeArgs

    Write-Host 'Starting DevHunt in PRODUCTION mode...' -ForegroundColor Cyan
    Write-Host 'Internal service ports remain closed; only gateway ports are exposed.' -ForegroundColor Yellow

    & $compose.Command @($compose.Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose returned exit code $LASTEXITCODE."
    }

    Write-Host ''
    Write-Host 'Production environment started.' -ForegroundColor Green
    Write-Host ''
    Write-Host 'Access points:' -ForegroundColor Cyan
    Write-Host '  API Gateway:  http://localhost (port 80)'
    Write-Host '  API Gateway:  https://localhost (port 443)'
    Write-Host ''
    Write-Host 'Security features:' -ForegroundColor Cyan
    Write-Host '  - Internal ports closed (DB, Redis, RabbitMQ)'
    Write-Host '  - Swagger disabled'
    Write-Host '  - Strict CORS policy'
    Write-Host '  - Production optimizations enabled'
    Write-Host ''
    Write-Host 'Monitoring:' -ForegroundColor Cyan
    Write-Host '  Health checks: curl http://localhost:7001/health'
    Write-Host '  Metrics:       curl http://localhost:7001/metrics'
    Write-Host '  Prometheus:    http://localhost:9090'
    Write-Host ''
    Write-Host 'Use ".\scripts\prod-stop.ps1" to stop.' -ForegroundColor Gray
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

    Pop-Location
}
