[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Get-ComposeInvocation {
    param(
        [string[]]$BaseArgs
    )

    $dockerCompose = Get-Command docker-compose -ErrorAction SilentlyContinue
    if ($dockerCompose) {
        $env:COMPOSE_PROFILES = 'dev'
        return @{ Command = 'docker-compose'; Arguments = $BaseArgs }
    }

    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if ($docker) {
        $args = @('compose', '--profile', 'dev')
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
        throw "Environment file '$envFile' not found. A .env file is required to stop the stack cleanly."
    }

    $envBackup = Import-DotEnv -Path $envFile

    $baseArgs = @('--env-file', $envFile, 'down', '--remove-orphans')
    $compose = Get-ComposeInvocation -BaseArgs $baseArgs

    Write-Host 'Stopping DevHunt (development profile)...' -ForegroundColor Cyan
    & $compose.Command @($compose.Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose returned exit code $LASTEXITCODE."
    }

    Write-Host 'Development environment stopped.' -ForegroundColor Green
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
