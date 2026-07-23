<#
Быстрая пересборка и перезапуск (для разработки)
Использование:
  .\scripts\dev-rebuild.ps1              # Пересобрать и перезапустить все
  .\scripts\dev-rebuild.ps1 frontend     # Только фронтенд
  .\scripts\dev-rebuild.ps1 core-api     # Только Core API
#>
[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [string]$Service = ""
)

# Просто вызываем dev-restart с пересборкой
if ($Service) {
    & "$PSScriptRoot\dev-restart.ps1" -Service $Service
} else {
    & "$PSScriptRoot\dev-restart.ps1"
}

